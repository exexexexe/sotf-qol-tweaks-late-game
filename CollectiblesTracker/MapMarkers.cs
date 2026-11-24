using RedLoader;
using Sons.Gameplay.GPS;
using TheForest.Utils;
using UnityEngine;

namespace CollectiblesTracker;

// Puts a GPS marker on every spot of a missing collectible. The GPS draws every active GPSLocator,
// so each marker is our own GameObject with a GPSLocator whose settings are copied from one of the game's.
// Each category gets its own copy of the icon list, recolored to the category's color.
internal static class MapMarkers
{
    private const float RetryInterval = 5f;
    private const float SyncInterval = 1f;

    private sealed class Marker
    {
        public int Category, Item;
        public Loc Loc;
        public GameObject Go;
        public GPSLocator Locator;
    }

    private static readonly List<Marker> Markers = new();
    private static bool _built;
    private static bool _shown = true;
    private static float _nextTry, _nextSync;
    private static bool _loggedNoTemplate, _loggedIcons;

    public static void Reset()
    {
        // Our GameObjects live in the game scene, so they are gone already after a load or quit.
        Markers.Clear();
        _built = false;
        _nextTry = 0f;
    }

    public static void Rebuild()
    {
        foreach (var m in Markers)
            if (m.Go != null)
                UnityEngine.Object.Destroy(m.Go);
        Reset();
    }

    // Apply a change from the F7 panel on the next frame instead of within a second.
    public static void SyncSoon() => _nextSync = 0f;

    public static void ToggleVisible()
    {
        _shown = !_shown;
        _nextSync = 0f;
        SonsSdk.SonsTools.ShowMessage(_shown ? "Collectible markers on" : "Collectible markers off", 2f);
    }

    public static void Tick()
    {
        if (!Progress.Ready)
            return;
        try
        {
            if (!_built)
            {
                if (!Config.Markers.Value || Time.time < _nextTry)
                    return;
                _nextTry = Time.time + RetryInterval;
                Build();
                return;
            }
            if (Time.time < _nextSync)
                return;
            _nextSync = Time.time + SyncInterval;
            Sync();
        }
        catch (Exception e)
        {
            RLog.Error($"Map markers failed: {e}");
            _built = true; // don't retry every few seconds and flood the log
            Markers.Clear();
        }
    }

    private static void Build()
    {
        var template = FindTemplate();
        if (template == null)
        {
            if (!_loggedNoTemplate)
                RLog.Msg("No GPS marker in the world to copy yet, will keep looking.");
            _loggedNoTemplate = true;
            return;
        }

        var icons = template._locatorIcons;
        LogIcons(icons);
        var iconId = PickIconId(template, icons);

        for (var c = 0; c < Data.Categories.Length; c++)
        {
            var cat = Data.Categories[c];
            var color = Data.MarkerColor(cat.Key);
            if (color == null || cat.Items.All(i => i.Locs.Length == 0))
                continue;
            var catIcons = RecoloredIcons(icons, iconId, color.Value);
            for (var i = 0; i < cat.Items.Length; i++)
                foreach (var loc in cat.Items[i].Locs)
                    Markers.Add(Create(template, catIcons, iconId, c, i, loc));
        }
        _built = true;
        _nextSync = 0f;
        RLog.Msg($"Created {Markers.Count} GPS markers (copied from '{template.gameObject.name}', icon {iconId}).");
    }

    private static GPSLocator FindTemplate()
    {
        GPSLocator fallback = null;
        foreach (var l in Resources.FindObjectsOfTypeAll<GPSLocator>())
        {
            if (l == null || l._locatorIcons == null || l._locatorIcons._iconDataList == null || l._locatorIcons._iconDataList.Count == 0)
                continue;
            if (l.gameObject.name.StartsWith("CollectiblesTracker_"))
                continue;
            // Prefer a plain pin, like the ones the player places with GPS locators.
            if (l._uiStyle == GPSLocator.Style.Pin)
                return l;
            fallback ??= l;
        }
        return fallback;
    }

    private static int PickIconId(GPSLocator template, GPSLocatorIcons icons)
    {
        // Default to the game's X mark (id 10 in the GPS icon list); the config can pick any other id.
        var wanted = Config.MarkerIconId.Value >= 0 ? Config.MarkerIconId.Value : 10;
        int? firstCustom = null;
        foreach (var d in icons._iconDataList)
        {
            if (d == null)
                continue;
            if (d._id == wanted)
                return wanted;
            if (d._allowedCustomGpsChoice && firstCustom == null)
                firstCustom = d._id;
        }
        return firstCustom ?? template._currentLocatorIconId;
    }

    private static GPSLocatorIcons RecoloredIcons(GPSLocatorIcons source, int iconId, Color color)
    {
        var copy = UnityEngine.Object.Instantiate(source);
        copy.hideFlags = HideFlags.HideAndDontSave;
        foreach (var d in copy._iconDataList)
        {
            if (d == null || d._id != iconId)
                continue;
            d._iconColor = color;
            d._backgroundColor = new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 1f);
        }
        return copy;
    }

    private static Marker Create(GPSLocator template, GPSLocatorIcons icons, int iconId, int category, int item, Loc loc)
    {
        var go = new GameObject($"CollectiblesTracker_{Data.Categories[category].Items[item].Id}");
        // Inactive while setting up, so the locator registers with the GPS only once it's configured
        go.SetActive(false);
        go.transform.position = new Vector3(loc.X, 300f, loc.Z);
        var l = go.AddComponent<GPSLocator>();
        l._locatorIcons = icons;
        l._currentLocatorIconId = iconId;
        l._uiStyle = template._uiStyle;
        l._iconScale = template._iconScale * Config.MarkerScale.Value;
        l._usePlayerAreaMask = false;
        l._disableBelowTerrain = false;
        l._hasMaxVisualRange = false;
        l._pulseIcon = false;
        l._constrainToScreen = false;
        l._recordMovementHistory = false;
        l._shouldBeepWhenInRange = false;
        l._excludeFromModuleRefresh = true;
        l._deactivateTrackerOnDisable = false;
        l._isActive = true;
        return new Marker { Category = category, Item = item, Loc = loc, Go = go, Locator = l };
    }

    private static void Sync()
    {
        var player = LocalPlayer.Transform != null ? LocalPlayer.Transform.position : Vector3.zero;
        var maxDist = Config.MaxMarkerDistance.Value;
        foreach (var m in Markers)
        {
            if (m.Go == null)
            {
                // The scene was unloaded under us; build again next time.
                Reset();
                return;
            }
            var show = Config.Markers.Value && _shown
                       && Selection.ShowMarker(m.Category, m.Item)
                       && (maxDist <= 0f || new Vector2(m.Loc.X - player.x, m.Loc.Z - player.z).magnitude <= maxDist);
            if (m.Go.activeSelf != show)
                m.Go.SetActive(show);
            if (show)
                EnsureRegistered(m.Locator);
        }
    }

    // GPSLocator registers itself with the GPS when enabled. In case that only happens for locators
    // that exist when a GPS starts, add ours to every GPS that doesn't have it yet.
    private static void EnsureRegistered(GPSLocator locator)
    {
        var trackers = GPSTrackerSystem.GpsTrackers;
        if (trackers == null)
            return;
        foreach (var tracker in trackers)
        {
            if (tracker == null || tracker._gpsLocators == null)
                continue;
            if (!tracker._gpsLocators.Contains(locator))
                tracker.AddGpsLocator(locator);
        }
    }

    private static void LogIcons(GPSLocatorIcons icons)
    {
        if (_loggedIcons)
            return;
        _loggedIcons = true;
        var parts = new List<string>();
        foreach (var d in icons._iconDataList)
            if (d != null)
                parts.Add($"{d._id}{(d._icon != null ? "=" + d._icon.name : "")}{(d._allowedCustomGpsChoice ? "*" : "")}");
        RLog.Msg($"GPS icon ids (* = player-selectable): {string.Join(", ", parts)}");
    }
}
