using HarmonyLib;
using RedLoader;
using Sons.Gameplay;
using UnityEngine;

namespace KelvinHelper;

// Remembers the last order Kelvin was given so it can be handed to him again when he
// drops it. Both entry points are patched since it's unclear which one survives
// IL2CPP inlining; they record the same thing, so double hits are harmless.
[HarmonyPatch]
internal static class OrderPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Robby), nameof(Robby.ServerGiveOrders))]
    private static void ServerGiveOrders(Robby.OrderType orderType, int subOrderType, int subOrderType2, Transform fromPlayer)
    {
        Record("ServerGiveOrders", orderType, subOrderType, subOrderType2, fromPlayer);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Robby), nameof(Robby.OnGiveOrderServer))]
    private static void OnGiveOrderServer(Robby.OrderType orderType, int subOrderType, int subOrderType2, Transform fromPlayer)
    {
        Record("OnGiveOrderServer", orderType, subOrderType, subOrderType2, fromPlayer);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Robby), nameof(Robby.OnOrderCompleted))]
    private static void OnOrderCompleted(bool restartAfterRest)
    {
        RLog.Msg($"Kelvin finished/stopped his order (restartAfterRest={restartAfterRest})");
        try
        {
            KelvinWatcher.OnOrderCompleted();
        }
        catch (Exception e)
        {
            RLog.Error($"Kelvin order-complete handling failed: {e}");
        }
    }

    private static void Record(string source, Robby.OrderType orderType, int sub, int sub2, Transform fromPlayer)
    {
        try
        {
            KelvinWatcher.OnOrderGiven(source, orderType, sub, sub2, fromPlayer);
        }
        catch (Exception e)
        {
            RLog.Error($"Recording Kelvin order failed: {e}");
        }
    }
}
