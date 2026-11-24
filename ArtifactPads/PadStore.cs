using System.Globalization;
using RedLoader;
using Sons.Gameplay.GameSetup;
using UnityEngine;

namespace ArtifactPads;

// Saved pad positions, one file per save game so each world has its own pads.
// Plain "number x y z yaw" lines to keep the file easy to read and edit by hand.
internal static class PadStore
{
    public const int MaxPads = 9;

    private static readonly Vector3?[] Positions = new Vector3?[MaxPads + 1];
    private static readonly float[] Yaws = new float[MaxPads + 1];
    private static string _file;

    public static void Load()
    {
        Array.Clear(Positions);
        Array.Clear(Yaws);

        var dir = Path.Combine(Path.GetDirectoryName(typeof(PadStore).Assembly.Location)!, "ArtifactPads");
        Directory.CreateDirectory(dir);
        var saveKey = $"{GameSetupManager.GetSaveGameType()}_{GameSetupManager.GetSelectedSaveId()}";
        _file = Path.Combine(dir, $"pads_{saveKey}.txt");
        RLog.Msg($"Pads file: {_file}");
        if (!File.Exists(_file))
            return;

        foreach (var line in File.ReadAllLines(_file))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5 || !int.TryParse(parts[0], out var n) || n < 1 || n > MaxPads)
                continue;
            Positions[n] = new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));
            Yaws[n] = Parse(parts[4]);
        }
    }

    public static bool TryGet(int n, out Vector3 position, out float yaw)
    {
        position = Positions[n] ?? default;
        yaw = Yaws[n];
        return Positions[n].HasValue;
    }

    public static void Set(int n, Vector3 position, float yaw)
    {
        Positions[n] = position;
        Yaws[n] = yaw;
        Save();
    }

    public static void Remove(int n)
    {
        Positions[n] = null;
        Save();
    }

    public static string Summary()
    {
        var saved = Enumerable.Range(1, MaxPads).Where(n => Positions[n].HasValue).ToList();
        return saved.Count == 0 ? "No pads saved" : "Pads: " + string.Join(", ", saved);
    }

    private static void Save()
    {
        if (_file == null)
            return;
        var lines = Enumerable.Range(1, MaxPads)
            .Where(n => Positions[n].HasValue)
            .Select(n =>
            {
                var p = Positions[n]!.Value;
                return string.Join(" ", n.ToString(), Fmt(p.x), Fmt(p.y), Fmt(p.z), Fmt(Yaws[n]));
            });
        File.WriteAllLines(_file, lines);
    }

    private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    private static string Fmt(float f) => f.ToString("R", CultureInfo.InvariantCulture);
}
