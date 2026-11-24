using RedLoader;
using UnityEngine.InputSystem;

namespace CollectiblesTracker;

public static class Config
{
    public static ConfigCategory Category { get; private set; }

    public static ConfigEntry<string> PanelKey { get; private set; }
    public static ConfigEntry<string> MarkersKey { get; private set; }
    public static ConfigEntry<bool> Markers { get; private set; }
    public static ConfigEntry<bool> ShowFoundMarkers { get; private set; }
    public static ConfigEntry<float> MaxMarkerDistance { get; private set; }
    public static ConfigEntry<float> MarkerScale { get; private set; }
    public static ConfigEntry<int> MarkerIconId { get; private set; }
    public static ConfigEntry<bool> FoundMessage { get; private set; }
    public static ConfigEntry<string> MapKey { get; private set; }
    public static ConfigEntry<string> HiddenItems { get; private set; }

    private static readonly Dictionary<string, ConfigEntry<bool>> ShowCategories = new();

    public static ConfigEntry<bool> ShowCategory(string key) => ShowCategories.TryGetValue(key, out var e) ? e : null;

    public static void Init()
    {
        Category = ConfigSystem.CreateFileCategory("CollectiblesTracker", "CollectiblesTracker", "CollectiblesTracker.cfg");

        PanelKey = Category.CreateEntry(
            "panel_key",
            "F7",
            "Panel key",
            "Opens and closes the list of missing collectibles. Page Up / Page Down switch between achievements while it's open.");

        MarkersKey = Category.CreateEntry(
            "markers_key",
            "F8",
            "Map markers key",
            "Shows or hides the collectible markers on the GPS.");

        Markers = Category.CreateEntry(
            "markers",
            true,
            "Map markers",
            "Put markers on the GPS for notes, blueprints, weapons to plate, clothing and action cameras you're still missing.");

        ShowFoundMarkers = Category.CreateEntry(
            "show_found_markers",
            false,
            "Keep markers for found items",
            "Leave markers on the GPS for things you already have.");

        MaxMarkerDistance = Category.CreateEntry(
            "max_marker_distance",
            0f,
            "Marker range (m)",
            "Only show markers this close to you. 0 shows every marker.");
        MaxMarkerDistance.SetRange(0f, 4000f);

        MarkerScale = Category.CreateEntry(
            "marker_scale",
            1f,
            "Marker size",
            "Size of the markers compared with the game's own GPS markers.");
        MarkerScale.SetRange(0.3f, 2f);

        MarkerIconId = Category.CreateEntry(
            "marker_icon_id",
            -1,
            "Marker icon id",
            "Which GPS icon the markers use. -1 uses the X mark. The log lists the ids the game has.");

        FoundMessage = Category.CreateEntry(
            "found_message",
            true,
            "Message when you find one",
            "Show your new count on screen when you pick up, eat, craft or plate something that counts.");

        MapKey = Category.CreateEntry(
            "map_key",
            "F9",
            "Big map key",
            "Opens and closes a large map of the island with your markers. + and - zoom in around you.");

        // One switch per category that has fixed spots. The F7 panel flips these too (select a category, press Enter).
        foreach (var cat in Data.Categories.Where(c => c.Items.Any(i => i.Locs.Length > 0)))
            ShowCategories[cat.Key] = Category.CreateEntry(
                "show_" + cat.Key,
                true,
                "Show " + cat.Title.ToLowerInvariant(),
                $"Show {cat.Title.ToLowerInvariant()} on the GPS and the big map.");

        HiddenItems = Category.CreateEntry(
            "hidden_items",
            "",
            "Hidden items",
            "Single items hidden from the GPS and big map, set from the F7 panel.",
            true);
    }

    public static Key ParseKey(ConfigEntry<string> entry, Key fallback)
    {
        return Enum.TryParse<Key>(entry.Value?.Trim(), true, out var key) && key != Key.None ? key : fallback;
    }

    // Same as the callback in "CreateSettings". Called when the settings ui is closed.
    public static void OnSettingsUiClosed()
    {
        MapMarkers.Rebuild();
    }
}
