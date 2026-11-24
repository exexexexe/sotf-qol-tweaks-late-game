using RedLoader;
using Sons.Gameplay.Artifact;
using Sons.Items;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArtifactPads;

// 1-9 (number row or numpad) teleports to that pad while the artifact is held in teleport mode.
// Ctrl+number saves your position as that pad, Alt+number deletes it, 0 lists them.
// The artifact's own portal teleport is left untouched as the "home" option.
internal static class PadInput
{
    private static readonly Key[] NumpadKeys =
    {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
        Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9,
    };

    private static readonly Key[] DigitKeys =
    {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
    };

    public static void OnUpdate()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        var pressed = -1;
        for (var i = 0; i < NumpadKeys.Length; i++)
        {
            if (keyboard[NumpadKeys[i]].wasPressedThisFrame || keyboard[DigitKeys[i]].wasPressedThisFrame)
            {
                pressed = i;
                break;
            }
        }
        if (pressed < 0)
            return;

        try
        {
            if (LocalPlayer.Transform == null || LocalPlayer.IsInInventory)
                return;

            var ability = HeldTeleportAbility(out var controller);
            if (ability == null)
                return;

            var ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            var alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            if (pressed == 0)
                Show(PadStore.Summary());
            else if (ctrl)
                SavePad(pressed);
            else if (alt)
                DeletePad(pressed);
            else
                TeleportToPad(pressed, ability, controller);
        }
        catch (Exception e)
        {
            RLog.Error($"Artifact pad action failed: {e}");
        }
    }

    // The local player's artifact, only when it's switched to the teleport ability
    private static TeleportationArtifactAbility HeldTeleportAbility(out ArtifactItemController held)
    {
        held = null;
        foreach (var controller in UnityEngine.Object.FindObjectsOfType<ArtifactItemController>())
        {
            if (!controller.isActiveAndEnabled || !controller.transform.IsChildOf(LocalPlayer.Transform))
                continue;
            held = controller;
            return controller.GetActiveArtifactAbility()?.TryCast<TeleportationArtifactAbility>();
        }
        return null;
    }

    private static void SavePad(int n)
    {
        if (LocalPlayer.IsInCaves)
        {
            Show("Can't save a pad inside a cave");
            return;
        }
        var tr = LocalPlayer.Transform;
        PadStore.Set(n, tr.position, tr.eulerAngles.y);
        RLog.Msg($"Saved pad {n} at {tr.position}");
        Show($"Saved pad {n}");
    }

    private static void DeletePad(int n)
    {
        PadStore.Remove(n);
        Show($"Deleted pad {n}");
    }

    private static void TeleportToPad(int n, TeleportationArtifactAbility ability, ArtifactItemController controller)
    {
        if (!PadStore.TryGet(n, out var position, out var yaw))
        {
            Show($"Pad {n} isn't saved yet (Ctrl+{n} to save it)");
            return;
        }
        if (LocalPlayer.IsInCaves)
        {
            Show("Can't teleport to a pad from inside a cave");
            return;
        }

        // Never teleport for free: if the artifact's fuel can't be found, refuse instead
        if (Config.UseFuel.Value && !TrySpendFuel(ability, controller))
            return;

        RLog.Msg($"Teleporting to pad {n} at {position}");
        LocalPlayer.TeleportTo(position, Quaternion.Euler(0f, yaw, 0f));
        try
        {
            ability.StartCoroutine(ability.TriggerFlash());
        }
        catch (Exception e)
        {
            RLog.Msg($"Teleport flash failed (harmless): {e.Message}");
        }
        Show($"Teleported to pad {n}");
    }

    private static bool TrySpendFuel(TeleportationArtifactAbility ability, ArtifactItemController controller)
    {
        var fuel = ability._fuel ?? controller?._fuel;
        if (fuel == null)
        {
            RLog.Warning("Couldn't find the artifact's solafite, refusing to teleport");
            Show("Couldn't read the artifact's solafite");
            return false;
        }

        var cost = ability.FuelCost > 0f ? ability.FuelCost : Config.FallbackFuelCost.Value;
        var before = fuel.CurrentVolume;
        if (before < cost)
        {
            RLog.Msg($"Not enough solafite: have {before}, need {cost}");
            Show("Not enough solafite");
            return false;
        }

        fuel.Empty(cost);
        RLog.Msg($"Solafite {before} -> {fuel.CurrentVolume} (cost {cost}, game's teleport cost {ability.FuelCost})");
        return true;
    }

    private static void Show(string text)
    {
        if (Config.ShowMessage.Value)
            SonsTools.ShowMessage(text, 2.5f);
    }
}
