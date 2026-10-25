#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// An explicit, reversible candidate, not an automatic import callback. The caller alone saves.
public static class Phase3SpatialLayout
{
    [Serializable] public sealed class Move
    {
        public long sceneId;
        public string path;
        public Vector3 before, after, delta;
    }
    [Serializable] public sealed class Pair
    {
        public string first, second;
        public bool beforeOverlap, afterOverlap, beforeSupported, afterSupported;
        public float beforeDepth, afterDepth, increase;
        public string limitation;
    }
    [Serializable] public sealed class CandidateReport
    {
        public string evidence = "Unity Editor imported collider candidate; not CharacterController.Move, participant, Quest or Player acceptance.";
        public string scope;
        public bool accepted, restoredAfterRejection, sceneSaved = false;
        public int comparedColliderPairs, unchangedAssemblyPairsExcluded;
        public List<Move> moves = new List<Move>();
        public List<Pair> touchingPairs = new List<Pair>();
        public List<string> failures = new List<string>();
        public List<string> doorChecks = new List<string>();
        public Phase3SpatialRouteChecks.Report routes;
        public string limitations = "Only fixed candidate X/Z changes; scales, rotations, support Y and source Prefabs remain unchanged. A passing .30m capsule route is not a .90m architectural passage. Rug follows the revised seating group; verify full support footprint separately. Seated use and chair pull-out need participant validation.";
    }
    sealed class Pose
    {
        public long id;
        public Transform target;
        public Vector3 position, localScale, delta;
        public Quaternion localRotation;
    }
    sealed class PairState
    {
        public Collider a, b;
        public bool overlap, supported;
        public float depth;
    }

    public static CandidateReport ApplyCandidate(bool balconyOnly = false, bool includeDining = true)
    {
        var report = new CandidateReport { scope = balconyOnly ? "Balcony left chair and table +0.35m Z only" : "Armchair, sofa, coffee table and rug; balcony left chair and table; relocate left planter to north band" + (includeDining ? "; dining table and two left chairs +0.25m X" : "; dining unchanged") };
        var plan = new List<Pose>();
        if (!balconyOnly)
        {
            Add(plan, 732500854, new Vector2(4.85f, 1.60f), new Vector3(1.05f, 0, -.50f));
            Add(plan, 16152113, new Vector2(4.456f, 3.90f), new Vector3(.18f, 0, -.75f));
            Add(plan, 1858018937, new Vector2(5.138f, 3.30f), new Vector3(.52f, 0, -.75f));
            Add(plan, 233736802, new Vector2(4.95f, 3.30f), new Vector3(0, 0, -.12f));
            if (includeDining)
            {
                Add(plan, 1337975014, new Vector2(4.45f, 6.20f), new Vector3(.25f, 0, 0));
                Add(plan, 247076002, new Vector2(3.772f, 5.609f), new Vector3(.25f, 0, 0));
                Add(plan, 730754861, new Vector2(3.772f, 6.603f), new Vector3(.25f, 0, 0));
            }
        }
        Add(plan, 221027378, new Vector2(6.35f, 6.35f), new Vector3(0, 0, .35f));
        // Right terrace chair stays put: moving it north introduced a real planter collision.
        Add(plan, 1517314931, new Vector2(7.25f, 6.35f), new Vector3(0, 0, .35f));
        // Relocate the existing left planter into the free north band behind the unchanged right seats.
        Add(plan, 1923070340, new Vector2(6.30f, 6.90f), new Vector3(2.15f, 0, 0));
        // Far-right deck chair stays put: its north move also intersected the planter.
        if (Application.isPlaying || plan.Any(p => p.target.gameObject.scene.path != Phase3SpatialStabilization.ScenePath))
            throw new InvalidOperationException("Open the isolated formal scene with Play stopped.");
        Physics.SyncTransforms();
        var all = Phase3SpatialStabilization.All<Collider>().Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).ToArray();
        var moved = all.Where(c => Owner(c, plan) != null).ToArray();
        var baseline = new Dictionary<string, PairState>();
        foreach (var a in moved)
            foreach (var b in all)
            {
                if (a == b) continue;
                var ownerA = Owner(a, plan); var ownerB = Owner(b, plan);
                if (ownerA == ownerB) { report.unchangedAssemblyPairsExcluded++; continue; }
                string key = Key(a, b);
                if (!baseline.ContainsKey(key)) baseline.Add(key, Measure(a, b));
            }
        report.comparedColliderPairs = baseline.Count;
        try
        {
            foreach (var pose in plan)
            {
                pose.target.position = pose.position + pose.delta;
                report.moves.Add(new Move { sceneId = pose.id, path = Phase3SpatialStabilization.PathOf(pose.target), before = pose.position, after = pose.target.position, delta = pose.delta });
            }
            Physics.SyncTransforms();
            foreach (var old in baseline.Values)
            {
                var current = Measure(old.a, old.b);
                if (!old.overlap && !current.overlap) continue;
                var a = Owner(old.a, plan); var b = Owner(old.b, plan);
                bool relativeUnchanged = (a != null ? a.delta : Vector3.zero) == (b != null ? b.delta : Vector3.zero);
                var item = new Pair {
                    first = Describe(old.a), second = Describe(old.b), beforeOverlap = old.overlap, afterOverlap = current.overlap,
                    beforeSupported = old.supported, afterSupported = current.supported,
                    beforeDepth = old.depth, afterDepth = current.depth, increase = current.depth - old.depth,
                    limitation = (!old.supported || !current.supported) ? "Two non-convex MeshColliders: ComputePenetration is unsupported; broadphase overlap is not proof of penetration." : "" };
                report.touchingPairs.Add(item);
                if (current.overlap && !current.supported && !relativeUnchanged)
                    report.failures.Add("Unresolved new/changed non-convex mesh overlap: " + item.first + " / " + item.second);
                else if (current.supported && current.depth > old.depth + .002f)
                    report.failures.Add("Penetration increased over 2mm: " + item.first + " / " + item.second + " increase=" + item.increase);
            }
            foreach (var pose in plan)
            {
                var bounds = Phase3SpatialStabilization.VisualBounds(pose.target);
                if (bounds.min.x < -.001f || bounds.max.x > 9.601f || bounds.min.z < -.001f || bounds.max.z > 7.261f)
                    report.failures.Add("Candidate visual leaves the authored room perimeter: " + pose.target.name);
                if (pose.target.localScale != pose.localScale || Quaternion.Angle(pose.target.localRotation, pose.localRotation) > .0001f || Mathf.Abs(pose.target.position.y - pose.position.y) > .00001f)
                    report.failures.Add("Candidate changed preserved scale, rotation or support height: " + pose.target.name);
            }
            if (report.failures.Count == 0)
            {
                report.failures.AddRange(Phase3SpatialDoorAndRoute.CheckDoors());
                report.doorChecks.AddRange(Phase3SpatialDoorAndRoute.LastChecks);
            }
            if (report.failures.Count == 0)
            {
                report.routes = Phase3SpatialRouteChecks.Run(false);
                report.failures.AddRange(report.routes.failures);
                if (report.routes.routes.Count != 3 || report.routes.routes.Any(r => !r.success || !r.strictRequestedGoalReached || r.usedAlternativeGoal))
                    report.failures.Add("All three exact .30m-radius route endpoints must be reached without fallback.");
            }
            report.accepted = report.failures.Count == 0;
            if (report.accepted)
                foreach (var pose in plan)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(pose.target);
                    EditorUtility.SetDirty(pose.target);
                }
        }
        catch (Exception e) { report.failures.Add(e.GetType().Name + ": " + e.Message); report.accepted = false; }
        finally
        {
            if (!report.accepted)
            {
                foreach (var pose in plan) pose.target.position = pose.position;
                Physics.SyncTransforms();
                report.restoredAfterRejection = plan.All(p => p.target.position == p.position && p.target.localScale == p.localScale && p.target.localRotation == p.localRotation);
                if (!report.restoredAfterRejection) report.failures.Add("Rejected candidate did not restore exactly; do not save.");
            }
        }
        return report;
    }

    static void Add(List<Pose> plan, long id, Vector2 expected, Vector3 delta)
    {
        Transform target = Phase3SpatialStabilization.Find<Transform>(id);
        if (Mathf.Abs(target.position.x - expected.x) > .001f || Mathf.Abs(target.position.z - expected.y) > .001f)
            throw new InvalidOperationException("Stale/already-applied layout plan: " + target.name);
        plan.Add(new Pose { id = id, target = target, position = target.position, localScale = target.localScale, localRotation = target.localRotation, delta = delta });
    }
    static Pose Owner(Collider collider, List<Pose> plan) => plan.FirstOrDefault(p => collider.transform == p.target || collider.transform.IsChildOf(p.target));
    static string Key(Collider a, Collider b) { string x=GlobalObjectId.GetGlobalObjectIdSlow(a).ToString(),y=GlobalObjectId.GetGlobalObjectIdSlow(b).ToString();return string.CompareOrdinal(x,y)<0?x+":"+y:y+":"+x; }
    static string Describe(Collider c) => Phase3SpatialStabilization.PathOf(c.transform) + " [" + c.GetType().Name + " " + Phase3SpatialStabilization.Id(c) + "]";
    static PairState Measure(Collider a, Collider b)
    {
        bool supported = !(a is MeshCollider ma && !ma.convex && b is MeshCollider mb && !mb.convex);
        if (!a.bounds.Intersects(b.bounds)) return new PairState { a = a, b = b, supported = supported };
        if (!supported) return new PairState { a = a, b = b, overlap = true, supported = false };
        bool overlap = Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float depth);
        return new PairState { a = a, b = b, overlap = overlap, depth = overlap ? depth : 0, supported = true };
    }
}
#endif
