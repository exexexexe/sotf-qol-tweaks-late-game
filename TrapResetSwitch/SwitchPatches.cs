using Construction;
using HarmonyLib;
using Sons.Electricity;
using UnityEngine;

namespace TrapResetSwitch;

// The switch on a light bulb (or other electric device). OnInteract is the input
// callback that only fires while the player is aiming at this switch, and since the
// input system calls it through a delegate it can't be inlined away like SwitchState.
[HarmonyPatch(typeof(ElectricDeviceSwitch), nameof(ElectricDeviceSwitch.OnInteract))]
internal static class DeviceSwitchPatch
{
    private static void Postfix(ElectricDeviceSwitch __instance)
    {
        var grids = new List<PowerGrid>();
        var structure = __instance.GetComponentInParent<ElectricDeviceStructure>();
        var device = structure != null ? structure.Device : __instance.GetComponentInParent<ElectricDevice>();
        if (device != null && device.PowerGrid != null)
            grids.Add(device.PowerGrid);
        TrapResetter.OnSwitchFlipped(__instance, grids, "device switch");
    }
}

// Inline switch placed on a wire run
[HarmonyPatch(typeof(ElectricWireConnectionSwitch), nameof(ElectricWireConnectionSwitch.OnInteract))]
internal static class WireSwitchPatch
{
    private static void Postfix(ElectricWireConnectionSwitch __instance)
    {
        var grids = new List<PowerGrid>();
        AddGrid(grids, __instance._wireA);
        AddGrid(grids, __instance._wireB);
        TrapResetter.OnSwitchFlipped(__instance, grids, "wire switch");
    }

    private static void AddGrid(List<PowerGrid> grids, Wire wire)
    {
        if (wire != null && wire.PowerGrid != null)
            grids.Add(wire.PowerGrid);
    }
}
