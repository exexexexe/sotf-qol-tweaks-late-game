using RedLoader;
using Sons.Crafting.Structures;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlueprintShiftFill;

// Shift + E on a blueprint adds every ingredient it still needs that the player carries.
// Polled from Update with our own raycast instead of patching the game's interact
// handler, since small IL2CPP methods can be inlined and never hit a Harmony patch.
internal static class ShiftFill
{
    // Safety cap so a misbehaving blueprint can't spin forever
    private const int MaxItemsPerPress = 2000;

    public static void OnUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;
        if (!keyboard.leftShiftKey.isPressed && !keyboard.rightShiftKey.isPressed)
            return;

        try
        {
            if (LocalPlayer.IsInInventory || LocalPlayer.Inventory == null || LocalPlayer.MainCamTr == null)
                return;

            var node = FindTargetedBlueprint();
            if (node == null)
            {
                RLog.Msg("Shift+E: not looking at a blueprint");
                return;
            }

            Fill(node);
        }
        catch (Exception e)
        {
            RLog.Error($"Shift fill failed: {e}");
        }
    }

    private static StructureCraftingNode FindTargetedBlueprint()
    {
        var cam = LocalPlayer.MainCamTr;
        var hits = Physics.RaycastAll(cam.position, cam.forward, Config.Reach.Value, ~0, QueryTriggerInteraction.Collide);
        StructureCraftingNode best = null;
        var bestDist = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.distance >= bestDist)
                continue;
            var node = hit.collider.GetComponentInParent<StructureCraftingNode>();
            if (node == null)
                continue;
            best = node;
            bestDist = hit.distance;
        }
        return best;
    }

    private static void Fill(StructureCraftingNode node)
    {
        var inventory = LocalPlayer.Inventory;
        RLog.Msg($"Shift+E on '{node.gameObject.name}', {node.CountAddedIngredients()} ingredient(s) already added");

        // Only ever add the ingredient the blueprint asks for next, like pressing E by
        // hand. Adding out of order gets rejected and the items are dropped on the floor.
        var added = 0;
        var compensated = 0;
        var missingId = -1;
        while (added < MaxItemsPerPress && !node.IsStructureComplete())
        {
            var id = node.GetNextRequiredIngredientId();
            if (id <= 0)
                break;
            var before = inventory.AmountOf(id, true, false);
            if (before <= 0)
            {
                missingId = id;
                break;
            }
            if (!node.TryAddIngredient(id, 1, true))
            {
                RLog.Msg($"  blueprint refused item {id}");
                break;
            }

            // If the game didn't take the item from the inventory itself, take it here
            if (inventory.AmountOf(id, true, false) >= before)
            {
                inventory.RemoveItem(id, 1, false, true, true, null, false);
                compensated++;
            }
            added++;
        }

        var complete = node.IsStructureComplete();
        RLog.Msg($"  added {added} item(s) ({compensated} removed from inventory by the mod), " +
                 $"now {node.CountAddedIngredients()} added, complete={complete}, next missing item={missingId}");
        if (!Config.ShowMessage.Value)
            return;
        var text = added > 0 ? $"Added {added} item{(added == 1 ? "" : "s")} to blueprint" : "Nothing to add";
        if (!complete && missingId > 0)
            text += " - you're missing the next material";
        SonsTools.ShowMessage(text, 2.5f);
    }
}
