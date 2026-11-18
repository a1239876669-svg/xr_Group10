#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using QuestGuidance.Review;

// Explicit Editor-only physics evidence. No scene saving, layout edits, input or XR execution.
public static class Phase3SpatialRouteChecks
{
    const float Grid = .05f, EdgeSpacing = .025f, FootLift = .025f, Height = 1.8f;
    const float MinX = 0f, MaxX = 9.6f, MinZ = -2.6f, MaxZ = 7.5f;
    const int QueryBudget = 75000;
    static readonly Vector3 FarHead = new Vector3(-1000, 1000, -1000);

    [Serializable] public sealed class DoorCheck
    {
        public string path, state, reason;
        public float angleOrTravel;
        public bool fullyOpened;
    }
    [Serializable] public sealed class RadiusCheck
    {
        public float radius;
        public int physicsQueries, overlapQueries, floorQueries, cachedSamples;
        public bool queryBudgetExceeded;
        public List<string> blockingColliders = new List<string>();
    }
    [Serializable] public sealed class RouteCheck
    {
        public string name, reason, requestedGoalRoom, reachedGoalRoom;
        public float radius, gridSpacing = Grid, maxEdgeSampleSpacing = EdgeSpacing;
        public Vector3 requestedStart, requestedGoal, reachedStart, reachedGoal;
        public bool success, requestedStartClear, requestedGoalClear;
        public bool strictRequestedGoalReached, usedAlternativeGoal;
        public int visitedCells;
        public float pathLength, maximumFloorStep, approximateMinimumRadialMargin;
        public float approximateMinimumUprightCapsuleDiameter;
        public bool marginCalculated, marginUpperSearchCapped;
        public string marginMethod = "At most 101 path nodes, radius binary search to 0.60m; approximate local capsule margin, not architectural net width.";
        public List<Vector3> path = new List<Vector3>();
        public List<string> endpointBlockers = new List<string>();
    }
    [Serializable] public sealed class Report
    {
        public string evidence = "Actual Unity Editor Physics.OverlapCapsuleNonAlloc, real imported colliders and sampled path edges; not Quest or Player validation.";
        public string limitations = "A sampled upright capsule path is not CharacterController.Move, room-scale user movement, door operation by a participant, architectural net-width certification or a measured bag passage.";
        public float gridSpacing = Grid, capsuleHeight = Height, footLift = FootLift;
        public float preservedLocomotionRadius, preservedStepOffset;
        public bool sceneSaved = false, closedDoorPosesRestored;
        public List<DoorCheck> doors = new List<DoorCheck>();
        public List<RadiusCheck> radiusChecks = new List<RadiusCheck>();
        public List<RouteCheck> routes = new List<RouteCheck>();
        public List<string> failures = new List<string>();
    }
    sealed class Pose
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Pose(Transform t) { transform = t; localPosition = t.localPosition; localRotation = t.localRotation; }
        public bool Matches() => Vector3.Distance(localPosition, transform.localPosition) < .00001f &&
            Quaternion.Angle(localRotation, transform.localRotation) < .001f;
    }
    sealed class Sample
    {
        public bool clear;
        public float floorY;
        public List<string> blockers = new List<string>();
    }
    sealed class BudgetExceeded : Exception { }

    public static Report Run(bool includeComfortRadius = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Route checks require stopped Play mode.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != Phase3SpatialStabilization.ScenePath)
            throw new InvalidOperationException("Only the formal detailed apartment may be checked.");
        var report = new Report();
        var mover = Phase3SpatialStabilization.All<ApartmentVRLocomotion>().Single();
        report.preservedLocomotionRadius = mover.capsuleRadius;
        report.preservedStepOffset = mover.stepOffset;
        var doors = Phase3SpatialStabilization.All<ApartmentCabinetDoor>();
        var slides = Phase3SpatialStabilization.All<ApartmentSlidingDoor>();
        if (doors.Any(d => Mathf.Abs(d.CurrentAngle) > .0001f) ||
            slides.Any(d => Mathf.Abs(d.CurrentTravel) > .0001f))
            throw new InvalidOperationException("Start from closed doors; no unknown live door state will be overwritten.");
        var poses = doors.Select(d => new Pose(d.transform))
            .Concat(slides.Select(d => new Pose(d.movingLeaf))).ToArray();
        try
        {
            foreach (var d in doors) d.ResetClosed();
            foreach (var d in slides) d.ResetClosed();
            var entrance = doors.Single(d => d.name == "Door_Entrance_Hinge");
            Open(entrance, report);
            foreach (var sliding in slides) Open(sliding, report);
            Physics.SyncTransforms();
            foreach (float radius in includeComfortRadius ? new[] { .30f, .45f } : new[] { .30f })
            {
                var search = new Search(scene, mover.transform, mover.teleportFloorMask.value,
                    radius, mover.stepOffset);
                try
                {
                    report.routes.Add(search.Route("Entrance to Living Room",
                        new Vector2(4.4f, -1.55f), new Vector2(5.5f, 4.75f), false));
                    report.routes.Add(search.Route("Living Room to Kitchen",
                        new Vector2(5.5f, 4.75f), new Vector2(2.1f, 5.5f), false));
                    // Living probe point (5.5,4.75) is an explicit geometry-test point, not a moved task anchor.
                    // The former (7.2,6.0) lies in the balcony table. Use the explicit front-edge
                    // review point; if blocked, record the failure and any reachable alternative separately.
                    report.routes.Add(search.Route("Living Room to Balcony front edge",
                        new Vector2(5.5f, 4.75f), new Vector2(6.4f, 5.95f), true));
                }
                catch (BudgetExceeded)
                {
                    search.summary.queryBudgetExceeded = true;
                    report.failures.Add("Physics query budget reached at radius " + radius +
                        "; incomplete checks are not passes.");
                }
                finally { search.Finish(); report.radiusChecks.Add(search.summary); }
            }
        }
        catch (Exception e) { report.failures.Add(e.GetType().Name + ": " + e.Message); }
        finally
        {
            foreach (var d in doors) d.ResetClosed();
            foreach (var d in slides) d.ResetClosed();
            Physics.SyncTransforms();
            report.closedDoorPosesRestored = poses.All(p => p.Matches());
            if (!report.closedDoorPosesRestored)
                report.failures.Add("Closed transform poses did not match the original snapshot; do not save this temporary scene.");
        }
        return report;
    }

    static void Open(ApartmentCabinetDoor door, Report report)
    {
        string reason;
        bool accepted = door.TryInteract(out reason);
        for (int i = 0; accepted && door.IsMoving && i < 600; i++)
            door.AdvanceMotion(.05f, FarHead, false);
        bool open = door.State == ApartmentCabinetState.Open &&
            Mathf.Abs(door.CurrentAngle - door.openAngle) < .01f;
        report.doors.Add(new DoorCheck {
            path = Phase3SpatialStabilization.PathOf(door.transform),
            state = door.State.ToString(), angleOrTravel = door.CurrentAngle,
            reason = accepted ? door.BlockedReason : reason, fullyOpened = open });
        if (!open) report.failures.Add("Actual entrance door motion did not reach its configured open angle: " + door.BlockedReason);
    }
    static void Open(ApartmentSlidingDoor door, Report report)
    {
        string reason;
        bool accepted = door.TryInteract(out reason);
        for (int i = 0; accepted && door.IsMoving && i < 600; i++)
            door.AdvanceMotion(.05f, FarHead, false);
        bool open = door.State == ApartmentCabinetState.Open &&
            Mathf.Abs(door.CurrentTravel - door.localTravel.magnitude) < .001f;
        report.doors.Add(new DoorCheck {
            path = Phase3SpatialStabilization.PathOf(door.transform),
            state = door.State.ToString(), angleOrTravel = door.CurrentTravel,
            reason = accepted ? door.BlockedReason : reason, fullyOpened = open });
        if (!open) report.failures.Add("Actual sliding-door motion did not fully open: " + door.BlockedReason);
    }

    sealed class Search
    {
        readonly Scene scene;
        readonly Transform player;
        readonly int floorMask, nx, nz;
        readonly float radius, stepOffset;
        readonly Dictionary<long, Sample> samples = new Dictionary<long, Sample>();
        readonly Dictionary<long, bool> edges = new Dictionary<long, bool>();
        readonly Dictionary<string, int> blocked = new Dictionary<string, int>();
        readonly Dictionary<Collider, string> descriptions = new Dictionary<Collider, string>();
        readonly Collider[] hits = new Collider[256];
        readonly RaycastHit[] floorHits = new RaycastHit[128];
        readonly ReviewRoomMap roomMap;
        public readonly RadiusCheck summary;

        public Search(Scene scene, Transform player, int floorMask, float radius, float stepOffset)
        {
            this.scene = scene; this.player = player; this.floorMask = floorMask;
            this.radius = radius; this.stepOffset = stepOffset;
            nx = Mathf.RoundToInt((MaxX - MinX) / Grid) + 1;
            nz = Mathf.RoundToInt((MaxZ - MinZ) / Grid) + 1;
            roomMap = Phase3SpatialStabilization.All<ReviewRoomMap>().FirstOrDefault();
            summary = new RadiusCheck { radius = radius };
        }
        void Count(bool floor)
        {
            if (++summary.physicsQueries > QueryBudget) throw new BudgetExceeded();
            if (floor) summary.floorQueries++; else summary.overlapQueries++;
        }
        static long Key(Vector2 p) => ((long)Mathf.RoundToInt(p.x * 100000f) << 32) ^
            (uint)Mathf.RoundToInt(p.y * 100000f);
        Vector2 Point(int id) => new Vector2(MinX + (id % nx) * Grid, MinZ + (id / nx) * Grid);
        int Node(Vector2 p) => Mathf.Clamp(Mathf.RoundToInt((p.y - MinZ) / Grid), 0, nz - 1) * nx +
            Mathf.Clamp(Mathf.RoundToInt((p.x - MinX) / Grid), 0, nx - 1);
        bool IsValid(Collider c) => c != null && c.enabled && !c.isTrigger &&
            c.gameObject.activeInHierarchy && c.gameObject.scene == scene &&
            !c.transform.IsChildOf(player);
        string Describe(Collider c)
        {
            Collider key = c;
            if (!descriptions.TryGetValue(key, out string text))
            {
                text = Phase3SpatialStabilization.PathOf(c.transform) + " [" +
                    c.GetType().Name + " " + Phase3SpatialStabilization.Id(c) + "]";
                descriptions.Add(key, text);
            }
            return text;
        }
        Sample At(Vector2 p)
        {
            long key = Key(p);
            if (samples.TryGetValue(key, out var value)) return value;
            value = new Sample();
            Count(true);
            int count = Physics.RaycastNonAlloc(new Vector3(p.x, 2.5f, p.y), Vector3.down,
                floorHits, 3f, floorMask, QueryTriggerInteraction.Ignore);
            if (count == floorHits.Length)
            {
                value.blockers.Add("Floor ray query buffer overflow; rejected.");
                samples.Add(key, value); return value;
            }
            bool groundFound = false;
            float highest = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = floorHits[i];
                if (!IsValid(hit.collider) || hit.normal.y < .70f || hit.point.y < -.05f || hit.point.y > .25f) continue;
                if (hit.point.y > highest) { highest = hit.point.y; groundFound = true; }
            }
            if (!groundFound)
            {
                value.blockers.Add("No real floor support at this sample.");
                samples.Add(key, value); return value;
            }
            value.floorY = highest;
            value.clear = CapsuleClear(p, highest, radius, value.blockers);
            foreach (string b in value.blockers) blocked[b] = blocked.TryGetValue(b, out int n) ? n + 1 : 1;
            samples.Add(key, value); return value;
        }
        bool CapsuleClear(Vector2 p, float floorY, float r, List<string> blockers)
        {
            Count(false);
            float bottom = floorY + FootLift;
            Vector3 a = new Vector3(p.x, bottom + r, p.y);
            Vector3 b = new Vector3(p.x, bottom + Height - r, p.y);
            int count = Physics.OverlapCapsuleNonAlloc(a, b, r, hits, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                blockers?.Add("Capsule query buffer overflow; rejected."); return false;
            }
            bool clear = true;
            for (int i = 0; i < count; i++)
                if (IsValid(hits[i])) { clear = false; blockers?.Add(Describe(hits[i])); }
            return clear;
        }
        bool Segment(Vector2 a, Vector2 b)
        {
            Sample last = At(a);
            if (!last.clear) return false;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / EdgeSpacing));
            for (int i = 1; i <= n; i++)
            {
                Sample next = At(Vector2.Lerp(a, b, (float)i / n));
                if (!next.clear || Mathf.Abs(next.floorY - last.floorY) > stepOffset + .0001f) return false;
                last = next;
            }
            return true;
        }
        bool Edge(int a, int b)
        {
            long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
            if (edges.TryGetValue(key, out bool result)) return result;
            int ax = a % nx, az = a / nx, bx = b % nx, bz = b / nx;
            if (ax != bx && az != bz &&
                (!At(Point(az * nx + bx)).clear || !At(Point(bz * nx + ax)).clear))
                result = false;
            else result = Segment(Point(a), Point(b));
            edges.Add(key, result); return result;
        }
        string Room(Vector2 p)
        {
            var s = At(p);
            return roomMap != null ? roomMap.Resolve(new Vector3(p.x, s.floorY + 1.6f, p.y)) : "No ReviewRoomMap";
        }
        static bool InBalcony(Vector2 p) => p.x >= 5.76f && p.x <= 9.36f && p.y >= 5.75f && p.y <= 7.26f;

        public RouteCheck Route(string name, Vector2 start, Vector2 goal, bool allowBalconyAlternative)
        {
            var result = new RouteCheck { name = name, radius = radius,
                requestedStart = new Vector3(start.x, 0, start.y),
                requestedGoal = new Vector3(goal.x, 0, goal.y) };
            var ss = At(start); var gs = At(goal);
            result.requestedStartClear = ss.clear; result.requestedGoalClear = gs.clear;
            result.requestedGoalRoom = Room(goal);
            result.endpointBlockers.AddRange(ss.blockers.Select(b => "Start: " + b));
            result.endpointBlockers.AddRange(gs.blockers.Select(b => "Goal: " + b));
            if (!ss.clear) { result.reason = "Requested start capsule is blocked; no disconnected nearby start was substituted."; return result; }
            int begin = Node(start);
            if (!At(Point(begin)).clear || !Segment(start, Point(begin)))
            { result.reason = "Start cannot connect to nearest grid node with 2.5 cm samples."; return result; }
            var goals = new HashSet<int>();
            int strict = Node(goal);
            if (gs.clear && At(Point(strict)).clear && Segment(Point(strict), goal)) goals.Add(strict);
            else if (allowBalconyAlternative)
            {
                // An explicitly labelled alternative, within the actual balcony polygon, never a
                // free point on the other side of a wall. BFS must still physically reach it.
                for (int z = 0; z < nz; z++) for (int x = 0; x < nx; x++)
                {
                    int id = z * nx + x; Vector2 p = Point(id);
                    if (InBalcony(p) && Vector2.Distance(p, goal) <= .80f && At(p).clear) goals.Add(id);
                }
                result.usedAlternativeGoal = true;
            }
            if (goals.Count == 0)
            { result.reason = "Requested goal blocked/unconnectable and no permitted goal candidate is clear."; return result; }
            int[] parent = Enumerable.Repeat(-2, nx * nz).ToArray();
            var queue = new Queue<int>(); queue.Enqueue(begin); parent[begin] = -1;
            int found = -1;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); result.visitedCells++;
                if (goals.Contains(current)) { found = current; break; }
                int cx = current % nx, cz = current / nx;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = cx + dx, z = cz + dz;
                    if (x < 0 || z < 0 || x >= nx || z >= nz) continue;
                    int next = z * nx + x;
                    if (parent[next] != -2 || !At(Point(next)).clear || !Edge(current, next)) continue;
                    parent[next] = current; queue.Enqueue(next);
                }
            }
            if (found < 0)
            { result.reason = "No physically sampled connected capsule route on this grid; not a proof that no off-grid route exists."; return result; }
            var ids = new List<int>();
            for (int i = found; i >= 0; i = parent[i]) ids.Add(i);
            ids.Reverse();
            var horizontal = new List<Vector2> { start };
            horizontal.AddRange(ids.Select(Point));
            if (!result.usedAlternativeGoal) horizontal.Add(goal);
            foreach (var p in horizontal)
                result.path.Add(new Vector3(p.x, At(p).floorY + FootLift, p.y));
            for (int i = 1; i < result.path.Count; i++)
            {
                result.pathLength += Vector3.Distance(result.path[i - 1], result.path[i]);
                result.maximumFloorStep = Mathf.Max(result.maximumFloorStep,
                    Mathf.Abs(result.path[i].y - result.path[i - 1].y));
            }
            result.reachedStart = result.path[0]; result.reachedGoal = result.path[result.path.Count - 1];
            result.reachedGoalRoom = Room(horizontal[horizontal.Count - 1]);
            result.success = true;
            result.strictRequestedGoalReached = !result.usedAlternativeGoal;
            result.reason = result.usedAlternativeGoal ?
                "Requested goal remains blocked. A separately labelled balcony alternative is physically connected; this is NOT a pass at the requested goal." :
                "Connected real-collider capsule path found, with diagonal corner checks and edges sampled at no more than 2.5 cm.";
            Margin(horizontal, result);
            return result;
        }
        void Margin(List<Vector2> points, RouteCheck result)
        {
            // Local inscribed-capsule diagnostic only. It is not an architectural net width.
            float minAvailable = .60f;
            int stride = Mathf.Max(1, Mathf.CeilToInt(points.Count / 100f));
            bool capped = false;
            foreach (Vector2 p in points.Where((point, index) => index % stride == 0 || index == points.Count - 1))
            {
                float floor = At(p).floorY;
                float lo = radius, hi = .60f;
                if (CapsuleClear(p, floor, hi, null)) { capped = true; lo = hi; }
                else for (int j = 0; j < 6; j++)
                {
                    float mid = (lo + hi) / 2;
                    if (CapsuleClear(p, floor, mid, null)) lo = mid; else hi = mid;
                }
                minAvailable = Mathf.Min(minAvailable, lo);
            }
            result.marginCalculated = true;
            result.approximateMinimumRadialMargin = minAvailable - radius;
            result.approximateMinimumUprightCapsuleDiameter = 2 * minAvailable;
            result.marginUpperSearchCapped = capped;
        }
        public void Finish()
        {
            summary.cachedSamples = samples.Count;
            summary.blockingColliders = blocked.OrderByDescending(p => p.Value).Take(24)
                .Select(p => p.Key + " sampledHits=" + p.Value).ToList();
        }
    }
}
#endif
