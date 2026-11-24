using RedLoader;

namespace BlueprintShiftFill;

public static class Config
{
    public static ConfigCategory Category { get; private set; }

    public static ConfigEntry<float> Reach { get; private set; }
    public static ConfigEntry<bool> ShowMessage { get; private set; }
    public static ConfigEntry<bool> ShiftTransferEnabled { get; private set; }
    public static ConfigEntry<int> MaxPerShiftClick { get; private set; }
    public static ConfigEntry<float> RepeatDelay { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("BlueprintShiftFill", "BlueprintShiftFill", "BlueprintShiftFill.cfg");

        Reach = Category.CreateEntry(
            "reach",
            6f,
            "Reach",
            "How far away (metres) the blueprint you're looking at can be.");
        Reach.SetRange(2f, 15f);

        ShowMessage = Category.CreateEntry(
            "show_message",
            true,
            "Show message",
            "Show how many items were added.");

        ShiftTransferEnabled = Category.CreateEntry(
            "shift_transfer",
            true,
            "Shift transfers",
            "Shift-click in the grab bag (shelves, giving items) moves the whole stack; Shift+add/take on stick/stone/log holders moves as many as fit.");

        MaxPerShiftClick = Category.CreateEntry(
            "max_per_shift_click",
            0,
            "Max per shift-click",
            "Most items one shift-click moves. 0 = no limit.");
        MaxPerShiftClick.SetRange(0, 200);

        RepeatDelay = Category.CreateEntry(
            "repeat_delay",
            0.15f,
            "Repeat delay",
            "Seconds between items when shift-clicking in the grab bag. Raise it if items get lost mid-throw.");
        RepeatDelay.SetRange(0.05f, 1f);
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
    }
}
