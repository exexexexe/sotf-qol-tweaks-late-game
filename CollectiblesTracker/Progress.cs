using RedLoader;
using Sons.Player;
using Sons.PlayerActions;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;

namespace CollectiblesTracker;

// Reads the local player's progress from the game's live PlayerState once a second.
// These are the same flags the save file stores, so the counts match the web checker.
internal static class Progress
{
    private const float RefreshInterval = 1f;

    private static bool[][] _have = Data.Categories.Select(c => new bool[c.Items.Length]).ToArray();
    private static float _nextRefresh;
    private static bool _loggedError;

    public static bool Ready { get; private set; }

    public static bool Has(int category, int item) => _have[category][item];

    public static int Count(int category) => _have[category].Count(h => h);

    public static void Reset()
    {
        Ready = false;
        _have = Data.Categories.Select(c => new bool[c.Items.Length]).ToArray();
        _nextRefresh = 0f;
    }

    public static void Tick()
    {
        if (Time.time < _nextRefresh)
            return;
        _nextRefresh = Time.time + RefreshInterval;

        try
        {
            Refresh();
        }
        catch (Exception e)
        {
            if (!_loggedError)
                RLog.Error($"Reading progress failed: {e}");
            _loggedError = true;
        }
    }

    private static void Refresh()
    {
        var state = Sons.Player.PlayerState.GetInstance();
        if (state == null || LocalPlayer.Transform == null)
        {
            Ready = false;
            return;
        }

        var wasReady = Ready;
        for (var c = 0; c < Data.Categories.Length; c++)
        {
            var cat = Data.Categories[c];
            for (var i = 0; i < cat.Items.Length; i++)
            {
                var have = Check(state, cat.Check, cat.Items[i]);
                // Announce new finds, but not everything at once on the first read after loading.
                if (have && !_have[c][i] && wasReady)
                    AnnounceFound(c, i);
                _have[c][i] = have;
            }
        }
        Ready = true;
    }

    private static bool Check(Sons.Player.PlayerState state, Check check, Item item)
    {
        switch (check)
        {
            case global::CollectiblesTracker.Check.HasOwned:
                return Owned(state, item.Id);
            case global::CollectiblesTracker.Check.Blueprint:
                return state.GetBool($"DiscoverablePageUnlocked_{item.Id}", false) || Owned(state, item.Id);
            case global::CollectiblesTracker.Check.IsPlated:
                return state.GetBool($"isPlated_{item.Id:00000000}", false);
            case global::CollectiblesTracker.Check.Consumed:
                return state.HasConsumed(item.Id);
            case global::CollectiblesTracker.Check.Crafted:
                return state.HasCrafted(item.Id);
            case global::CollectiblesTracker.Check.Footage:
                return state.GetBool($"ActionCameraVideoCollected_{item.Internal}", false)
                       || PlayerActionCameraAction.HasAlreadyCollectedClip(item.Internal);
            default:
                return false;
        }
    }

    private static bool Owned(Sons.Player.PlayerState state, int id)
    {
        if (state.GetBool($"hasOwned_{id:00000000}", false))
            return true;
        var inventory = LocalPlayer.Inventory;
        return inventory != null && inventory.Owns(id);
    }

    private static void AnnounceFound(int category, int item)
    {
        var cat = Data.Categories[category];
        RLog.Msg($"Found {cat.Items[item].Label} ({cat.Title})");
        if (!Config.FoundMessage.Value)
            return;
        // The flag being announced isn't counted yet, so add it.
        var got = Count(category) + 1;
        SonsTools.ShowMessage($"{cat.Title}: {got}/{cat.Items.Length}  ·  {cat.Items[item].Label}", 4f);
    }
}
