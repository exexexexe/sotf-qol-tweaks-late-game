using HarmonyLib;
using Pathfinding;
using RedLoader;
using Sons.Ai.Vail;
using Sons.Crafting.Structures;
using Sons.Gameplay.Traps;
using UnityEngine;

namespace KelvinHelper;

// Lets Kelvin walk through traps. Traps are structures, and structures cut a hole in
// the navmesh, so Kelvin's pathfinding treats every trap as a wall. Turning those cuts
// off gives him straight paths through them; the patch below then stops his collision
// sweep from treating trap parts as obstacles.
[HarmonyPatch]
internal static class TrapPassThrough
{
    private const float ScanInterval = 10f;

    private static float _nextScan;
    private static readonly HashSet<int> LoggedTraps = new();
    private static readonly Dictionary<int, bool> IsTrapCollider = new();
    private static IntPtr _kelvinMover;
    private static bool _loggedPassThrough;

    public static void SetKelvinMover(ActorMover mover)
    {
        _kelvinMover = mover != null ? mover.Pointer : IntPtr.Zero;
    }

    public static void Tick(float now)
    {
        if (!Config.ThroughTraps.Value || now < _nextScan)
            return;
        _nextScan = now + ScanInterval;

        var disabled = 0;
        foreach (var trap in UnityEngine.Object.FindObjectsOfType<BaseTrapController>())
        {
            var root = trap.GetComponentInParent<ScrewStructure>();
            var searchFrom = root != null ? root.gameObject : trap.gameObject;
            foreach (var cut in searchFrom.GetComponentsInChildren<NavmeshCut>(true))
            {
                if (!cut.enabled)
                    continue;
                cut.enabled = false;
                disabled++;
            }
            if (LoggedTraps.Add(trap.GetInstanceID()))
                RLog.Msg($"Trap '{searchFrom.name}' at {trap.transform.position}: navmesh cuts now off");
        }
        if (disabled > 0)
            RLog.Msg($"Turned off {disabled} trap navmesh cut(s) so Kelvin can path through traps");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ActorMover), nameof(ActorMover.CheckSphereTrace))]
    private static void CheckSphereTrace(ActorMover __instance, ref bool __result, ref RaycastHit hitInfo)
    {
        if (!__result || _kelvinMover == IntPtr.Zero || __instance.Pointer != _kelvinMover || !Config.ThroughTraps.Value)
            return;
        try
        {
            var collider = hitInfo.collider;
            if (collider == null || !IsPartOfTrap(collider))
                return;
            __result = false;
            if (!_loggedPassThrough)
            {
                _loggedPassThrough = true;
                RLog.Msg($"Kelvin walked through trap part '{collider.name}' (pass-through patch works)");
            }
        }
        catch (Exception e)
        {
            RLog.Error($"Trap pass-through check failed: {e}");
        }
    }

    private static bool IsPartOfTrap(Collider collider)
    {
        var id = collider.GetInstanceID();
        if (!IsTrapCollider.TryGetValue(id, out var isTrap))
        {
            var root = collider.GetComponentInParent<ScrewStructure>();
            isTrap = collider.GetComponentInParent<BaseTrapController>() != null
                     || (root != null && root.GetComponentInChildren<BaseTrapController>(true) != null);
            IsTrapCollider[id] = isTrap;
        }
        return isTrap;
    }
}
