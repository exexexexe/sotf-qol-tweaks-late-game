using RedLoader;
using Sons.Ai.Vail;
using Sons.Gameplay;
using SonsSdk;
using TheForest.Utils;
using UnityEngine;

namespace KelvinHelper;

// Watches Kelvin a few times a second and patches over his worst habits:
// walking into things forever, standing in fire, and dropping tasks by himself.
// Kelvin's AI only runs on the host, so on a client this does nothing.
internal static class KelvinWatcher
{
    private const float TickInterval = 0.5f;
    private const float MovedEnough = 0.5f;
    private const float CloseToTarget = 1.5f;
    private const float RouteSampleStep = 0.5f;
    private const float MaxHeightChange = 1f;
    private const float RepeatSeconds = 30f;
    private const float RepeatRadius = 3f;
    private const float PlayerFallbackRange = 40f;
    private const float GoalJumpRange = 30f;
    private const float HistorySeconds = 4f;
    private const float QuickFailSeconds = 60f;
    private const int MaxQuickFails = 3;

    private static readonly Robby.OrderType[] ResumableOrders =
    {
        Robby.OrderType.Get, Robby.OrderType.ClearArea, Robby.OrderType.MaintainBase,
    };

    private static float _nextTick;
    private static float _nextSearch;
    private static Robby _robby;

    // Unstuck state
    private static Vector3 _anchorPos;
    private static float _anchorTime;
    private static float _unstuckCooldownUntil;
    private static Vector3 _lastStuckPos;
    private static float _lastStuckTime = -1000f;
    private static int _stuckRepeats;

    // Fire state: recent positions so he can be put back somewhere he wasn't burning
    private static readonly Queue<(float time, Vector3 pos)> History = new();
    private static float _fireCooldownUntil;

    // Resume state
    private static bool _hasOrder;
    private static Robby.OrderType _orderType;
    private static int _orderSub, _orderSub2;
    private static Transform _orderFrom;
    private static bool _reissuing;
    private static float _completedAt = -1f;
    private static float _lastResumeTime = -1000f;

    // Stay state: he only follows when the player asked for it
    private static bool _playerWantsFollow;
    private static bool _autoStayed;
    private static float _idleSince = -1f;

    // Diagnostics: how long he's been standing still, for the log
    private static float _nextStillLog;
    private static int _quickFails;

    public static void OnOrderGiven(string source, Robby.OrderType type, int sub, int sub2, Transform from)
    {
        if (_reissuing)
            return;
        RLog.Msg($"Kelvin given order {type} ({sub}, {sub2}) via {source}");
        _quickFails = 0;
        _completedAt = -1f;
        _playerWantsFollow = type == Robby.OrderType.Follow;
        _autoStayed = false;
        if (ResumableOrders.Contains(type))
        {
            _hasOrder = true;
            _orderType = type;
            _orderSub = sub;
            _orderSub2 = sub2;
            _orderFrom = from;
        }
        else
        {
            // Follow / stay / break / build: the player decided, don't override it
            _hasOrder = false;
        }
    }

    // The game's own "order finished" callback; he may still look busy afterwards,
    // so resuming is timed from here rather than from his order state
    public static void OnOrderCompleted()
    {
        // Whatever he was doing is over, so he may start following again
        _autoStayed = false;
        if (!_hasOrder)
            return;
        var now = Time.time;
        _completedAt = now;
        if (now - _lastResumeTime < QuickFailSeconds)
            _quickFails++;
    }

    public static void OnUpdate()
    {
        var now = Time.time;
        if (now < _nextTick)
            return;
        _nextTick = now + TickInterval;

        try
        {
            var robby = FindKelvin(now);
            if (robby == null)
                return;
            var actor = robby._actor;
            if (actor == null || actor.IsDead() || actor.IsRobbyInjured())
                return;

            TrapPassThrough.SetKelvinMover(actor._actorMover);
            TrapPassThrough.Tick(now);

            var pos = actor.Position();
            History.Enqueue((now, pos));
            while (History.Count > 0 && now - History.Peek().time > HistorySeconds)
                History.Dequeue();

            if (Config.FireEscape.Value)
                CheckFire(actor, pos, now);
            if (Config.Unstuck.Value)
                CheckStuck(robby, actor, pos, now);
            if (Config.ResumeTasks.Value)
                CheckResume(robby, now);
            if (Config.StayAtBase.Value)
                CheckStay(robby, now);
            LogIfStandingStill(robby, actor, now);
        }
        catch (Exception e)
        {
            RLog.Error($"Kelvin helper failed: {e}");
            _nextTick = now + 5f;
        }
    }

    private static Robby FindKelvin(float now)
    {
        if (_robby == null && now >= _nextSearch)
        {
            _nextSearch = now + 5f;
            _robby = UnityEngine.Object.FindObjectOfType<Robby>();
            if (_robby != null)
                RLog.Msg("Found Kelvin");
        }
        if (_robby == null)
            return null;

        var entity = _robby.entity;
        if (entity != null && entity.isAttached && !entity.isOwner)
            return null;
        return _robby;
    }

    private static void CheckStuck(Robby robby, VailActor actor, Vector3 pos, float now)
    {
        if ((pos - _anchorPos).sqrMagnitude > MovedEnough * MovedEnough)
        {
            _anchorPos = pos;
            _anchorTime = now;
            return;
        }

        var tryingToMove = actor._hasMoveToTarget || actor._hasPath;
        if (!tryingToMove || robby._interactingPlayer != null)
        {
            _anchorTime = now;
            return;
        }
        if (now - _anchorTime < Config.StuckSeconds.Value || now < _unstuckCooldownUntil)
            return;

        var route = RouteAhead(actor, pos);
        var goal = route[route.Count - 1];
        if (Flat(goal - pos).magnitude < CloseToTarget)
        {
            // Standing next to what he's working on, e.g. chopping. Not stuck.
            _anchorTime = now;
            return;
        }

        // Stuck at the same place again soon after a nudge: the last nudge wasn't enough
        var repeat = now - _lastStuckTime < RepeatSeconds && (pos - _lastStuckPos).sqrMagnitude < RepeatRadius * RepeatRadius;
        _stuckRepeats = repeat ? _stuckRepeats + 1 : 0;
        _lastStuckPos = pos;
        _lastStuckTime = now;

        var kelvin = actor.transform;
        StuckDiagnostics.Log(actor, kelvin, pos, route, _stuckRepeats);

        string how;
        Vector3 spot;
        if (_stuckRepeats <= 1 && TryPastObstacle(kelvin, pos, route, _stuckRepeats == 0 ? 6f : 12f, out spot))
            how = _stuckRepeats == 0 ? "past the obstacle" : "further along his path";
        else if (_stuckRepeats <= 2 && (goal - pos).sqrMagnitude < GoalJumpRange * GoalJumpRange && TryNear(kelvin, goal, out spot))
            how = "at where he was going";
        else if (TryNearPlayer(kelvin, pos, out spot))
            how = "next to the player";
        else if (TryPastObstacle(kelvin, pos, route, 12f, out spot))
            how = "further along his path";
        else
        {
            RLog.Msg($"Kelvin stuck at {pos} but found no free spot towards {goal}");
            _anchorTime = now;
            return;
        }

        var look = Flat(goal - spot);
        actor.SetPositionAndRotation(spot, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Flat(spot - pos)), false);
        RLog.Msg($"Kelvin stuck for {now - _anchorTime:0.0}s at {pos} (time #{_stuckRepeats + 1} here), moved him {how} to {spot} (heading to {goal})");
        Show(_stuckRepeats == 0 ? "Kelvin was stuck - moved him past it" : "Kelvin stuck again - moved him further");
        if (_stuckRepeats >= 3)
            _stuckRepeats = 0;
        _anchorPos = spot;
        _anchorTime = now;
        _unstuckCooldownUntil = now + 2f;
    }

    // Where he's still headed: his position, then the path points he hasn't reached yet
    private static List<Vector3> RouteAhead(VailActor actor, Vector3 pos)
    {
        var route = new List<Vector3> { pos };
        var count = actor._hasPath ? actor.GetPathPointsCount() : 0;
        for (var i = Mathf.Max(actor._pathIndex, 0); i < count; i++)
            route.Add(actor.GetPathPointAtIndex(i));
        if (route.Count == 1)
            route.Add(actor._hasPath ? actor._activePathTargetPos : actor._worldGoalPosition);
        return route;
    }

    // Walks along his route and takes the first free ground-level spot beyond whatever
    // he's walking into, so a thick wall gets skipped instead of him landing against it
    private static bool TryPastObstacle(Transform kelvin, Vector3 pos, List<Vector3> route, float maxDistance, out Vector3 spot)
    {
        spot = default;
        var first = SampleRoute(route, 1f, 1f).FirstOrDefault();
        var dir = Flat(first.point - pos);
        var minDistance = 1.5f;
        if (dir.sqrMagnitude > 0.01f && TryFirstHit(kelvin, pos + Vector3.up, dir.normalized, 4f, out var blocked))
            minDistance = Mathf.Max(minDistance, blocked + 0.8f);

        foreach (var (point, distance) in SampleRoute(route, RouteSampleStep, maxDistance))
        {
            if (distance < minDistance)
                continue;
            if (TryFindStandingSpot(kelvin, point, out spot))
                return true;
        }
        return false;
    }

    private static bool TryNear(Transform kelvin, Vector3 point, out Vector3 spot)
    {
        foreach (var offset in new[] { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
        {
            if (TryFindStandingSpot(kelvin, point + offset * 1.2f, out spot))
                return true;
        }
        spot = default;
        return false;
    }

    private static bool TryNearPlayer(Transform kelvin, Vector3 pos, out Vector3 spot)
    {
        spot = default;
        var player = LocalPlayer.Transform;
        if (player == null || (player.position - pos).sqrMagnitude > PlayerFallbackRange * PlayerFallbackRange)
            return false;
        // Behind the player, so he doesn't pop up in their face
        return TryNear(kelvin, player.position - Flat(player.forward).normalized * 2f, out spot);
    }

    // Points every `step` metres along the route, up to `maxDistance` from its start
    private static IEnumerable<(Vector3 point, float distance)> SampleRoute(List<Vector3> route, float step, float maxDistance)
    {
        var travelled = 0f;
        var next = step;
        for (var i = 1; i < route.Count; i++)
        {
            var from = route[i - 1];
            var segment = route[i] - from;
            var length = segment.magnitude;
            while (next <= travelled + length && next <= maxDistance)
            {
                yield return (from + segment * ((next - travelled) / length), next);
                next += step;
            }
            travelled += length;
            if (next > maxDistance)
                yield break;
        }
    }

    private static bool TryFirstHit(Transform kelvin, Vector3 origin, Vector3 dir, float range, out float distance)
    {
        distance = float.MaxValue;
        foreach (var hit in Physics.SphereCastAll(origin, 0.3f, dir, range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !StuckDiagnostics.IsKelvin(hit.collider, kelvin) && hit.distance < distance)
                distance = hit.distance;
        }
        return distance < float.MaxValue;
    }

    private static void CheckFire(VailActor actor, Vector3 pos, float now)
    {
        if (now < _fireCooldownUntil || !(actor.IsOnFire() || actor.IsBodyBurning()))
            return;

        // The oldest remembered spot that's clearly away from where he is now
        foreach (var (_, oldPos) in History)
        {
            if ((oldPos - pos).sqrMagnitude < 2f * 2f)
                continue;
            actor.SetPositionAndRotation(oldPos, Quaternion.LookRotation(Flat(oldPos - pos)), false);
            RLog.Msg($"Kelvin on fire at {pos}, moved him back to {oldPos}");
            Show("Kelvin walked into fire - pulled him out");
            _fireCooldownUntil = now + 3f;
            History.Clear();
            return;
        }
        _fireCooldownUntil = now + 1f;
    }

    private static void CheckResume(Robby robby, float now)
    {
        if (!_hasOrder || _completedAt < 0f || now - _completedAt < Config.ResumeDelay.Value)
            return;
        if (robby._interactingPlayer != null)
            return;

        if (_quickFails >= MaxQuickFails)
        {
            RLog.Msg($"Kelvin keeps dropping {_orderType} right away (probably nothing left to do), stopping auto-resume");
            Show("Kelvin can't continue his task - give him a new one");
            _hasOrder = false;
            return;
        }

        RLog.Msg($"Kelvin stopped {now - _completedAt:0}s ago, giving him {_orderType} ({_orderSub}, {_orderSub2}) again");
        var from = _orderFrom != null ? _orderFrom : LocalPlayer.Transform;
        _reissuing = true;
        try
        {
            robby.ServerGiveOrders(_orderType, _orderSub, _orderSub2, from);
        }
        finally
        {
            _reissuing = false;
        }
        Show("Kelvin went back to his task");
        _lastResumeTime = now;
        _completedAt = -1f;
    }

    // With no task Kelvin falls back to following the player. Unless the player picked
    // "Follow" themselves, tell him to stay put instead so he doesn't leave the base.
    private static void CheckStay(Robby robby, float now)
    {
        // Following isn't a running order of its own, it's what he does without one
        var idle = !_playerWantsFollow && !_autoStayed
                   && robby._interactingPlayer == null && !robby._waitingForOrders
                   && robby._activeOrder == null
                   && !robby.Sons_Ai_Vail_IRobbyCallbacks_HasStayOrders();
        if (!idle)
        {
            _idleSince = -1f;
            return;
        }
        // Give the game a moment to finish whatever transition it's in
        if (_idleSince < 0f)
            _idleSince = now;
        if (now - _idleSince < 2f)
            return;

        RLog.Msg($"Kelvin has nothing to do and would follow, telling him to stay at {robby._actor.Position()}");
        _reissuing = true;
        try
        {
            robby.ServerGiveOrders(Robby.OrderType.StayHere, 0, -1, LocalPlayer.Transform);
        }
        finally
        {
            _reissuing = false;
        }
        _autoStayed = true;
        _idleSince = -1f;
        Show("Kelvin is staying put");
    }

    // Logs what his movement state looks like when he stands still, so the next
    // problem can be diagnosed from the log
    private static void LogIfStandingStill(Robby robby, VailActor actor, float now)
    {
        if (now - _anchorTime < 6f || now < _nextStillLog)
            return;
        _nextStillLog = now + 15f;
        RLog.Msg($"Kelvin standing still {now - _anchorTime:0}s at {actor.Position()}: hasPath={actor._hasPath} " +
                 $"hasMoveTarget={actor._hasMoveToTarget} waitingForPath={actor._waitingForPath} " +
                 $"gameStuckTime={actor._timeMovementStuck:0.0} activeOrder={robby._activeOrder != null} " +
                 $"waitingForOrders={robby._waitingForOrders}");
    }

    // Ground close to the candidate's height with room for him to stand. Looking down
    // from only a little above keeps him off roofs and the tops of walls.
    private static bool TryFindStandingSpot(Transform kelvin, Vector3 candidate, out Vector3 spot)
    {
        spot = default;
        var best = float.MaxValue;
        var found = false;
        foreach (var hit in Physics.RaycastAll(candidate + Vector3.up * 1.2f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || StuckDiagnostics.IsKelvin(hit.collider, kelvin) || hit.distance >= best)
                continue;
            best = hit.distance;
            found = hit.normal.y >= 0.6f;
            spot = hit.point;
        }
        if (!found || Mathf.Abs(spot.y - candidate.y) > MaxHeightChange)
            return false;
        foreach (var col in Physics.OverlapCapsule(spot + Vector3.up * 0.6f, spot + Vector3.up * 1.6f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col != null && !StuckDiagnostics.IsKelvin(col, kelvin))
                return false;
        }
        return true;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.001f ? v : Vector3.forward;
    }

    private static void Show(string text)
    {
        if (Config.ShowMessage.Value)
            SonsTools.ShowMessage(text, 3f);
    }
}
