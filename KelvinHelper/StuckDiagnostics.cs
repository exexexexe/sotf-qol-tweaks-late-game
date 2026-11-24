using Pathfinding;
using RedLoader;
using Sons.Ai.Vail;
using Sons.Crafting.Structures;
using UnityEngine;

namespace KelvinHelper;

// Writes to the log what Kelvin is pushing against when he gets stuck: the collider,
// the structure it belongs to, and whether anything cuts the navmesh there. That tells
// us whether his path goes through a structure because it's missing from the navmesh.
internal static class StuckDiagnostics
{
    private const int MaxRoutePoints = 8;
    private const int MaxBlockers = 6;
    private const float CutSearchRadius = 4f;

    public static void Log(VailActor actor, Transform kelvin, Vector3 pos, List<Vector3> route, int repeats)
    {
        try
        {
            var count = actor._hasPath ? actor.GetPathPointsCount() : 0;
            var shown = route.Skip(1).Take(MaxRoutePoints).Select(p => Fmt(p));
            RLog.Msg($"[stuck] #{repeats + 1} at {Fmt(pos)}, path point {actor._pathIndex}/{count}, " +
                     $"hasPath={actor._hasPath} hasMoveTarget={actor._hasMoveToTarget} goal={Fmt(actor._worldGoalPosition)}");
            RLog.Msg($"[stuck]   route ahead: {string.Join(" -> ", shown)}{(route.Count - 1 > MaxRoutePoints ? " ..." : "")}");

            var dir = route.Count > 1 ? Flat(route[1] - pos).normalized : kelvin.forward;
            var chest = pos + Vector3.up * 1f;
            var seen = new HashSet<int>();
            var logged = 0;
            var contact = chest + dir * 0.7f;

            foreach (var hit in Physics.SphereCastAll(chest, 0.3f, dir, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.collider == null || IsKelvin(hit.collider, kelvin) || !seen.Add(hit.collider.GetInstanceID()))
                    continue;
                if (logged == 0)
                    contact = hit.point;
                if (logged++ < MaxBlockers)
                    LogBlocker(hit.collider, $"ahead at {hit.distance:0.0}m");
            }
            foreach (var col in Physics.OverlapSphere(chest + dir * 0.7f, 0.9f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col == null || IsKelvin(col, kelvin) || !seen.Add(col.GetInstanceID()))
                    continue;
                if (logged++ < MaxBlockers)
                    LogBlocker(col, "touching");
            }
            if (logged == 0)
                RLog.Msg("[stuck]   nothing solid in front of him (blocked by something else, or by his own steering)");

            LogNearbyCuts(contact);
        }
        catch (Exception e)
        {
            RLog.Error($"[stuck] diagnostics failed: {e}");
        }
    }

    private static void LogBlocker(Collider col, string where)
    {
        var structure = col.GetComponentInParent<ScrewStructure>();
        var owner = structure != null ? structure.gameObject : col.transform.root.gameObject;
        var size = col.bounds.size;
        var cuts = owner.GetComponentsInChildren<NavmeshCut>(true);
        var cutText = cuts.Length == 0
            ? "NO navmesh cut"
            : $"{cuts.Length} navmesh cut(s): {string.Join("; ", cuts.Take(3).Select(DescribeCut))}";
        RLog.Msg($"[stuck]   {where}: '{col.name}' ({col.GetType().Name}, layer {LayerMask.LayerToName(col.gameObject.layer)}, " +
                 $"size {size.x:0.0}x{size.y:0.0}x{size.z:0.0}) on '{owner.name}' " +
                 $"{(structure != null ? "[structure]" : "")} - {cutText}");
    }

    // Cuts belonging to other objects can still cover this spot, so list what's nearby too
    private static void LogNearbyCuts(Vector3 contact)
    {
        var near = UnityEngine.Object.FindObjectsOfType<NavmeshCut>()
            .Where(c => c != null && c.enabled && Flat(c.transform.position - contact).magnitude < CutSearchRadius)
            .OrderBy(c => Flat(c.transform.position - contact).magnitude)
            .Take(4)
            .ToList();
        if (near.Count == 0)
        {
            RLog.Msg($"[stuck]   no enabled navmesh cuts within {CutSearchRadius}m of {Fmt(contact)}");
            return;
        }
        foreach (var cut in near)
        {
            var structure = cut.GetComponentInParent<ScrewStructure>();
            var owner = structure != null ? structure.name : cut.transform.root.name;
            RLog.Msg($"[stuck]   nearby cut on '{owner}' {Flat(cut.transform.position - contact).magnitude:0.0}m away: {DescribeCut(cut)}");
        }
    }

    private static string DescribeCut(NavmeshCut cut)
    {
        var shape = cut.type.ToString();
        var size = shape.Contains("Circle") ? $"r={cut.circleRadius:0.0}" : $"{cut.rectangleSize.x:0.0}x{cut.rectangleSize.y:0.0}";
        return $"{shape} {size} h={cut.height:0.0} at {Fmt(cut.transform.position)}{(cut.enabled ? "" : " (off)")}";
    }

    public static bool IsKelvin(Collider col, Transform kelvin)
    {
        return col.transform.IsChildOf(kelvin);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private static string Fmt(Vector3 v) => $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
}
