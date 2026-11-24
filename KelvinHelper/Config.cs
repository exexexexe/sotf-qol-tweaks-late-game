using RedLoader;

namespace KelvinHelper;

public static class Config
{
    public static ConfigCategory Category { get; private set; }

    public static ConfigEntry<bool> ThroughTraps { get; private set; }
    public static ConfigEntry<bool> Unstuck { get; private set; }
    public static ConfigEntry<float> StuckSeconds { get; private set; }
    public static ConfigEntry<bool> FireEscape { get; private set; }
    public static ConfigEntry<bool> ResumeTasks { get; private set; }
    public static ConfigEntry<float> ResumeDelay { get; private set; }
    public static ConfigEntry<bool> StayAtBase { get; private set; }
    public static ConfigEntry<bool> ShowMessage { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("KelvinHelper", "KelvinHelper", "KelvinHelper.cfg");

        ThroughTraps = Category.CreateEntry(
            "through_traps",
            true,
            "Walk through traps",
            "Traps no longer block Kelvin's paths and he walks through them. Other AI can also path through traps (and into them).");

        Unstuck = Category.CreateEntry(
            "unstuck",
            true,
            "Unstick Kelvin",
            "Move Kelvin past whatever he's walking into when he hasn't made progress for a while.");

        StuckSeconds = Category.CreateEntry(
            "stuck_seconds",
            4f,
            "Seconds before unsticking",
            "How long Kelvin has to be walking without getting anywhere before he's moved.");
        StuckSeconds.SetRange(2f, 20f);

        FireEscape = Category.CreateEntry(
            "fire_escape",
            true,
            "Pull out of fire",
            "When Kelvin catches fire, move him back to where he was a moment ago.");

        ResumeTasks = Category.CreateEntry(
            "resume_tasks",
            true,
            "Resume tasks",
            "When Kelvin stops a gather/clear/maintain task by himself, give him the same task again.");

        ResumeDelay = Category.CreateEntry(
            "resume_delay",
            15f,
            "Seconds before resuming",
            "How long Kelvin can stand around without a task before his last task is given again.");
        ResumeDelay.SetRange(5f, 120f);

        StayAtBase = Category.CreateEntry(
            "stay_at_base",
            true,
            "Stay instead of following",
            "When Kelvin has nothing to do he stays where he is (e.g. at base) instead of following you. Telling him \"Follow\" on the notepad still works.");

        ShowMessage = Category.CreateEntry(
            "show_message",
            true,
            "Show message",
            "Show a message when the mod helps Kelvin.");
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
    }
}
