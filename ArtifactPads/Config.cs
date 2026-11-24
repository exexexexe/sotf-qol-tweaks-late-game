using RedLoader;

namespace ArtifactPads;

public static class Config
{
    public static ConfigCategory Category { get; private set; }

    public static ConfigEntry<bool> UseFuel { get; private set; }
    public static ConfigEntry<float> FallbackFuelCost { get; private set; }
    public static ConfigEntry<bool> ShowMessage { get; private set; }

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("ArtifactPads", "ArtifactPads", "ArtifactPads.cfg");

        UseFuel = Category.CreateEntry(
            "use_fuel",
            true,
            "Use solafite",
            "Teleporting to a pad costs the same solafite as the normal artifact teleport.");

        FallbackFuelCost = Category.CreateEntry(
            "fallback_fuel_cost",
            1f,
            "Fallback solafite cost",
            "Solafite used per pad teleport if the game's own teleport cost can't be read.");
        FallbackFuelCost.SetRange(0.1f, 10f);

        ShowMessage = Category.CreateEntry(
            "show_message",
            true,
            "Show message",
            "Show a message when a pad is saved or used.");
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
    }
}
