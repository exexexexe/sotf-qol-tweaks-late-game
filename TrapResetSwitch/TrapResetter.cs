using RedLoader;
using Sons.Electricity;
using Sons.Gameplay.Traps;
using SonsSdk;
using UnityEngine;

namespace TrapResetSwitch;

internal static class TrapResetter
{
    // Only the player standing at the switch runs the reset; the game syncs
    // each ResetTrap() to the other players like a manual reset.
    private const float PresserDistance = 6f;
    private const float Cooldown = 1f;

    // Shared across hooks: one flip can hit both a switch patch and the device toggle patch
    private static float _lastFlip = -10f;

    public static void OnSwitchFlipped(MonoBehaviour sw, List<PowerGrid> grids, string source)
    {
        try
        {
            var switchPos = sw.transform.position;
            var playerDist = SonsTools.GetPlayerDistance(switchPos);
            RLog.Msg($"[{source}] '{sw.gameObject.name}' at {switchPos}, player {playerDist:F1}m away, {grids.Count} grid(s)");

            if (playerDist > PresserDistance)
            {
                RLog.Msg("  ignored: local player isn't at the switch");
                return;
            }

            if (Time.time - _lastFlip < Cooldown)
            {
                RLog.Msg("  ignored: cooldown");
                return;
            }
            _lastFlip = Time.time;

            if (Config.RequirePower.Value && !grids.Any(g => g.IsConnectedToPower))
            {
                RLog.Msg("  stopped: no power on the switch's circuit");
                Message("Trap reset: switch has no power");
                return;
            }

            var wireColliders = Config.WiredMode.Value ? CollectWireColliders(grids) : null;
            if (wireColliders != null)
                RLog.Msg($"  wired mode: {wireColliders.Count} wire collider(s)");
            if (wireColliders != null && wireColliders.Count == 0)
            {
                RLog.Msg("  no wire colliders found, falling back to radius");
                wireColliders = null;
            }

            var reset = 0;
            var skipped = 0;
            var traps = UnityEngine.Object.FindObjectsOfType<BaseTrapController>();
            RLog.Msg($"  {traps.Count} trap(s) loaded");
            foreach (var trap in traps)
            {
                if (trap == null)
                    continue;
                RLog.Msg($"  trap '{trap.gameObject.name}' {(trap.transform.position - switchPos).magnitude:F1}m away, triggered={trap.IsTriggered}");
                if (!trap.IsTriggered)
                    continue;

                var trapPos = trap.transform.position;
                var inRange = wireColliders != null
                    ? IsNearWire(trapPos, wireColliders, Config.WireReach.Value)
                    : (trapPos - switchPos).sqrMagnitude <= Config.Radius.Value * Config.Radius.Value;
                if (!inRange)
                    continue;

                // Spinning traps that are still running can't be re-armed yet
                if (IsStillRunning(trap))
                {
                    skipped++;
                    continue;
                }

                trap.ResetTrap();
                reset++;
            }

            RLog.Msg($"  reset {reset} trap(s), {skipped} still running");
            var text = $"Reset {reset} trap{(reset == 1 ? "" : "s")}";
            if (skipped > 0)
                text += $" ({skipped} still spinning)";
            Message(text);
        }
        catch (Exception e)
        {
            RLog.Error($"Trap reset failed: {e}");
        }
    }

    private static bool IsStillRunning(BaseTrapController trap)
    {
        var grind = trap.TryCast<GrindTrap>();
        if (grind != null)
            return grind._running;
        var spin = trap.TryCast<SpinTrap>();
        return spin != null && spin._running;
    }

    private static List<Collider> CollectWireColliders(List<PowerGrid> grids)
    {
        var colliders = new List<Collider>();
        foreach (var grid in grids.Distinct())
        {
            var wires = grid._wires;
            if (wires == null)
                continue;
            for (var i = 0; i < wires.Count; i++)
            {
                var wire = wires[i]?.TryCast<Wire>();
                if (wire == null)
                    continue;
                colliders.AddRange(wire.GetComponentsInChildren<Collider>(true));
            }
        }
        return colliders;
    }

    private static bool IsNearWire(Vector3 pos, List<Collider> colliders, float reach)
    {
        var reachSqr = reach * reach;
        foreach (var col in colliders)
        {
            if (col == null)
                continue;
            // ClosestPoint only supports primitives and convex meshes
            var mesh = col.TryCast<MeshCollider>();
            var closest = mesh != null && !mesh.convex ? col.bounds.ClosestPoint(pos) : col.ClosestPoint(pos);
            if ((closest - pos).sqrMagnitude <= reachSqr)
                return true;
        }
        return false;
    }

    private static void Message(string text)
    {
        if (Config.ShowMessage.Value)
            SonsTools.ShowMessage(text, 3f);
    }
}
