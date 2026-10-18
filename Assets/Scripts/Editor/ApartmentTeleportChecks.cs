#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;

// Repeatable Editor checks. Temporary collision fixtures live in a separate unsaved scene, never in the apartment.
public static class ApartmentTeleportChecks
{
    [Serializable] class Report
    {
        public string utc;
        public int passed;
        public string failure;
        public bool questLinkVerified;
        public string limits = "Editor logic/geometry only; no hardware input, render or comfort certification.";
    }
    static Report report;

    [MenuItem("Apartment Phase 2/M2-B3/Diagnose Door Connectivity (Read Only)")]
    public static void DiagnoseDoors()
    {
        var scene = ApartmentTeleportSetup.RequireScene();
        var mover = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ApartmentVRLocomotion>(true)).Single();
        var filter = new NavMeshQueryFilter { agentTypeID = mover.teleportSurface.agentTypeID, areaMask = 1 };
        var settings = NavMesh.GetSettingsByID(filter.agentTypeID);
        var lines = new System.Text.StringBuilder("Agent height=" + settings.agentHeight + ", radius=" + settings.agentRadius + "\n");
        foreach (var door in mover.teleportSurface.GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("Threshold_Door_") && !c.name.Contains("MainEntrance")))
        {
            var b = door.bounds;
            Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            Vector3 center = new Vector3(b.center.x, b.max.y, b.center.z);
            bool a = NavMesh.SamplePosition(center - normal * .85f, out NavMeshHit from, .25f, filter);
            bool z = NavMesh.SamplePosition(center + normal * .85f, out NavMeshHit to, .25f, filter);
            var path = new NavMeshPath();
            bool found = a && z && NavMesh.CalculatePath(from.position, to.position, filter, path);
            lines.AppendLine(door.name + ": samples=" + a + "/" + z + ", path=" + (found ? path.status.ToString() : "none"));
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TeleportDoorDiagnostics.txt", lines.ToString());
        Debug.Log("Teleport door diagnostics (no scene changes):\n" + lines);
    }

    [MenuItem("Apartment Phase 2/M2-B3/Run Teleport Checks")]
    public static void Run()
    {
        report = new Report { utc = DateTime.UtcNow.ToString("o"), failure = "" };
        Scene scratch = default;
        Scene original = default;
        try
        {
            original = ApartmentTeleportSetup.RequireScene();
            if (original.isDirty) throw new InvalidOperationException("请先保存场景，再运行自动检查。");
            CheckButton();
            CheckMath();
            CheckSmoothTurn();
            var mover = original.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ApartmentVRLocomotion>(true)).Single();
            Assert(mover.TeleportConfigurationError() == null, "saved teleport configuration");
            Physics.SyncTransforms();
            Vector3 valid = FindClearFloor(mover);
            Assert(mover.ValidateTeleportDestination(valid, out _, out _), "clear reachable floor accepted");
            Assert(!mover.ValidateTeleportDestination(new Vector3(float.NaN, 0f, 0f), out _, out _), "NaN rejected");
            Assert(!mover.ValidateTeleportDestination(valid + Vector3.right * 100f, out _, out _), "out-of-range rejected");
            Assert(!mover.ValidateTeleportDestination(valid + Vector3.up * 1f, out _, out _), "floating destination rejected");
            Vector3 originalPosition = mover.transform.position;
            Assert(!mover.TryTeleportTo(valid, out _), "no teleport commits outside tracked VR Play Mode");
            Assert(mover.transform.position == originalPosition, "failed teleport preserves player position");
            int floorLayer = LayerMask.NameToLayer("TeleportFloor");
            var furniture = mover.teleportSurface.transform.Find("Furniture").GetComponentsInChildren<BoxCollider>();
            var nearFurniture = furniture.FirstOrDefault(c => Vector3.Distance(c.bounds.center, mover.transform.position) < 4.5f);
            Assert(nearFurniture != null, "furniture fixture exists in apartment");
            var tabletop = new Vector3(nearFurniture.bounds.center.x, nearFurniture.bounds.max.y, nearFurniture.bounds.center.z);
            Assert(!mover.ValidateTeleportDestination(tabletop, out _, out _), "furniture cannot be a landing floor");
            var entrance = mover.teleportSurface.GetComponentsInChildren<BoxCollider>().Single(c => c.name == "Floor_Entrance_1");
            var edge = new Vector3(entrance.bounds.max.x - .02f, entrance.bounds.max.y, entrance.bounds.center.z);
            Assert(!mover.ValidateTeleportDestination(edge, out _, out _), "physical floor edge cannot fit the player");

            scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            CheckRoomRoutes(mover, scratch);
            var obstruction = Box("Temporary blocked landing", valid + Vector3.up * 1f, new Vector3(.65f, 2f, .65f), 0);
            Physics.SyncTransforms();
            Assert(!mover.ValidateTeleportDestination(valid, out _, out _), "new obstacle invalidates previously clear landing without rebake");
            obstruction.SetActive(false);
            Physics.SyncTransforms();
            Assert(mover.ValidateTeleportDestination(valid, out _, out _), "removing temporary obstacle restores destination");

            Vector3 rayStart = new Vector3(1000f, 1f, 1000f);
            var wall = Box("Temporary first-hit wall", rayStart + Vector3.right, new Vector3(.1f, 1f, 1f), 0);
            Box("Temporary floor behind wall", rayStart + Vector3.right * 2f, new Vector3(.1f, 1f, 1f), floorLayer);
            Physics.SyncTransforms();
            var query = typeof(ApartmentVRLocomotion).GetMethod("FirstSceneHit", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] args = { new Ray(rayStart, Vector3.right), 3f, default(RaycastHit), false };
            Assert((bool)query.Invoke(mover, args), "arc ray sees solids");
            Assert(((RaycastHit)args[2]).collider.gameObject == wall, "nearest wall blocks floor behind it");
            Assert(!(bool)args[3], "small collision query is not saturated");
            for (int i = 0; i < 70; i++)
                Box("Temporary saturation " + i, rayStart + Vector3.forward * 10f + Vector3.right * (1f + i * .02f),
                    new Vector3(.01f, 1f, 1f), 0);
            Physics.SyncTransforms();
            args = new object[] { new Ray(rayStart + Vector3.forward * 10f, Vector3.right), 3f, default(RaycastHit), false };
            Assert((bool)query.Invoke(mover, args) && (bool)args[3], "saturated collision query fails closed");
            CheckPreview(valid);
        }
        catch (Exception e) { report.failure = e.ToString(); }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
            Physics.SyncTransforms();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/TeleportChecks.json", JsonUtility.ToJson(report, true));
            if (string.IsNullOrEmpty(report.failure)) Debug.Log("M2-B3 checks PASS: " + report.passed +
                " assertions. Report: Logs/TeleportChecks.json. Quest Link hardware NOT verified.");
            else Debug.LogError("M2-B3 checks FAILED after " + report.passed + " assertions: " + report.failure);
        }
    }

    static Vector3 FindClearFloor(ApartmentVRLocomotion mover, Transform floorRoot = null)
    {
        var floors = (floorRoot == null ? mover.teleportSurface.transform : floorRoot).GetComponentsInChildren<BoxCollider>().Where(c =>
            c.name.StartsWith("Floor_", StringComparison.Ordinal));
        foreach (var floor in floors)
            for (int x = 1; x < 6; x++)
                for (int z = 1; z < 6; z++)
                {
                    var b = floor.bounds;
                    Vector3 point = new Vector3(Mathf.Lerp(b.min.x, b.max.x, x / 6f), b.max.y,
                        Mathf.Lerp(b.min.z, b.max.z, z / 6f));
                    if (mover.ValidateTeleportDestination(point, out Vector3 resolved, out _)) return resolved;
                }
        throw new InvalidOperationException("没有找到出生点附近的安全落点；请检查 Bake、地板层、出生位置和碰撞体。");
    }

    static void CheckSmoothTurn()
    {
        var input = new ApartmentSmoothTurnInput();
        Assert(input.Sample(Vector2.right, .2f) == 0f, "smooth yaw starts disarmed");
        input.Sample(Vector2.zero, .2f);
        Assert(input.Sample(Vector2.right, .2f) == 1f, "full stick gives full continuous yaw");
        Assert(input.Sample(Vector2.right, .2f) == 1f, "held stick keeps turning, not a snap latch");
        Assert(input.Sample(new Vector2(.6f, 0f), .2f) > .49f && input.Sample(new Vector2(.6f, 0f), .2f) < .51f,
            "half strength after dead zone gives half yaw speed");
        Assert(input.Sample(Vector2.left, .2f) == -1f, "left stick direction gives negative yaw");
        Assert(input.Sample(Vector2.up, .2f) == 0f, "vertical right-stick input does not turn");
        Assert(input.Sample(new Vector2(.1f, 0f), .2f) == 0f, "small horizontal drift ignored");
        input.Reset();
        Assert(input.Sample(Vector2.right, .2f) == 0f, "resume requires neutral after reset");
        input.Sample(Vector2.zero, .2f);
        Assert(input.Sample(Vector2.right, .2f) == 1f, "neutral rearms smooth yaw");
        Assert(input.Sample(new Vector2(float.NaN, 0f), .2f) == 0f, "nonfinite yaw is rejected");
        Assert(input.Sample(Vector2.right, .2f) == 0f, "nonfinite input disarms subsequent held stick");
        foreach (int fps in new[] { 24, 60, 120 })
        {
            Vector3 root = new Vector3(4f, 0f, 3f);
            Vector3 head = root + new Vector3(.4f, 1.65f, -.25f);
            Vector3 fixedHead = head;
            Quaternion rotation = Quaternion.identity;
            Vector3 headLocal = head - root;
            for (int frame = 0; frame < fps; frame++)
            {
                float yaw = 60f / fps;
                root = ApartmentTurnMath.RootAroundHead(root, head, yaw);
                rotation = (Quaternion.AngleAxis(yaw, Vector3.up) * rotation).normalized;
                head = root + rotation * headLocal;
            }
            Assert(Vector3.Distance(head, fixedHead) < .0001f, "smooth yaw keeps offset head fixed at " + fps + "fps");
            Assert(Vector3.Distance(rotation * Vector3.forward, Quaternion.Euler(0f, 60f, 0f) * Vector3.forward) < .0001f,
                "smooth yaw rate independent of " + fps + "fps");
            Assert(Mathf.Abs(root.y) < .0001f, "turn preserves floor height at " + fps + "fps");
        }
    }

    static void CheckRoomRoutes(ApartmentVRLocomotion original, Scene scratch)
    {
        var host = UnityEngine.Object.Instantiate(original.gameObject);
        host.name = "Temporary cross-room teleport probe";
        SceneManager.MoveGameObjectToScene(host, scratch);
        var probe = host.GetComponent<ApartmentVRLocomotion>();
        probe.enabled = false;
        probe.rig.enabled = false;
        // A scratch-scene surface reference satisfies the same-scene invariant, while queries
        // use the real apartment's already registered map. Assigning data does not AddData/rebake.
        var mapHost = new GameObject("Temporary map reference (no extra NavMesh registration)");
        var mapReference = mapHost.AddComponent<NavMeshSurface>();
        mapReference.agentTypeID = original.teleportSurface.agentTypeID;
        mapReference.navMeshData = original.teleportSurface.navMeshData;
        probe.teleportSurface = mapReference;
        var controller = host.GetComponent<CharacterController>();
        controller.height = 1.8f;
        controller.center = Vector3.up * .9f;
        probe.rig.centerEyeAnchor.localPosition = Vector3.up * 1.65f;
        var filter = new NavMeshQueryFilter { agentTypeID = original.teleportSurface.agentTypeID, areaMask = 1 };
        bool diagonalRouteFound = false;
        foreach (var door in original.teleportSurface.GetComponentsInChildren<BoxCollider>().Where(c =>
            c.name.StartsWith("Threshold_Door_") && !c.name.Contains("MainEntrance")))
        {
            var b = door.bounds;
            Vector3 center = new Vector3(b.center.x, b.max.y, b.center.z);
            Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
            // Immediately either side of a doorway, beyond the capsule radius. Ignore furniture
            // farther into the room; the regression is specifically passage through the opening.
            Vector3 a = center - normal * .48f, z = center + normal * .48f;
            host.transform.SetPositionAndRotation(a, Quaternion.identity);
            Physics.SyncTransforms();
            Assert(NavMesh.SamplePosition(a, out NavMeshHit from, .12f, filter) &&
                NavMesh.SamplePosition(z, out NavMeshHit to, .12f, filter) &&
                NavMesh.CalculatePath(from.position, to.position, filter, new NavMeshPath()), door.name + " has candidate navigation");
            bool valid = probe.ValidateTeleportDestination(z, out _, out string reason);
            Assert(valid, door.name + " forward teleport accepted: " + reason);
            host.transform.position = z;
            Physics.SyncTransforms();
            valid = probe.ValidateTeleportDestination(a, out _, out reason);
            Assert(valid, door.name + " reverse teleport accepted: " + reason);
            // A live closed door inserted after bake must invalidate the route.
            host.transform.position = a;
            var blocker = Box("Temporary closed door", center + Vector3.up * 1f,
                b.size.x < b.size.z ? new Vector3(.15f, 2f, .8f) : new Vector3(1.1f, 2f, .15f), 0);
            Physics.SyncTransforms();
            Assert(!probe.ValidateTeleportDestination(z, out _, out _), door.name + " closed door blocks stale-map route");
            blocker.SetActive(false);
            Physics.SyncTransforms();
            Assert(probe.ValidateTeleportDestination(z, out _, out _), door.name + " reopened door restores route");
            // Actual tall body is still constrained by lintel, despite lower bake height.
            controller.height = 2.2f;
            controller.center = Vector3.up * 1.1f;
            Assert(!probe.ValidateTeleportDestination(z, out _, out _), door.name + " live tall capsule rejected under lintel");
            controller.height = 1.8f;
            controller.center = Vector3.up * .9f;
            // At least one real doorway must permit a diagonal aim whose direct body dash
            // clips a jamb, but whose complete walking route safely goes through the opening.
            if (!diagonalRouteFound)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                var sweep = typeof(ApartmentVRLocomotion).GetMethod("SweepBlocked", BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (float sourceOffset in new[] { -.6f, -.35f, 0f, .35f, .6f })
                    foreach (float targetOffset in new[] { -.6f, -.35f, 0f, .35f, .6f })
                    {
                        Vector3 start = a + tangent * sourceOffset, end = z + tangent * targetOffset;
                        host.transform.position = start;
                        Physics.SyncTransforms();
                        if (probe.ValidateTeleportDestination(end, out _, out _) &&
                            (bool)sweep.Invoke(probe, new object[] { start + Vector3.up * .3f,
                                start + Vector3.up * 1.5f, .27f, end - start })) diagonalRouteFound = true;
                    }
            }
        }
        Assert(diagonalRouteFound, "actual diagonal doorway route accepted despite blocked straight body dash");
        // Confirm the actual runtime turn method applies yaw/offset together without drift.
        host.transform.SetPositionAndRotation(FindClearFloor(probe, original.teleportSurface.transform), Quaternion.identity);
        probe.rig.centerEyeAnchor.localPosition = new Vector3(.05f, 1.65f, 0f);
        controller.center = new Vector3(.05f, .9f, 0f);
        Physics.SyncTransforms();
        Vector3 before = probe.rig.centerEyeAnchor.position;
        var turn = typeof(ApartmentVRLocomotion).GetMethod("TryTurn", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert((bool)turn.Invoke(probe, new object[] { probe.rig.centerEyeAnchor, 2f }), "actual continuous turn increment accepted");
        Assert(Vector3.Distance(before, probe.rig.centerEyeAnchor.position) < .0001f, "actual continuous turn preserves head position");
        host.SetActive(false);
        Physics.SyncTransforms();
    }

    static GameObject Box(string name, Vector3 position, Vector3 size, int layer)
    {
        var item = new GameObject(name);
        item.transform.position = position;
        item.layer = layer;
        item.AddComponent<BoxCollider>().size = size;
        return item;
    }

    static void CheckPreview(Vector3 floor)
    {
        var host = new GameObject("Temporary preview check");
        var mover = host.AddComponent<ApartmentVRLocomotion>();
        mover.enabled = false;
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(ApartmentVRLocomotion).GetMethod("EnsureTeleportRuntimeState", flags).Invoke(mover, null);
        var points = (Vector3[])typeof(ApartmentVRLocomotion).GetField("arcPoints", flags).GetValue(mover);
        points[0] = floor + Vector3.up;
        points[1] = floor;
        var draw = typeof(ApartmentVRLocomotion).GetMethod("DrawTeleportPreview", flags);
        draw.Invoke(mover, new object[] { 2, true, floor, false });
        var preview = host.transform.Find("TeleportPreview (runtime only)");
        Assert(preview != null, "preview objects generated");
        Assert(preview.GetComponentsInChildren<Collider>(true).Length == 0, "preview has no physics colliders");
        var arc = preview.Find("AimArc").GetComponent<LineRenderer>();
        var ring = preview.Find("LandingRing").GetComponent<LineRenderer>();
        Assert(arc.positionCount == 2 && ring.positionCount == 33, "arc and ring geometry generated");
        Assert(arc.sharedMaterial.shader.name == "Unlit/Color", "preview shader exists in Editor");
        Assert(arc.sharedMaterial.color.r > .9f && arc.sharedMaterial.color.g < .2f, "invalid preview is red");
        draw.Invoke(mover, new object[] { 2, true, floor, true });
        Assert(arc.sharedMaterial.color.g > .9f && arc.sharedMaterial.color.r < .2f, "valid preview is green");
        draw.Invoke(mover, new object[] { 2, false, floor, false });
        Assert(!ring.enabled, "no-hit hides landing ring");
        typeof(ApartmentVRLocomotion).GetMethod("HideTeleportVisuals", flags).Invoke(mover, null);
        Assert(!preview.gameObject.activeSelf, "cancellation hides preview");
        UnityEngine.Object.DestroyImmediate(arc.sharedMaterial);
    }

    static void Assert(bool success, string name)
    {
        if (!success) throw new InvalidOperationException(name);
        report.passed++;
    }

    static void CheckButton()
    {
        var b = new ApartmentTeleportButtonLatch();
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.None, "held A at startup is disarmed");
        Assert(b.Sample(true, false) == ApartmentTeleportButtonAction.None, "release arms input");
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.Begin && b.IsAiming, "press starts aiming");
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.Hold, "hold does not commit");
        Assert(b.Sample(true, false) == ApartmentTeleportButtonAction.Release && !b.IsAiming, "release commits once");
        Assert(b.Sample(true, false) == ApartmentTeleportButtonAction.None, "release does not repeat");
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.Begin, "second press starts fresh aim");
        Assert(b.Sample(false, true) == ApartmentTeleportButtonAction.Cancel && !b.IsAiming, "tracking/focus loss cancels");
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.None, "held A on reconnect cannot restart");
        Assert(b.Sample(true, false) == ApartmentTeleportButtonAction.None, "reconnect release does not commit");
        Assert(b.Sample(true, true) == ApartmentTeleportButtonAction.Begin, "fresh press after reconnect works");
        b.Reset();
        Assert(!b.IsAiming && b.Sample(true, true) == ApartmentTeleportButtonAction.None, "mode reset disarms held A");
        var snap = new ApartmentSnapTurnLatch();
        Assert(snap.Sample(1f, 1f, .7f, .25f) == 0, "snap turn starts disarmed");
        snap.Sample(0f, 0f, .7f, .25f);
        Assert(snap.Sample(1f, 1f, .7f, .25f) == 1, "existing right snap turn preserved");
        Assert(snap.Sample(1f, 1f, .7f, .25f) == 0, "existing held-stick latch preserved");
    }

    static void CheckMath()
    {
        var root = new Vector3(.45f, 0f, .7f);
        var head = root + new Vector3(.4f, 1.65f, -.25f);
        var floor = new Vector3(2f, 0f, 3f);
        var moved = ApartmentTeleportMath.RootAtLanding(root, head, floor);
        var movedHead = head + moved - root;
        Assert(Mathf.Abs(movedHead.x - floor.x) < .0001f, "room-scale X compensated");
        Assert(Mathf.Abs(movedHead.z - floor.z) < .0001f, "room-scale Z compensated");
        Assert(Mathf.Abs(movedHead.y - head.y) < .0001f, "flat floor preserves eye height");
        Assert(moved.y == floor.y, "root remains on physical floor");
        var rotatedOffset = Quaternion.Euler(0f, 90f, 0f) * new Vector3(.4f, 1.65f, -.25f);
        moved = ApartmentTeleportMath.RootAtLanding(root, root + rotatedOffset, floor);
        movedHead = root + rotatedOffset + moved - root;
        Assert(Vector2.Distance(new Vector2(movedHead.x, movedHead.z), new Vector2(floor.x, floor.z)) < .0001f,
            "room-scale compensation also works after turning");
    }
}
#endif
