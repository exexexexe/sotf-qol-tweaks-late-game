namespace CollectiblesTracker;

// What the player chose to show on the GPS and the big map: whole categories, plus single items they hid.
// Both are kept in the config file so the choice survives restarts.
internal static class Selection
{
    private static HashSet<string> _hiddenItems;

    public static bool HasSpots(int category) => Data.Categories[category].Items.Any(i => i.Locs.Length > 0);

    public static bool CategoryShown(int category)
    {
        var entry = Config.ShowCategory(Data.Categories[category].Key);
        return entry == null || entry.Value;
    }

    public static bool ItemShown(int category, int item) => CategoryShown(category) && !Hidden.Contains(Key(category, item));

    public static void ToggleCategory(int category)
    {
        var entry = Config.ShowCategory(Data.Categories[category].Key);
        if (entry == null)
            return;
        entry.Value = !entry.Value;
        Config.Category.SaveToFile(false);
    }

    public static void ToggleItem(int category, int item)
    {
        var key = Key(category, item);
        if (!Hidden.Remove(key))
            Hidden.Add(key);
        Config.HiddenItems.Value = string.Join(",", Hidden.OrderBy(k => k));
        Config.Category.SaveToFile(false);
    }

    // Visible on the GPS and big map: chosen, and still missing unless found ones are kept.
    public static bool ShowMarker(int category, int item) =>
        ItemShown(category, item) && (Config.ShowFoundMarkers.Value || !Progress.Has(category, item));

    private static HashSet<string> Hidden => _hiddenItems ??= new HashSet<string>(
        (Config.HiddenItems.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    // Category key plus id, since one item can be in two achievements (the crafted club).
    private static string Key(int category, int item) => $"{Data.Categories[category].Key}:{Data.Categories[category].Items[item].Id}";
}
