using HarmonyLib;
using RedLoader;
using Sons.Gameplay.GrabBag;
using Sons.Inventory;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlueprintShiftFill;

// Shift versions of the game's one-at-a-time item transfers.
//
// Grab bag (shelves, giving items to other players / Kelvin / Virginia, drying racks):
// shift-clicking an item keeps repeating that same click until the stack is gone, the
// receiver stops taking items, or the grab bag closes. Repeats are spaced out so each
// tossed item can land before the next one, like clicking quickly by hand.
//
// Item holders (stick/stone/bone/log holders): with shift held, the game's own "add one"
// and "take one" input handlers run again in a loop until nothing more changes.
[HarmonyPatch]
internal static class ShiftTransfer
{
    // Stop a grab bag repeat when the inventory count hasn't dropped for this long
    private const float StallTimeout = 2.5f;
    // Safety cap for the item holder loops
    private const int MaxHolderRepeats = 500;

    private static bool _repeating;

    // Pending grab bag repeat
    private static GrabBagController _bag;
    private static ItemInstance _instance;
    private static int _itemId = -1;
    private static bool _isUnique;
    private static int _itemCount;
    private static int _given;
    private static int _lastAmount;
    private static float _nextRepeatTime;
    private static float _lastProgressTime;

    private static bool ShiftHeld()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GrabBagController), nameof(GrabBagController.OnLayoutItemInteraction))]
    private static void OnLayoutItemInteraction(GrabBagController __instance, ItemInstance itemInstance, bool isUniqueCollection, int itemCount)
    {
        if (_repeating || !Config.ShiftTransferEnabled.Value)
            return;
        try
        {
            // Any normal click cancels a running repeat
            StopRepeat(false);
            var id = itemInstance != null ? itemInstance._itemID : -1;
            RLog.Msg($"Grab bag click: item {id}, unique={isUniqueCollection}, count={itemCount}, shift={ShiftHeld()}");
            if (!ShiftHeld() || id <= 0 || LocalPlayer.Inventory == null)
                return;

            _bag = __instance;
            _instance = itemInstance;
            _itemId = id;
            _isUnique = isUniqueCollection;
            _itemCount = itemCount;
            _given = 0;
            // The original click hasn't run yet, so count the item it is about to send
            _lastAmount = LocalPlayer.Inventory.AmountOf(id, true, false);
            _nextRepeatTime = Time.time + Config.RepeatDelay.Value;
            _lastProgressTime = Time.time;
        }
        catch (Exception e)
        {
            RLog.Error($"Shift click setup failed: {e}");
        }
    }

    public static void OnUpdate()
    {
        if (_bag == null || Time.time < _nextRepeatTime)
            return;

        try
        {
            var inventory = LocalPlayer.Inventory;
            if (inventory == null || !_bag.ReceiverIsValid)
            {
                StopRepeat(true);
                return;
            }

            var amount = inventory.AmountOf(_itemId, true, false);
            if (amount < _lastAmount)
            {
                _given += _lastAmount - amount;
                _lastAmount = amount;
                _lastProgressTime = Time.time;
            }
            if (amount <= 0 || (Config.MaxPerShiftClick.Value > 0 && _given >= Config.MaxPerShiftClick.Value))
            {
                StopRepeat(true);
                return;
            }
            if (Time.time - _lastProgressTime > StallTimeout)
            {
                RLog.Msg($"  item {_itemId} stopped moving (receiver full or refused it)");
                StopRepeat(true);
                return;
            }

            // Unique items (weapons, tools...) are separate instances, so send the next one
            var instance = _isUnique ? inventory.GetItemInstanceOfType(_itemId) : _instance;
            if (instance == null)
            {
                StopRepeat(true);
                return;
            }

            _repeating = true;
            try
            {
                _bag.OnLayoutItemInteraction(instance, _isUnique, _itemCount);
            }
            finally
            {
                _repeating = false;
            }
            _nextRepeatTime = Time.time + Config.RepeatDelay.Value;
        }
        catch (Exception e)
        {
            RLog.Error($"Shift click repeat failed: {e}");
            StopRepeat(false);
        }
    }

    private static void StopRepeat(bool report)
    {
        if (_bag == null)
            return;
        // Count whatever landed since the last check
        try
        {
            var inventory = LocalPlayer.Inventory;
            if (inventory != null)
                _given += Math.Max(0, _lastAmount - inventory.AmountOf(_itemId, true, false));
        }
        catch { }

        RLog.Msg($"  shift click done: moved {_given} of item {_itemId}");
        if (report && Config.ShowMessage.Value && _given > 1)
            SonsTools.ShowMessage($"Moved {_given} items", 2f);
        _bag = null;
        _instance = null;
        _itemId = -1;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InWorldLayoutItemGroup), nameof(InWorldLayoutItemGroup.AddItem), typeof(InputAction.CallbackContext))]
    private static void HolderAddItem(InWorldLayoutItemGroup __instance, InputAction.CallbackContext context)
    {
        RepeatHolder(__instance, "add", () => __instance.AddItem(context));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InWorldLayoutItemGroup), nameof(InWorldLayoutItemGroup.RemoveItem), typeof(InputAction.CallbackContext))]
    private static void HolderRemoveItem(InWorldLayoutItemGroup __instance, InputAction.CallbackContext context)
    {
        RepeatHolder(__instance, "take", () => __instance.RemoveItem(context));
    }

    private static void RepeatHolder(InWorldLayoutItemGroup group, string action, Action repeat)
    {
        if (_repeating || !Config.ShiftTransferEnabled.Value || !ShiftHeld())
            return;

        _repeating = true;
        var moved = 0;
        try
        {
            var limit = Config.MaxPerShiftClick.Value > 0 ? Config.MaxPerShiftClick.Value - 1 : MaxHolderRepeats;
            while (moved < limit)
            {
                var before = group.GetCurrentCount();
                repeat();
                if (group.GetCurrentCount() == before)
                    break;
                moved++;
            }
        }
        catch (Exception e)
        {
            RLog.Error($"Shift {action} on holder failed: {e}");
        }
        finally
        {
            _repeating = false;
        }

        // +1 for the press the game already handled
        RLog.Msg($"Shift {action} on holder '{group.gameObject.name}': {moved + 1} item(s), now {group.GetCurrentCount()}");
        if (Config.ShowMessage.Value && moved > 0)
            SonsTools.ShowMessage(action == "add" ? $"Added {moved + 1} items" : $"Took {moved + 1} items", 2f);
    }
}
