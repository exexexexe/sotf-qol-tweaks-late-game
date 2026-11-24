using UnityEngine;

namespace CollectiblesTracker;

// How the game records that an item counts toward its achievement.
internal enum Check
{
    HasOwned,   // hasOwned_<id> (notes, clothing)
    Blueprint,  // DiscoverablePageUnlocked_<id>
    IsPlated,   // isPlated_<id>
    Consumed,   // consumed.<id>
    Crafted,    // crafted.<id> (crafted and 3D-printed items)
    Footage,    // ActionCameraVideoCollected_<name>
}

// World position (x/z) of one pickup, the place name shown to the player, and whether it's underground.
internal sealed record Loc(float X, float Z, string Place, bool Underground);

internal sealed record Item(int Id, string Label, string Internal, string Group, string Where, Loc[] Locs);

internal sealed record Category(string Key, string Title, string Achievement, Check Check, Item[] Items);

internal static partial class Data
{
    // Marker colors, the same as the web checker's map.
    public static Color? MarkerColor(string categoryKey) => categoryKey switch
    {
        "notes" => new Color(0.95f, 0.95f, 0.92f),
        "blueprints" => new Color(0.37f, 0.69f, 0.90f),
        "plated" => new Color(0.91f, 0.70f, 0.24f),
        "clothing" => new Color(0.78f, 0.49f, 0.84f),
        "footage" => new Color(0.94f, 0.42f, 0.34f),
        _ => null,
    };
}
