using System.Reflection;
using TheForest.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CollectiblesTracker;

// F9: a large map of the island (a terrain render bundled in the dll) with the same markers as the GPS.
// The image covers world x/z -2000..2000 with north up. + and - zoom in around the player.
// When the F7 panel is open the map moves to the right of it, and rings the item under the panel's cursor.
internal static class BigMap
{
    private const float WorldHalf = 2000f;
    private static readonly int[] Zooms = { 1, 2, 4, 8 };
    private static readonly Color Frame = new(0.07f, 0.09f, 0.08f, 0.92f);

    private static int _zoom;
    private static Texture2D _map, _dot, _diamond, _ring;
    private static bool _loadFailed;

    public static bool Visible { get; set; }

    public static void HandleInput(Keyboard keyboard)
    {
        if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
            _zoom = Math.Min(_zoom + 1, Zooms.Length - 1);
        if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
            _zoom = Math.Max(_zoom - 1, 0);
    }

    public static void Draw()
    {
        if (!EnsureTextures())
            return;
        var scale = Screen.height / Panel.VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        var vw = Screen.width / scale;
        var vh = Panel.VirtualHeight;

        // Square map, beside the panel when it's open
        var left = Panel.Visible ? Panel.Width + 24f : 24f;
        var size = Mathf.Max(360f, Mathf.Min(vh - 48f - 34f, vw - left - 24f));
        var x = Panel.Visible ? Mathf.Max(left, vw - size - 24f) : (vw - size) / 2f;
        var y = (vh - size - 34f) / 2f;
        Panel.Box(new Rect(x - 6f, y - 6f, size + 12f, size + 12f + 34f), Frame);

        // Visible part of the world: the whole island, or a window around the player when zoomed
        var player = LocalPlayer.Transform != null ? LocalPlayer.Transform.position : Vector3.zero;
        var half = WorldHalf / Zooms[_zoom];
        var cx = _zoom == 0 ? 0f : Mathf.Clamp(player.x, -WorldHalf + half, WorldHalf - half);
        var cz = _zoom == 0 ? 0f : Mathf.Clamp(player.z, -WorldHalf + half, WorldHalf - half);
        var uv = new Rect((cx - half + WorldHalf) / (2f * WorldHalf), (cz - half + WorldHalf) / (2f * WorldHalf), half / WorldHalf, half / WorldHalf);
        GUI.DrawTextureWithTexCoords(new Rect(x, y, size, size), _map, uv);

        Vector2 ToScreen(float wx, float wz) => new(x + (wx - (cx - half)) / (2f * half) * size, y + ((cz + half) - wz) / (2f * half) * size);
        bool Inside(Vector2 p) => p.x >= x && p.x <= x + size && p.y >= y && p.y <= y + size;

        // Markers: circles above ground, diamonds underground, faded when already found
        var markerSize = Mathf.Lerp(12f, 18f, _zoom / (float)(Zooms.Length - 1));
        var labels = _zoom >= 2;
        for (var c = 0; c < Data.Categories.Length; c++)
        {
            var cat = Data.Categories[c];
            var color = Data.MarkerColor(cat.Key);
            if (color == null)
                continue;
            for (var i = 0; i < cat.Items.Length; i++)
            {
                if (!Selection.ShowMarker(c, i))
                    continue;
                var have = Progress.Has(c, i);
                foreach (var loc in cat.Items[i].Locs)
                {
                    var p = ToScreen(loc.X, loc.Z);
                    if (!Inside(p))
                        continue;
                    var tint = color.Value;
                    tint.a = have ? 0.4f : 1f;
                    Icon(loc.Underground ? _diamond : _dot, p, markerSize + 3f, new Color(0.06f, 0.08f, 0.06f, tint.a));
                    Icon(loc.Underground ? _diamond : _dot, p, markerSize, tint);
                    if (labels && !have)
                        Panel.Label(p.x + markerSize * 0.7f, p.y - 11f, 260f, cat.Items[i].Label, Panel.SmallStyle, Color.white);
                }
            }
        }

        // The item under the F7 panel's cursor gets a ring (even if it's hidden or off this part of the map)
        if (Panel.Focused is { } f)
            foreach (var loc in Data.Categories[f.category].Items[f.item].Locs)
            {
                var p = ToScreen(loc.X, loc.Z);
                if (Inside(p))
                    Icon(_ring, p, 40f + 6f * Mathf.Sin(Time.unscaledTime * 6f), Color.white);
            }

        // You: white dot with a short tick showing which way you face
        var me = ToScreen(player.x, player.z);
        if (Inside(me))
        {
            var fwd = LocalPlayer.Transform != null ? LocalPlayer.Transform.forward : Vector3.forward;
            var dir = new Vector2(fwd.x, -fwd.z).normalized;
            for (var k = 1; k <= 3; k++)
                Icon(_dot, me + dir * (6f + k * 5f), 6f, Color.white);
            Icon(_dot, me, 15f, new Color(0.06f, 0.08f, 0.06f));
            Icon(_dot, me, 11f, Color.white);
            Panel.Label(me.x + 10f, me.y - 11f, 60f, "You", Panel.TextStyle, Color.white);
        }

        // Footer: what's shown and the keys
        var fy = y + size + 7f;
        var fx = x + 4f;
        for (var c = 0; c < Data.Categories.Length; c++)
        {
            var color = Data.MarkerColor(Data.Categories[c].Key);
            if (color == null || !Selection.CategoryShown(c))
                continue;
            Icon(_dot, new Vector2(fx + 6f, fy + 11f), 11f, color.Value);
            var missing = Data.Categories[c].Items.Length - Progress.Count(c);
            var text = $"{Data.Categories[c].Title} {missing}";
            Panel.Label(fx + 15f, fy, 200f, text, Panel.SmallStyle, Panel.Ink);
            fx += 15f + Panel.SmallStyle.CalcSize(new GUIContent(text)).x + 14f;
        }
        var keys = $"{Config.MapKey.Value} close · +/- zoom ({Zooms[_zoom]}x)";
        var kw = Panel.SmallStyle.CalcSize(new GUIContent(keys)).x;
        Panel.Label(x + size - kw - 4f, fy, kw + 8f, keys, Panel.SmallStyle, Panel.Muted);
    }

    private static void Icon(Texture2D tex, Vector2 center, float size, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), tex);
        GUI.color = old;
    }

    private static bool EnsureTextures()
    {
        if (_map != null)
            return true;
        if (_loadFailed)
            return false;
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CollectiblesTracker.map.jpg");
            using var ms = new MemoryStream();
            stream!.CopyTo(ms);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(tex, ms.ToArray()))
                throw new Exception("LoadImage returned false");
            _dot = Shape((dx, dy) => dx * dx + dy * dy <= 1f);
            _diamond = Shape((dx, dy) => Mathf.Abs(dx) + Mathf.Abs(dy) <= 1f);
            _ring = Shape((dx, dy) => { var r = dx * dx + dy * dy; return r <= 1f && r >= 0.72f; });
            _map = tex;
            return true;
        }
        catch (Exception e)
        {
            _loadFailed = true;
            Visible = false;
            RedLoader.RLog.Error($"Couldn't load the map image: {e}");
            return false;
        }
    }

    // A white 64x64 shape with a soft edge, tinted with GUI.color when drawn.
    private static Texture2D Shape(Func<float, float, bool> inside)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
        for (var py = 0; py < n; py++)
            for (var px = 0; px < n; px++)
            {
                // 4 samples per pixel for a smooth edge
                var hits = 0;
                for (var s = 0; s < 4; s++)
                {
                    var dx = (px + 0.25f + 0.5f * (s % 2)) / n * 2f - 1f;
                    var dy = (py + 0.25f + 0.5f * (s / 2)) / n * 2f - 1f;
                    if (inside(dx, dy)) hits++;
                }
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, hits / 4f));
            }
        tex.Apply();
        return tex;
    }
}
