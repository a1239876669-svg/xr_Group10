#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit menus only: no auto-installation, scene opening, runtime logic or respawn.
public static class ApartmentPhase2EntranceSafetyInstaller
{
    const string ScenePath = "Assets/Scenes/ApartmentPhase2_VR.unity";
    const string GrayboxLayout = "Assets/Apartment/ApartmentGrayboxLayout.json";
    const string FurnitureLayout = "Assets/Apartment/Furniture/ApartmentFurnitureLayout.json";
    const string RuntimePath = "Assets/Scripts/ApartmentVRLocomotion.cs";
    const string GroupName = "SimulationSafety";
    const string BoundaryName = "EntranceExteriorBoundary";
    const float Epsilon = .001f;
    static readonly Vector3 Center = new Vector3(0f, 1.40f, -.40f);
    static readonly Vector3 Size = new Vector3(1.20f, 2.80f, .10f);
    static readonly Bounds Candidate = new Bounds(Center, Size);
    static readonly string[] Sources = {
        "Assets/Scenes/ApartmentPhase2.unity", "Assets/Scenes/ApartmentPhase2_Furnished.unity" };
    static bool installing;

    [MenuItem("Apartment Phase 2/M2-B3/Install Entrance Safety Boundary (No Overwrite)")]
    public static void InstallEntranceSafetyBoundary()
    {
        if (installing) { Debug.LogError("M2-B3: installation is already in progress."); return; }
        installing = true;
        int undoGroup = -1;
        bool saveAttempted = false, saved = false;
        var sourceLocks = new List<FileStream>();
        try
        {
            Scene scene = RequireScene();
            RequireClean(scene);
            RequireNewObjects(scene);
            Report before = Inspect(scene, false);
            RequirePass(before);
            // Lock original scenes/metadata against accidental writes during this installation.
            foreach (string path in Sources.SelectMany(p => new[] { p, p + ".meta" }))
                sourceLocks.Add(new FileStream(Absolute(path), FileMode.Open, FileAccess.Read, FileShare.Read));
            var disk = CaptureProtectedFiles();
            string targetHash = Hash(Absolute(ScenePath));
            var original = CaptureObjects(scene);

            if (!EditorUtility.DisplayDialog("M2-B3 entrance prototype safety",
                "Add SimulationSafety/EntranceExteriorBoundary and save ONLY ApartmentPhase2_VR.unity.\n" +
                "Centre (0, 1.40, -0.40) m; BoxCollider size (1.20, 2.80, 0.10) m.\n" +
                "No renderer, Rigidbody, trigger, respawn or changes to existing geometry/scripts.\n" +
                "This blocks exiting the prototype. A 0.80 m probe at the doorway threshold is restricted; " +
                "the 0.60 m recorded routes must remain clear of the new boundary.\nContinue?",
                "Install and save VR scene", "Cancel")) return;

            RequireScene(); RequireClean(scene); RequireNewObjects(scene);
            RequireUnchanged(disk, targetHash);
            RequireObjectsUnchanged(scene, original, false);
            RequirePass(Inspect(scene, false));
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install M2-B3 entrance safety boundary");
            var group = new GameObject(GroupName);
            SceneManager.MoveGameObjectToScene(group, scene);
            Undo.RegisterCreatedObjectUndo(group, "Create prototype safety root");
            // New scene root, never parented under or inserted into ApartmentRoot.
            group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.transform.localScale = Vector3.one;
            var boundary = new GameObject(BoundaryName);
            SceneManager.MoveGameObjectToScene(boundary, scene);
            Undo.RegisterCreatedObjectUndo(boundary, "Create entrance exterior boundary");
            boundary.transform.SetParent(group.transform, false);
            boundary.transform.localPosition = Center;
            boundary.transform.localRotation = Quaternion.identity;
            boundary.transform.localScale = Vector3.one;
            boundary.layer = All<CharacterController>(scene).Single().gameObject.layer;
            var collider = Undo.AddComponent<BoxCollider>(boundary);
            collider.center = Vector3.zero;
            collider.size = Size;
            collider.isTrigger = false;
            collider.enabled = true;
            collider.includeLayers = 0;
            collider.excludeLayers = 0;
            EditorUtility.SetDirty(collider);

            Report installed = Inspect(scene, true);
            RequirePass(installed);
            RequireObjectsUnchanged(scene, original, true);
            RequireUnchanged(disk, targetHash);
            EditorSceneManager.MarkSceneDirty(scene);
            saveAttempted = true;
            if (!EditorSceneManager.SaveScene(scene, ScenePath, false))
                throw new IOException("Unity did not confirm a successful VR scene save.");
            saved = true;
            Undo.CollapseUndoOperations(undoGroup);
            RequireProtectedUnchanged(disk);
            RequireObjectsUnchanged(scene, original, true);
            Report after = Inspect(scene, true);
            RequirePass(after);
            Log(after);
            Debug.Log("M2-B3 installed and saved ONLY " + ScenePath +
                ". Protected file hashes and all original GameObject/component snapshots unchanged. " +
                "Play Mode edge collision, return movement and Quest Link safety are NOT verified.");
        }
        catch (Exception exception)
        {
            // Remove only this operation's additions before any save attempt; never restore asset files.
            if (!saveAttempted && undoGroup >= 0)
            {
                try { Undo.RevertAllDownToGroup(undoGroup); }
                catch (Exception undoError) { Debug.LogError("M2-B3 Undo failed: " + undoError.Message); }
            }
            Debug.LogError("M2-B3 stopped: " + exception.Message +
                (saved ? " VR scene was saved; inspect the reported failure." : saveAttempted ?
                " A save was attempted; disk/scene state requires manual review." : " No VR scene save attempted.") +
                " No existing asset is deleted, restored or overwritten by recovery. Do not rerun over existing safety objects.");
        }
        finally
        {
            foreach (FileStream sourceLock in sourceLocks) sourceLock.Dispose();
            installing = false;
        }
    }

    [MenuItem("Apartment Phase 2/M2-B3/Validate Entrance Safety Boundary (Read Only)")]
    public static void ValidateEntranceSafetyBoundary()
    {
        try
        {
            Scene scene = RequireScene();
            bool dirty = scene.isDirty;
            var original = CaptureObjects(scene);
            var disk = CaptureProtectedFiles();
            string targetHash = Hash(Absolute(ScenePath));
            Report report = Inspect(scene, true);
            RequireObjectsUnchanged(scene, original, false);
            RequireUnchanged(disk, targetHash);
            if (scene.isDirty != dirty) throw new InvalidOperationException("Scene dirty state changed unexpectedly during inspection.");
            Log(report);
            if (dirty) Debug.LogWarning("M2-B3 inspected unsaved in-memory scene state only. No assets saved.");
        }
        catch (Exception exception) { Debug.LogError("M2-B3 read-only validation stopped: " + exception.Message); }
    }

    static Scene RequireScene()
    {
        if (Application.unityVersion != "6000.6.4f1") throw new InvalidOperationException("Use Unity 6000.6.4f1.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Leave Play Mode and wait for compilation/import first.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("Manually open only ApartmentPhase2_VR.unity, not additively. This tool never opens scenes.");
        foreach (string path in Sources.Concat(new[] { ScenePath, GrayboxLayout, FurnitureLayout, RuntimePath }))
            if (!File.Exists(Absolute(path)) || !File.Exists(Absolute(path + ".meta")))
                throw new FileNotFoundException("Required source/dependency or metadata is missing: " + path);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<MonoScript>(RuntimePath)?.GetClass() != typeof(ApartmentVRLocomotion))
            throw new InvalidOperationException("VR scene/runtime dependency is not successfully imported/compiled.");
        return scene;
    }

    static void RequireClean(Scene scene)
    {
        if (scene.isDirty) throw new InvalidOperationException("Unsaved scene edits exist. Review/save them yourself; nothing is silently saved or discarded.");
    }
    static void RequireNewObjects(Scene scene)
    {
        if (Objects(scene).Any(o => o.name == GroupName || o.name == BoundaryName))
            throw new InvalidOperationException("Safety group or boundary already exists (including inactive/nested objects). No reuse, overwrite or duplicate installation.");
    }

    sealed class Report
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> limits = new List<string>();
        public int roots, boundaries, routes, hits60, hits80, overlaps;
        public float estimatedFootMargin;
    }
    static Report Inspect(Scene scene, bool installed)
    {
        var report = new Report();
        var objects = Objects(scene).ToArray();
        GameObject[] groups = objects.Where(o => o.name == GroupName).ToArray();
        GameObject[] boundaries = objects.Where(o => o.name == BoundaryName).ToArray();
        report.roots = groups.Length; report.boundaries = boundaries.Length;
        if (installed && (groups.Length != 1 || boundaries.Length != 1))
            report.errors.Add("Exactly one SimulationSafety and one EntranceExteriorBoundary is required, including inactive objects.");
        if (!installed && (groups.Length != 0 || boundaries.Length != 0)) report.errors.Add("Safety objects already exist.");
        var controllers = All<CharacterController>(scene);
        var movers = All<ApartmentVRLocomotion>(scene);
        if (controllers.Length != 1 || movers.Length != 1 || !controllers[0].enabled || !movers[0].isActiveAndEnabled ||
            controllers[0].gameObject.name != "VRPlayer" || controllers[0].transform.parent != null ||
            movers[0].gameObject != controllers[0].gameObject)
            report.errors.Add("Expected one enabled VRPlayer CharacterController and its existing locomotion component.");
        var apartment = objects.Where(o => o.name == "ApartmentRoot").ToArray();
        if (apartment.Length != 1 || apartment[0].transform.parent != null || !Identity(apartment[0].transform, Vector3.zero))
            report.errors.Add("ApartmentRoot must retain the identity scene-root transform used by route data.");
        foreach (GameObject obj in objects)
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) != 0) report.errors.Add("Missing script: " + obj.name);
        var boxes = All<BoxCollider>(scene);
        RequireBox(boxes, "Floor_Entrance_1", new Bounds(new Vector3(.95f, -.05f, .875f), new Vector3(2.70f, .10f, 1.15f)), report);
        RequireBox(boxes, "Floor_Entrance_2", new Bounds(new Vector3(.325f, -.05f, .225f), new Vector3(1.45f, .10f, .15f)), report);
        RequireBox(boxes, "Threshold_Door_MainEntrance", new Bounds(new Vector3(0f, -.05f, 0f), new Vector3(.80f, .10f, .30f)), report);
        RequireBox(boxes, "Door_MainEntrance_SideA", new Bounds(new Vector3(-.45f, 1.40f, 0f), new Vector3(.10f, 2.80f, .30f)), report);
        RequireBox(boxes, "Door_MainEntrance_SideB", new Bounds(new Vector3(.80f, 1.40f, 0f), new Vector3(.80f, 2.80f, .30f)), report);
        RequireBox(boxes, "Door_MainEntrance_Lintel", new Bounds(new Vector3(0f, 2.45f, 0f), new Vector3(.80f, .70f, .30f)), report);
        // All approved doorway geometry ends at Z=-0.15; candidate ends at Z=-0.35.
        if (Candidate.max.z >= -.15f - Epsilon) report.errors.Add("Candidate intrudes into the doorway thickness.");
        if (controllers.Length == 1 && movers.Length == 1)
        {
            var cc = controllers[0]; var mover = movers[0];
            if (Vector3.Distance(cc.transform.lossyScale, Vector3.one) > Epsilon || Vector3.Angle(cc.transform.up, Vector3.up) > .01f ||
                Mathf.Abs(cc.radius - mover.capsuleRadius) > Epsilon || Mathf.Abs(cc.skinWidth - mover.skinWidth) > Epsilon)
                report.errors.Add("Player scale/upright pose or capsule initialization parameters are inconsistent.");
            if (IntersectsCandidate(cc.transform.TransformPoint(cc.center), cc.height, cc.radius))
                report.errors.Add("Current player capsule would overlap the new boundary; installation must not move the player to compensate.");
            report.estimatedFootMargin = Candidate.max.z + cc.radius - cc.skinWidth - (-.15f);
            if (cc.radius < .30f - Epsilon || cc.skinWidth <= 0f || cc.skinWidth >= cc.radius || report.estimatedFootMargin < .05f - Epsilon)
                report.errors.Add("Current capsule cannot provide the assumed minimum floor-edge margin. No parameters are changed automatically.");
            if (Physics.GetIgnoreLayerCollision(cc.gameObject.layer, cc.gameObject.layer))
                report.errors.Add("Player layer ignores collisions with the proposed boundary layer.");
            if (!cc.detectCollisions || (cc.excludeLayers.value & (1 << cc.gameObject.layer)) != 0)
                report.errors.Add("CharacterController collision detection/layer exclusions would bypass the boundary.");
            if (mover.debugMode) report.limits.Add("Debug Mode is saved ON; desktop testing does not verify Quest Link.");
        }
        if (installed && groups.Length == 1 && boundaries.Length == 1)
        {
            var group = groups[0]; var boundary = boundaries[0];
            if (group.transform.parent != null || !Identity(group.transform, Vector3.zero) || !group.activeSelf ||
                group.GetComponents<Component>().Length != 1 || group.transform.childCount != 1 ||
                boundary.transform.parent != group.transform || !Identity(boundary.transform, Center) || !boundary.activeInHierarchy)
                report.errors.Add("Safety hierarchy/transforms/components differ from the approved two-object structure.");
            var boundaryBoxes = boundary.GetComponents<BoxCollider>();
            if (boundaryBoxes.Length != 1 || boundary.GetComponents<Component>().Length != 2 || boundary.transform.childCount != 0 ||
                boundary.GetComponentsInChildren<Renderer>(true).Length != 0 || boundary.GetComponentsInChildren<MeshFilter>(true).Length != 0 ||
                boundary.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                report.errors.Add("Boundary must contain ONLY Transform + BoxCollider, with no renderer, mesh, Rigidbody or child objects.");
            if (boundaryBoxes.Length == 1)
            {
                var box = boundaryBoxes[0];
                if (!box.enabled || box.isTrigger || Vector3.Distance(box.center, Vector3.zero) > Epsilon ||
                    Vector3.Distance(box.size, Size) > Epsilon || !SameBounds(WorldBounds(box), Candidate) ||
                    box.includeLayers.value != 0 || box.excludeLayers.value != 0)
                    report.errors.Add("Boundary collider centre/size/enabled/trigger/layer override settings are invalid.");
                if (controllers.Length == 1 && boundary.layer != controllers[0].gameObject.layer)
                    report.errors.Add("Boundary must use the validated player collision layer.");
            }
        }
        foreach (Collider collider in All<Collider>(scene))
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy || collider.name == BoundaryName ||
                (controllers.Length == 1 && collider.transform.IsChildOf(controllers[0].transform))) continue;
            Bounds bounds = collider is BoxCollider box ? WorldBounds(box) : collider.bounds;
            if (PositiveOverlap(bounds, Candidate))
            { report.overlaps++; report.errors.Add("Boundary candidate overlaps existing collider bounds: " + collider.name); }
        }
        CheckRoutes(report);
        return report;
    }

    static void RequireBox(BoxCollider[] boxes, string name, Bounds expected, Report report)
    {
        var matches = boxes.Where(b => b.name == name).ToArray();
        if (matches.Length != 1 || !matches[0].enabled || matches[0].isTrigger || !matches[0].gameObject.activeInHierarchy ||
            !SameBounds(WorldBounds(matches[0]), expected)) report.errors.Add("Entrance baseline collider differs or is missing: " + name);
    }
    static bool Identity(Transform transform, Vector3 position) =>
        Vector3.Distance(transform.localPosition, position) <= Epsilon &&
        Vector3.Distance(transform.localScale, Vector3.one) <= Epsilon && Quaternion.Angle(transform.localRotation, Quaternion.identity) <= .01f;
    static Bounds WorldBounds(BoxCollider box)
    {
        var bounds = new Bounds(box.transform.TransformPoint(box.center), Vector3.zero);
        Vector3 half = box.size * .5f;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    bounds.Encapsulate(box.transform.TransformPoint(box.center + Vector3.Scale(half, new Vector3(x, y, z))));
        return bounds;
    }
    static bool SameBounds(Bounds a, Bounds b) => Vector3.Distance(a.center, b.center) <= Epsilon && Vector3.Distance(a.size, b.size) <= Epsilon;
    static bool PositiveOverlap(Bounds a, Bounds b) =>
        Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > Epsilon &&
        Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y) > Epsilon &&
        Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > Epsilon;

    [Serializable] sealed class Point { public string name = ""; public Vector3 position = Vector3.zero; }
    [Serializable] sealed class GrayboxData { public Point[] route = Array.Empty<Point>(); }
    [Serializable] sealed class InspectionRoute { public string name = ""; public Point[] points = Array.Empty<Point>(); }
    [Serializable] sealed class FurnitureData
    {
        public InspectionRoute[] routes = Array.Empty<InspectionRoute>();
        public float probeHeight = 0f, probeCentreY = 0f, sampleStep = 0f;
        public float[] probeWidths = Array.Empty<float>();
    }
    static void CheckRoutes(Report report)
    {
        var graybox = JsonUtility.FromJson<GrayboxData>(File.ReadAllText(Absolute(GrayboxLayout)));
        var furniture = JsonUtility.FromJson<FurnitureData>(File.ReadAllText(Absolute(FurnitureLayout)));
        if (graybox?.route == null || graybox.route.Length < 2 || furniture?.routes == null || furniture.routes.Length == 0)
        { report.errors.Add("Original route data is absent/invalid; cannot verify boundary impact."); return; }
        if (Mathf.Abs(furniture.probeHeight - 1.70f) > Epsilon || Mathf.Abs(furniture.probeCentreY - .95f) > Epsilon ||
            Mathf.Abs(furniture.sampleStep - .04f) > Epsilon || furniture.probeWidths == null ||
            furniture.probeWidths.Length != 2 || Mathf.Abs(furniture.probeWidths[0] - .60f) > Epsilon ||
            Mathf.Abs(furniture.probeWidths[1] - .80f) > Epsilon)
        { report.errors.Add("Route probe profile changed; this installer cannot silently substitute inspection parameters."); return; }
        CheckRoute("M1-A", graybox.route, report);
        foreach (var route in furniture.routes)
        {
            if (route == null || route.points == null || route.points.Length < 2)
            { report.errors.Add("Invalid M1-B route."); continue; }
            CheckRoute(route.name, route.points, report);
        }
    }
    static void CheckRoute(string name, Point[] points, Report report)
    {
        report.routes++;
        bool hit60 = false, hit80 = false;
        for (int i = 0; i < points.Length; i++)
            if (points[i] == null || !Finite(points[i].position)) { report.errors.Add("Non-finite/missing route point: " + name); return; }
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 a = points[i - 1].position, b = points[i].position;
            float distance = Vector3.Distance(a, b);
            if (distance > 100f) { report.errors.Add("Unexpectedly long route segment: " + name); return; }
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / .04f));
            for (int sample = 0; sample <= steps; sample++)
            {
                Vector3 feet = Vector3.Lerp(a, b, (float)sample / steps);
                hit60 |= CapsuleIntersects(feet, .30f);
                hit80 |= CapsuleIntersects(feet, .40f);
            }
        }
        if (hit60) { report.hits60++; report.errors.Add("New boundary intersects a recorded 0.60 m route: " + name); }
        if (hit80) { report.hits80++; report.limits.Add("0.80 m probe intersects exterior boundary on " + name + "; threshold/outbound use is restricted. Do not claim universal route clearance."); }
    }
    static bool CapsuleIntersects(Vector3 feet, float radius)
    {
        // Same 1.70 m / centre-Y 0.95 m inspection envelope; exact capsule vs candidate AABB, no Physics simulation.
        return IntersectsCandidate(feet + Vector3.up * .95f, 1.70f, radius);
    }
    static bool IntersectsCandidate(Vector3 center, float height, float radius)
    {
        float half = Mathf.Max(0f, height * .5f - radius);
        float dx = Mathf.Max(Candidate.min.x - center.x, center.x - Candidate.max.x, 0f);
        float dz = Mathf.Max(Candidate.min.z - center.z, center.z - Candidate.max.z, 0f);
        float dy = Mathf.Max(Candidate.min.y - (center.y + half), center.y - half - Candidate.max.y, 0f);
        return dx * dx + dy * dy + dz * dz < (radius - Epsilon) * (radius - Epsilon);
    }
    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    static Dictionary<EntityId, string> CaptureObjects(Scene scene)
    {
        // Serialization callbacks could mutate a component while taking a supposedly read-only snapshot.
        // Refuse such scenes before serializing anything rather than invoking unknown user/SDK callbacks.
        if (All<Component>(scene).Any(component => component is ISerializationCallbackReceiver))
            throw new InvalidOperationException("Scene contains a serialization callback component; safe read-only snapshots cannot be guaranteed. No installation attempted.");
        var records = new Dictionary<EntityId, string>();
        foreach (GameObject obj in Objects(scene))
        {
            records.Add(obj.GetEntityId(), EditorJsonUtility.ToJson(obj));
            foreach (Component component in obj.GetComponents<Component>())
            {
                if (component == null) throw new InvalidOperationException("Missing component: " + obj.name);
                records.Add(component.GetEntityId(), EditorJsonUtility.ToJson(component));
            }
        }
        return records;
    }
    static void RequireObjectsUnchanged(Scene scene, Dictionary<EntityId, string> before, bool allowNewSafety)
    {
        var current = CaptureObjects(scene);
        foreach (var record in before)
            if (!current.TryGetValue(record.Key, out string json) || json != record.Value)
                throw new InvalidOperationException("An original scene object/component changed or disappeared (entity ID " + record.Key + ").");
        var allowed = new HashSet<EntityId>();
        if (allowNewSafety)
            foreach (GameObject obj in Objects(scene).Where(o => o.name == GroupName || o.name == BoundaryName))
            {
                allowed.Add(obj.GetEntityId());
                foreach (Component component in obj.GetComponents<Component>()) allowed.Add(component.GetEntityId());
            }
        foreach (EntityId id in current.Keys)
            if (!before.ContainsKey(id) && !allowed.Contains(id)) throw new InvalidOperationException("Unexpected new scene object/component: " + id);
    }
    static Dictionary<string, string> CaptureProtectedFiles()
    {
        var records = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in new[] { "Assets", "Docs", "Packages", "ProjectSettings" })
            foreach (string path in Directory.GetFiles(Absolute(folder), "*", SearchOption.AllDirectories))
                if (!string.Equals(Path.GetFullPath(path), Absolute(ScenePath), StringComparison.OrdinalIgnoreCase))
                    records.Add(Path.GetFullPath(path), Hash(path));
        return records; // Includes target .meta, source scenes, scripts, materials, layouts, font and XR configuration.
    }
    static void RequireProtectedUnchanged(Dictionary<string, string> before)
    {
        var after = CaptureProtectedFiles();
        if (before.Count != after.Count) throw new IOException("Protected file set changed during installation/validation.");
        foreach (var record in before)
            if (!after.TryGetValue(record.Key, out string hash) || hash != record.Value)
                throw new IOException("Protected file changed: " + record.Key);
    }
    static void RequireUnchanged(Dictionary<string, string> disk, string targetHash)
    {
        RequireProtectedUnchanged(disk);
        if (Hash(Absolute(ScenePath)) != targetHash) throw new IOException("VR scene changed externally; no save permitted.");
    }
    static string Absolute(string path) => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static IEnumerable<GameObject> Objects(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    static void RequirePass(Report report)
    {
        if (report.errors.Count != 0) throw new InvalidOperationException(string.Join("; ", report.errors));
    }
    static void Log(Report report)
    {
        foreach (string error in report.errors) Debug.LogError("M2-B3 MODEL/CONFIG ERROR: " + error);
        foreach (string limit in report.limits) Debug.LogWarning("M2-B3 DESIGN LIMIT: " + limit);
        Debug.Log("M2-B3 static check: safetyRoots=" + report.roots + ", boundaries=" + report.boundaries +
            ", existingColliderOverlaps=" + report.overlaps + ", routes=" + report.routes + ", routeHits0.60=" + report.hits60 +
            ", routeHits0.80=" + report.hits80 + ", estimatedFootMargin=" + report.estimatedFootMargin.ToString("F3") +
            " m, errors=" + report.errors.Count + ", limits=" + report.limits.Count +
            ". Validation saves no assets. UNVERIFIED: Physics edge/diagonal collisions, walking away from guard, frame stalls, room-scale head tracking and Quest Link.");
    }
}
#endif
