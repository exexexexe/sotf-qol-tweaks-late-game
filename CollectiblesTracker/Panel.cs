using TheForest.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CollectiblesTracker;

// The F7 panel: every achievement's count on the left, the selected one's missing items on the right.
// Up/Down move the cursor, Left/Right switch between the two lists, and Enter switches the
// category or item under the cursor on or off for the GPS and the big map.
// Drawn with IMGUI on a 1080p virtual canvas that scales with the screen.
internal static class Panel
{
    public const float VirtualHeight = 1080f;
    public const float Width = 330f + 12f + 500f + 24f; // left list + gap + item list + margin

    private const float Line = 22f;
    private const float Pad = 14f;
    private const float LeftWidth = 330f;
    private const float ListWidth = 500f;
    private const float Swatch = 11f;

    private static readonly Color Bg = new(0.07f, 0.09f, 0.08f, 0.86f);
    private static readonly Color Cursor = new(1f, 1f, 1f, 0.09f);
    public static readonly Color Gold = new(0.85f, 0.65f, 0.28f);
    public static readonly Color Done = new(0.49f, 0.76f, 0.58f);
    public static readonly Color Muted = new(0.62f, 0.68f, 0.65f);
    public static readonly Color Ink = new(0.92f, 0.94f, 0.92f);

    private static int _category;
    private static int _itemCursor;
    private static bool _focusItems;
    private static int _scroll;
    private static GUIStyle _text, _title, _small;

    public static bool Visible { get; set; }

    // The item under the cursor, so the big map can ring it.
    public static (int category, int item)? Focused { get; private set; }

    public static void HandleInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard[Config.ParseKey(Config.PanelKey, Key.F7)].wasPressedThisFrame)
            Visible = !Visible;
        if (keyboard[Config.ParseKey(Config.MarkersKey, Key.F8)].wasPressedThisFrame)
            MapMarkers.ToggleVisible();
        if (keyboard[Config.ParseKey(Config.MapKey, Key.F9)].wasPressedThisFrame)
            BigMap.Visible = !BigMap.Visible;
        if (BigMap.Visible)
            BigMap.HandleInput(keyboard);
        if (!Visible || !Progress.Ready)
        {
            Focused = null;
            return;
        }

        var n = Data.Categories.Length;
        if (keyboard.pageDownKey.wasPressedThisFrame)
            SelectCategory((_category + 1) % n);
        if (keyboard.pageUpKey.wasPressedThisFrame)
            SelectCategory((_category + n - 1) % n);
        if (keyboard.rightArrowKey.wasPressedThisFrame && MissingItems().Count > 0)
            _focusItems = true;
        if (keyboard.leftArrowKey.wasPressedThisFrame)
            _focusItems = false;

        var down = keyboard.downArrowKey.wasPressedThisFrame;
        var up = keyboard.upArrowKey.wasPressedThisFrame;
        if (_focusItems)
        {
            var count = MissingItems().Count;
            if (down) _itemCursor = Math.Min(_itemCursor + 1, count - 1);
            if (up) _itemCursor = Math.Max(_itemCursor - 1, 0);
        }
        else
        {
            if (down) SelectCategory((_category + 1) % n);
            if (up) SelectCategory((_category + n - 1) % n);
        }

        var items = MissingItems();
        if (items.Count == 0)
            _focusItems = false;
        _itemCursor = Math.Clamp(_itemCursor, 0, Math.Max(0, items.Count - 1));
        Focused = _focusItems && items.Count > 0 ? (_category, items[_itemCursor]) : null;

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            ToggleUnderCursor(items);
    }

    private static void SelectCategory(int category)
    {
        _category = category;
        _itemCursor = 0;
        _scroll = 0;
    }

    private static void ToggleUnderCursor(List<int> items)
    {
        var cat = Data.Categories[_category];
        if (!Selection.HasSpots(_category))
        {
            SonsSdk.SonsTools.ShowMessage($"{cat.Title} have no fixed spots, so they're never on the map.", 2.5f);
            return;
        }
        if (_focusItems && items.Count > 0)
        {
            var item = items[_itemCursor];
            if (cat.Items[item].Locs.Length == 0)
                return;
            if (!Selection.CategoryShown(_category))
                Selection.ToggleCategory(_category); // showing one item of a hidden category turns the category back on
            Selection.ToggleItem(_category, item);
        }
        else
        {
            Selection.ToggleCategory(_category);
        }
        MapMarkers.SyncSoon();
    }

    private static List<int> MissingItems()
    {
        var list = new List<int>();
        var cat = Data.Categories[_category];
        for (var i = 0; i < cat.Items.Length; i++)
            if (!Progress.Has(_category, i))
                list.Add(i);
        return list;
    }

    public static void OnGUI()
    {
        if (!Progress.Ready)
            return;
        try
        {
            if (Visible)
                Draw();
            if (BigMap.Visible)
                BigMap.Draw();
        }
        catch (Exception e)
        {
            Visible = false;
            BigMap.Visible = false;
            RedLoader.RLog.Error($"Drawing failed: {e}");
        }
    }

    private static void Draw()
    {
        EnsureStyles();
        var scale = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        var player = LocalPlayer.Transform != null ? LocalPlayer.Transform.position : Vector3.zero;

        // Left: one row per achievement
        var rows = Data.Categories.Length;
        var x = 24f;
        var y = 24f;
        Box(new Rect(x, y, LeftWidth, Pad * 2 + Line * (rows + 8.2f)), Bg);
        var cy = y + Pad;
        Label(x + Pad, cy, LeftWidth, "Collectibles", _title, Gold);
        cy += Line * 1.4f;
        Label(x + Pad, cy, LeftWidth, $"{Config.PanelKey.Value} close · {Config.MapKey.Value} big map · {Config.MarkersKey.Value} GPS markers", _small, Muted);
        cy += Line * 1.3f;
        for (var c = 0; c < rows; c++)
        {
            var cat = Data.Categories[c];
            var got = Progress.Count(c);
            var total = cat.Items.Length;
            if (c == _category)
                Box(new Rect(x + 4f, cy, LeftWidth - 8f, Line), _focusItems ? new Color(1f, 1f, 1f, 0.04f) : Cursor);
            DrawSwatch(x + Pad, cy, c, -1);
            var color = got == total ? Done : c == _category ? Ink : Muted;
            Label(x + Pad + Swatch + 8f, cy, LeftWidth, cat.Title, _text, color);
            Label(x + LeftWidth - Pad - 70f, cy, 70f, $"{got}/{total}", _text, color);
            cy += Line;
        }
        cy += Line * 0.5f;
        Label(x + Pad, cy, LeftWidth - Pad * 2, "Arrows move · Left/Right switch list · Enter show/hide", _small, Muted);
        cy += Line * 1.1f;
        var nearest = Nearest(player);
        Label(x + Pad, cy, LeftWidth - Pad * 2, "Nearest missing (shown on map)", _small, Muted);
        cy += Line * 0.9f;
        Label(x + Pad, cy, LeftWidth - Pad * 2,
            nearest is { } n ? $"{n.item.Label}, {Distance(player, n.loc)}" : "Nothing left on the map", _text, Ink);
        cy += Line;
        if (nearest is { } nn)
            Label(x + Pad, cy, LeftWidth - Pad * 2, nn.loc.Place, _small, Muted);

        DrawMissing(x + LeftWidth + 12f, y, player);
    }

    private static void DrawMissing(float x, float y, Vector3 player)
    {
        var cat = Data.Categories[_category];
        var items = MissingItems();
        var multiGroup = cat.Items.Select(it => it.Group).Distinct().Count() > 1;

        // Rows: group headers (-1) and items; remember which row the cursor is on
        var rows = new List<int>();
        string group = null;
        var cursorRow = 0;
        for (var k = 0; k < items.Count; k++)
        {
            var item = cat.Items[items[k]];
            if (multiGroup && item.Group != group)
                rows.Add(-1 - rows.Count);
            group = item.Group;
            if (k == _itemCursor)
                cursorRow = rows.Count;
            rows.Add(items[k]);
        }

        var maxRows = (int)((VirtualHeight - y - 40f - Pad * 2 - Line * 3) / Line);
        if (cursorRow < _scroll) _scroll = cursorRow;
        if (cursorRow >= _scroll + maxRows) _scroll = cursorRow - maxRows + 1;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, rows.Count - maxRows));
        var shown = Math.Min(rows.Count - _scroll, maxRows);

        var height = Pad * 2 + Line * (2.6f + Math.Max(1, shown));
        Box(new Rect(x, y, ListWidth, height), Bg);
        var cy = y + Pad;
        var got = Progress.Count(_category);
        Label(x + Pad, cy, ListWidth, $"{cat.Title}  <color=#9aa59f>{cat.Achievement} · {got}/{cat.Items.Length}</color>", _title, Gold);
        cy += Line * 1.2f;
        var hint = !Selection.HasSpots(_category) ? "No fixed spots, so not on the map"
            : !Selection.CategoryShown(_category) ? "Hidden from the map (Enter on the left list to show)"
            : _scroll > 0 ? $"{_scroll} more above" : "Missing";
        Label(x + Pad, cy, ListWidth, hint, _small, Muted);
        cy += Line * 1.2f;
        if (items.Count == 0)
        {
            Label(x + Pad, cy, ListWidth, "All found.", _text, Done);
            return;
        }

        group = null;
        for (var r = 0; r < shown; r++)
        {
            var row = rows[_scroll + r];
            var ry = cy + r * Line;
            if (row < 0)
            {
                var next = rows.Skip(_scroll + r + 1).FirstOrDefault(v => v >= 0);
                Label(x + Pad, ry + 3f, ListWidth, cat.Items[next].Group.ToUpperInvariant(), _small, Muted);
                continue;
            }
            var item = cat.Items[row];
            if (_focusItems && items[_itemCursor] == row)
                Box(new Rect(x + 4f, ry, ListWidth - 8f, Line), Cursor);
            if (item.Locs.Length > 0)
                DrawSwatch(x + Pad, ry, _category, row);
            var where = item.Locs.Length > 0
                ? $"{Closest(item, player).Place}, {Distance(player, Closest(item, player))}"
                : item.Where;
            var dim = item.Locs.Length > 0 && !Selection.ItemShown(_category, row);
            Label(x + Pad + Swatch + 8f, ry, ListWidth - Pad * 2 - Swatch,
                $"{item.Label}  <color=#9aa59f>{where}</color>", _text, dim ? Muted : Ink);
        }
        if (_scroll + shown < rows.Count)
            Label(x + Pad, cy + shown * Line, ListWidth, $"{rows.Count - _scroll - shown} more below", _small, Muted);
    }

    // Filled square in the category color when it's on the map, a faint one when it's hidden.
    private static void DrawSwatch(float x, float y, int category, int item)
    {
        var color = Data.MarkerColor(Data.Categories[category].Key);
        if (color == null || !Selection.HasSpots(category))
            return;
        var on = item < 0 ? Selection.CategoryShown(category) : Selection.ItemShown(category, item);
        var c = color.Value;
        Box(new Rect(x, y + (Line - Swatch) / 2f + 1f, Swatch, Swatch), on ? c : new Color(c.r, c.g, c.b, 0.18f));
    }

    private static (Item item, Loc loc)? Nearest(Vector3 player)
    {
        (Item, Loc)? best = null;
        var bestDist = float.MaxValue;
        for (var c = 0; c < Data.Categories.Length; c++)
        {
            var cat = Data.Categories[c];
            for (var i = 0; i < cat.Items.Length; i++)
            {
                if (Progress.Has(c, i) || !Selection.ItemShown(c, i))
                    continue;
                foreach (var loc in cat.Items[i].Locs)
                {
                    var d = Flat(player, loc);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = (cat.Items[i], loc);
                    }
                }
            }
        }
        return best;
    }

    private static Loc Closest(Item item, Vector3 player) => item.Locs.OrderBy(l => Flat(player, l)).First();

    private static float Flat(Vector3 player, Loc loc) => new Vector2(loc.X - player.x, loc.Z - player.z).magnitude;

    public static string Distance(Vector3 player, Loc loc)
    {
        var d = Flat(player, loc);
        if (d < 25f)
            return "right here";
        var deg = Mathf.Atan2(loc.X - player.x, loc.Z - player.z) * Mathf.Rad2Deg;
        var dir = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[(int)Mathf.Repeat(Mathf.Round(deg / 45f), 8f)];
        var dist = d >= 1000f ? $"{d / 1000f:0.0} km" : $"{Mathf.Round(d / 10f) * 10f:0} m";
        return $"{dist} {dir}";
    }

    public static void Box(Rect r, Color color)
    {
        var old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Label(float x, float y, float w, string text, GUIStyle style, Color color)
    {
        style.normal.textColor = color;
        GUI.Label(new Rect(x, y, w, Line + 4f), text, style);
    }

    public static GUIStyle SmallStyle
    {
        get
        {
            EnsureStyles();
            return _small;
        }
    }

    public static GUIStyle TextStyle
    {
        get
        {
            EnsureStyles();
            return _text;
        }
    }

    private static void EnsureStyles()
    {
        if (_text != null)
            return;
        _text = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = false, clipping = TextClipping.Clip };
        _title = new GUIStyle(_text) { fontSize = 19, fontStyle = FontStyle.Bold };
        _small = new GUIStyle(_text) { fontSize = 13 };
    }
}
