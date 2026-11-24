using RedLoader;

namespace TrapResetSwitch;

public static class Config
{
    public static ConfigCategory Category { get; private set; }

    public static ConfigEntry<bool> WiredMode { get; private set; }
    public static ConfigEntry<float> Radius { get; private set; }
    public static ConfigEntry<float> WireReach { get; private set; }
    public static ConfigEntry<bool> RequirePower { get; private set; }
    public static ConfigEntry<bool> ShowMessage { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("TrapResetSwitch", "TrapResetSwitch", "TrapResetSwitch.cfg");

        WiredMode = Category.CreateEntry(
            "wired_mode",
            false,
            "Wired mode",
            "Only reset traps that a wire connected to the switch runs past. When off, every trap in the radius is reset.");

        Radius = Category.CreateEntry(
            "radius",
            50f,
            "Reset radius",
            "Traps within this many metres of the switch are reset (ignored in wired mode).");
        Radius.SetRange(5f, 200f);

        WireReach = Category.CreateEntry(
            "wire_reach",
            4f,
            "Wire reach",
            "Wired mode: how close (metres) a trap has to be to a connected wire.");
        WireReach.SetRange(1f, 15f);

        RequirePower = Category.CreateEntry(
            "require_power",
            true,
            "Require power",
            "The switch's circuit needs a battery or solar panel for the reset to work.");

        ShowMessage = Category.CreateEntry(
            "show_message",
            true,
            "Show message",
            "Show how many traps were reset.");
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
    }
}
