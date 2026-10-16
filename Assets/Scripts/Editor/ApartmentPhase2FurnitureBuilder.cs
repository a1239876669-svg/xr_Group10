#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor only. Never writes the source scene, package settings or runtime systems.
public static class ApartmentPhase2FurnitureBuilder
{
    const string SourceScene = "Assets/Scenes/test/ApartmentPhase2.unity";
    const string TargetScene = "Assets/Scenes/test/ApartmentPhase2_Furnished.unity";
    const string LayoutPath = "Assets/Apartment/Furniture/ApartmentFurnitureLayout.json";
    const string BaseLayoutPath = "Assets/Apartment/ApartmentGrayboxLayout.json";
    const string FloorPlanPath = "Docs/References/ApartmentFloorPlan.png";
    const string MaterialFolder = "Assets/Apartment/Materials/";
    const float Epsilon = .00001f;
    static readonly string[] ExistingParents = {
        "ApartmentRoot/Furniture", "ApartmentRoot/Anchors", "ApartmentRoot/AssumptionMarkers"
    };

    [Serializable] public class Layout
    {
        public int schemaVersion;
        public string sourceScene, targetScene, sourceLayout, floorPlan, referenceCommit;
        public Furniture[] furniture;
        public Node[] nodes;
        public Route[] routes;
        public Vector3 spawnCandidate;
        public PullTest chairPullTest;
        public float[] probeWidths;
        public float probeHeight, probeCentreY, sampleStep;
    }
    [Serializable] public class Furniture
    {
        public string name, room, path, orientation;
        public Vector3 position, size;
        public bool assumed;
    }
    [Serializable] public class Node
    {
        public string path, kind, material;
        public Vector3 position, size;
        public bool collider;
    }
    [Serializable] public class Route { public string name; public Point[] points; }
    [Serializable] public class Point { public string name; public Vector3 position; }
    [Serializable] public class PullTest { public float northDistance, southDistance, invalidNorthDistance; }
    [Serializable] public class BaseLayout
    {
        public int schemaVersion;
        public BaseNode[] objects;
        public Portal[] portals;
        public Point[] route;
    }
    [Serializable] public class BaseNode
    {
        public string path, kind, material;
        public Vector3 position, size;
        public bool collider;
    }
    [Serializable] public class Portal
    {
        public string name, axis;
        public Vector3 position;
        public float width, height, thickness;
    }
    class Solid
    {
        public Bounds bounds;
        public string path;
        public Furniture owner;
        public BoxCollider collider;
    }
    struct RouteResult
    {
        public int samples, blocked, contact, unsupported;
        public string firstBlock;
    }

    static string Absolute(string relative)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (Path.IsPathRooted(relative) || !path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path is outside the project: " + relative);
        return path;
    }
    static bool IsNewPath(string path)
    {
        foreach (string parent in ExistingParents)
            if (path == parent + "/M1B" || path.StartsWith(parent + "/M1B/", StringComparison.Ordinal)) return true;
        return false;
    }
    static bool Finite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
    static void RequireSize(Vector3 size, string path)
    {
        if (!Finite(size) || size.x <= 0 || size.y <= 0 || size.z <= 0)
            throw new InvalidOperationException("Invalid size: " + path);
    }
    static Layout ReadLayout()
    {
        foreach (string path in new[] { SourceScene, SourceScene + ".meta", BaseLayoutPath, FloorPlanPath, LayoutPath })
            if (!File.Exists(Absolute(path))) throw new FileNotFoundException("Required source missing: " + path);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Absolute(LayoutPath)));
        if (layout == null || layout.schemaVersion != 1 || layout.sourceScene != SourceScene ||
            layout.targetScene != TargetScene || layout.sourceLayout != BaseLayoutPath ||
            layout.floorPlan != FloorPlanPath || layout.furniture == null || layout.furniture.Length != 23 ||
            layout.nodes == null || layout.routes == null || layout.chairPullTest == null ||
            layout.probeWidths == null || layout.probeWidths.Length != 2 ||
            Mathf.Abs(layout.probeWidths[0] - .60f) > Epsilon ||
            Mathf.Abs(layout.probeWidths[1] - .80f) > Epsilon ||
            Mathf.Abs(layout.sampleStep - .04f) > Epsilon ||
            Mathf.Abs(layout.probeHeight - 1.70f) > Epsilon ||
            Mathf.Abs(layout.probeCentreY - .95f) > Epsilon)
            throw new InvalidOperationException("Missing, changed or unsupported M1-B layout contract.");
        var paths = new HashSet<string>(ExistingParents, StringComparer.Ordinal);
        foreach (var node in layout.nodes)
        {
            if (string.IsNullOrEmpty(node.path) || node.path.Contains("..") ||
                !IsNewPath(node.path) ||
                !paths.Add(node.path) || !Finite(node.position))
                throw new InvalidOperationException("Invalid or duplicate new object path: " + node.path);
            int slash = node.path.LastIndexOf('/');
            if (slash < 0 || !paths.Contains(node.path.Substring(0, slash)))
                throw new InvalidOperationException("Parent must precede child: " + node.path);
            RequireSize(node.size, node.path);
            if (node.kind != "empty" && node.kind != "cube" && node.kind != "cylinder" && node.kind != "box")
                throw new InvalidOperationException("Unsupported primitive: " + node.path);
            if ((node.kind == "empty" && node.collider) || (node.kind == "cylinder" && node.collider) ||
                (node.kind == "box" && !node.collider))
                throw new InvalidOperationException("Unsupported collider contract: " + node.path);
        }
        var furniturePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in layout.furniture)
        {
            RequireSize(f.size, f.path);
            if (!furniturePaths.Add(f.path) || !paths.Contains(f.path) || !Finite(f.position))
                throw new InvalidOperationException("Invalid furniture reference: " + f.path);
        }
        var routeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in layout.routes)
        {
            if (string.IsNullOrEmpty(route.name) || !routeNames.Add(route.name) ||
                route.points == null || route.points.Length < 2)
                throw new InvalidOperationException("Invalid route.");
            foreach (var point in route.points)
                if (!Finite(point.position) || Mathf.Abs(point.position.y) > Epsilon)
                    throw new InvalidOperationException("Route points must be floor coordinates.");
        }
        return layout;
    }
    static BaseLayout ReadBase()
    {
        var layout = JsonUtility.FromJson<BaseLayout>(File.ReadAllText(Absolute(BaseLayoutPath)));
        if (layout == null || layout.schemaVersion != 1 || layout.objects == null ||
            layout.portals == null || layout.route == null)
            throw new InvalidOperationException("Invalid M1-A reference layout.");
        return layout;
    }
    static Dictionary<string, Transform> ScenePaths(Scene scene)
    {
        var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                string path = HierarchyPath(transform);
                if (result.ContainsKey(path)) throw new InvalidOperationException("Duplicate hierarchy path: " + path);
                result.Add(path, transform);
            }
        return result;
    }
    static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
    static Vector3 NodeScale(Node node)
    {
        if (node.kind == "empty" || node.kind == "box") return Vector3.one;
        return node.kind == "cylinder" ? new Vector3(node.size.x, node.size.y / 2, node.size.z) : node.size;
    }

    [MenuItem("Apartment Phase 2/M1-B/Create Furnished Copy (Never Overwrite)")]
    public static void CreateFurnishedCopy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("Exit Play Mode first."); return; }
        Scene previous = SceneManager.GetActiveScene();
        Scene created = default;
        bool copied = false, saved = false;
        try
        {
            // Check both asset and sidecar BEFORE CopyAsset, scene creation or saving.
            if (File.Exists(Absolute(TargetScene)) || Directory.Exists(Absolute(TargetScene)) ||
                File.Exists(Absolute(TargetScene + ".meta")) || AssetDatabase.LoadMainAssetAtPath(TargetScene))
                throw new InvalidOperationException("Target already exists. STOP; no overwrite: " + TargetScene);
            var layout = ReadLayout();
            ReadBase();
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene))
                throw new InvalidOperationException("Source scene has not been imported.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == SourceScene && loaded.isDirty)
                    throw new InvalidOperationException("Source scene has unsaved changes; preserve them before generating.");
            }
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var node in layout.nodes)
            {
                if (node.kind != "cube" && node.kind != "cylinder") continue;
                if (materials.ContainsKey(node.material)) continue;
                if (node.material != "FurnitureWood" && node.material != "FurnitureFabric" &&
                    node.material != "FurnitureWorktop" && node.material != "FurnitureAppliance" &&
                    node.material != "Anchor" && node.material != "Assumption")
                    throw new InvalidOperationException("Unapproved material reference: " + node.material);
                string path = MaterialFolder + node.material + ".mat";
                if (!File.Exists(Absolute(path + ".meta"))) throw new FileNotFoundException("Material meta missing: " + path);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) throw new InvalidOperationException("Material not imported: " + path);
                materials.Add(node.material, material);
            }
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
                throw new IOException("CopyAsset failed; inspect the target before trying again.");
            copied = true;
            created = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Additive);
            if (!SceneManager.SetActiveScene(created))
                throw new InvalidOperationException("Cannot activate the new scene; stop before creating objects.");
            var paths = ScenePaths(created);
            foreach (string parent in ExistingParents)
                if (!paths.ContainsKey(parent)) throw new InvalidOperationException("Source group missing: " + parent);
            if (paths["ApartmentRoot/Furniture"].childCount != 0)
                throw new InvalidOperationException("M1-A Furniture is not empty; stop to avoid duplicate modelling.");
            foreach (var node in layout.nodes)
            {
                if (paths.ContainsKey(node.path)) throw new InvalidOperationException("New object already exists: " + node.path);
                GameObject go;
                if (node.kind == "cube" || node.kind == "cylinder")
                {
                    go = GameObject.CreatePrimitive(node.kind == "cube" ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                    foreach (var collider in go.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                    go.GetComponent<MeshRenderer>().sharedMaterial = materials[node.material];
                }
                else go = new GameObject();
                go.name = node.path.Substring(node.path.LastIndexOf('/') + 1);
                go.transform.SetParent(paths[node.path.Substring(0, node.path.LastIndexOf('/'))], false);
                go.transform.localPosition = node.position;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = NodeScale(node);
                if (node.collider)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.size = node.kind == "box" ? node.size : Vector3.one;
                    box.center = Vector3.zero;
                    box.isTrigger = false;
                }
                paths.Add(node.path, go.transform);
            }
            if (!EditorSceneManager.SaveScene(created, TargetScene)) throw new IOException("Saving the new scene failed.");
            saved = true;
            Debug.Log("M1-B furnished copy generated and saved: " + TargetScene +
                ". Open it alone and run the read-only validator. No XR or Physics test was run.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (copied && !saved)
                Debug.LogError("A new target copy may remain incomplete. No cleanup, retry or overwrite was performed: " + TargetScene);
        }
        finally
        {
            if (created.IsValid() && created.isLoaded) EditorSceneManager.CloseScene(created, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static bool Near(Vector3 a, Vector3 b) { return (a - b).sqrMagnitude <= Epsilon * Epsilon; }
    static bool Identity(Transform t)
    {
        return Quaternion.Angle(t.rotation, Quaternion.identity) <= .001f &&
            t.lossyScale.x > 0 && t.lossyScale.y > 0 && t.lossyScale.z > 0;
    }
    static int Error(string text, UnityEngine.Object context = null)
    { Debug.LogError("M1-B MODELLING ERROR: " + text, context); return 1; }
    static Bounds BoxBounds(BoxCollider box)
    {
        return new Bounds(box.transform.TransformPoint(box.center), Vector3.Scale(box.size, box.transform.lossyScale));
    }
    // 0 = separate, 1 = touching (zero margin), 2 = positive-volume penetration.
    static int Relation(Bounds a, Bounds b)
    {
        Vector3 d = Vector3.Min(a.max, b.max) - Vector3.Max(a.min, b.min);
        if (d.x < -Epsilon || d.y < -Epsilon || d.z < -Epsilon) return 0;
        return d.x > Epsilon && d.y > Epsilon && d.z > Epsilon ? 2 : 1;
    }
    static bool Supported(Vector3 ground, List<Solid> floors)
    {
        foreach (var floor in floors)
        {
            Vector3 lo = floor.bounds.min, hi = floor.bounds.max;
            if (Mathf.Abs(hi.y) <= Epsilon && ground.x >= lo.x - Epsilon &&
                ground.x <= hi.x + Epsilon && ground.z >= lo.z - Epsilon && ground.z <= hi.z + Epsilon)
                return true;
        }
        return false;
    }
    static RouteResult SampleRoute(Route route, float width, Layout layout, List<Solid> solids, List<Solid> floors)
    {
        var result = new RouteResult();
        for (int i = 1; i < route.points.Length; i++)
        {
            Vector3 a = route.points[i - 1].position, b = route.points[i].position;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / layout.sampleStep));
            for (int j = 0; j <= steps; j++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)j / steps);
                var probe = new Bounds(p + Vector3.up * layout.probeCentreY,
                    new Vector3(width, layout.probeHeight, width));
                bool blocked = false, contact = false;
                foreach (var solid in solids)
                {
                    int relation = Relation(probe, solid.bounds);
                    if (relation == 2)
                    {
                        blocked = true;
                        if (result.firstBlock == null) result.firstBlock = solid.path;
                    }
                    else if (relation == 1) contact = true;
                }
                result.samples++;
                if (blocked) result.blocked++;
                if (contact) result.contact++;
                if (!Supported(p, floors)) result.unsupported++;
            }
        }
        return result;
    }
    static void ReportRoute(Route route, float width, Layout layout, List<Solid> solids, List<Solid> floors, string state)
    {
        var result = SampleRoute(route, width, layout, solids, floors);
        string text = "M1-B " + state + " / " + route.name + " / " + width.ToString("F2") +
            "m: samples=" + result.samples + ", blocked=" + result.blocked + ", zeroMargin=" +
            result.contact + ", unsupported=" + result.unsupported +
            (result.firstBlock == null ? "" : "; first obstacle=" + result.firstBlock);
        if (result.blocked > 0 || result.contact > 0 || result.unsupported > 0)
            Debug.LogWarning("DESIGN LIMIT / static route: " + text);
        else Debug.Log(text + ". Geometric sample only; not a VR or accessibility pass.");
    }
    static List<Solid> Pulled(List<Solid> solids, float north, float south)
    {
        // Pure data copies: never move scene objects or change their colliders.
        var result = new List<Solid>();
        foreach (var solid in solids)
        {
            var copy = new Solid { path = solid.path, bounds = solid.bounds, owner = solid.owner, collider = solid.collider };
            if (copy.owner != null && copy.owner.name.StartsWith("DiningChairNorth", StringComparison.Ordinal))
                copy.bounds = new Bounds(copy.bounds.center + Vector3.forward * north, copy.bounds.size);
            if (copy.owner != null && copy.owner.name.StartsWith("DiningChairSouth", StringComparison.Ordinal))
                copy.bounds = new Bounds(copy.bounds.center - Vector3.forward * south, copy.bounds.size);
            result.Add(copy);
        }
        return result;
    }
    static int PairPenetrations(List<Solid> solids)
    {
        int count = 0;
        for (int i = 0; i < solids.Count; i++)
            for (int j = i + 1; j < solids.Count; j++)
            {
                var a = solids[i]; var b = solids[j];
                if (a.owner == b.owner || (a.owner == null && b.owner == null)) continue;
                if (Relation(a.bounds, b.bounds) == 2) count++;
            }
        return count;
    }
    static void ReportPull(Layout layout, List<Solid> solids, List<Solid> floors)
    {
        var test = layout.chairPullTest;
        var pulled = Pulled(solids, test.northDistance, test.southDistance);
        Debug.Log("M1-B virtual chair pull N=" + test.northDistance + ", S=" + test.southDistance +
            "m; cross-owner penetrations=" + PairPenetrations(pulled) + ". No scene transforms changed.");
        foreach (var route in layout.routes)
            if (route.name == "M1B_Core" || route.name == "M1B_Balcony")
                foreach (float width in layout.probeWidths)
                    ReportRoute(route, width, layout, pulled, floors, "VIRTUAL PULLED CHAIRS");
        var invalid = Pulled(solids, test.invalidNorthDistance, 0);
        Debug.LogWarning("DESIGN LIMIT / INVALID TEST POSE: north chairs pulled " + test.invalidNorthDistance +
            "m; cross-owner penetrations=" + PairPenetrations(invalid) +
            ". Expected north-wall penetration; this pose is not saved or permitted.");
    }

    [MenuItem("Apartment Phase 2/M1-B/Validate Active Furnished (Read Only)")]
    public static void ValidateActiveFurnished()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogError("Validate in Edit Mode, with the furnished scene open alone."); return; }
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != TargetScene)
                throw new InvalidOperationException("Open ApartmentPhase2_Furnished.unity as the active scene first.");
            bool dirtyBefore = scene.isDirty;
            var layout = ReadLayout();
            var reference = ReadBase();
            var paths = ScenePaths(scene);
            int errors = 0;
            foreach (var node in reference.objects)
            {
                Transform t;
                if (!paths.TryGetValue(node.path, out t)) { errors += Error("Missing M1-A object: " + node.path); continue; }
                Vector3 scale = node.kind == "cube" ? node.size : Vector3.one;
                Quaternion rotation = node.kind == "camera" ? Quaternion.Euler(90, 0, 0) :
                    node.kind == "light" ? Quaternion.Euler(50, -30, 0) : Quaternion.identity;
                if (!Near(t.localPosition, node.position) || !Near(t.localScale, scale) ||
                    Quaternion.Angle(t.localRotation, rotation) > .001f ||
                    !t.gameObject.activeSelf)
                    errors += Error("M1-A transform or activation changed: " + node.path, t);
                var box = t.GetComponent<BoxCollider>();
                if (t.GetComponents<BoxCollider>().Length != (node.collider ? 1 : 0) || (bool)box != node.collider || (box && (!box.enabled || box.isTrigger ||
                    !Near(box.size, Vector3.one) || !Near(box.center, Vector3.zero))))
                    errors += Error("M1-A collider changed: " + node.path, t);
                if (node.kind == "cube")
                {
                    var renderer = t.GetComponent<MeshRenderer>();
                    var expected = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + node.material + ".mat");
                    if (!expected || !renderer || !renderer.enabled || renderer.sharedMaterial != expected)
                        errors += Error("M1-A material or renderer changed: " + node.path, t);
                }
            }
            var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in reference.objects) expectedPaths.Add(node.path);
            foreach (var node in layout.nodes)
            {
                expectedPaths.Add(node.path);
                Transform t;
                if (!paths.TryGetValue(node.path, out t)) { errors += Error("Missing M1-B object: " + node.path); continue; }
                if (!Near(t.localPosition, node.position) || !Near(t.localScale, NodeScale(node)) ||
                    Quaternion.Angle(t.localRotation, Quaternion.identity) > .001f || !t.gameObject.activeSelf)
                    errors += Error("M1-B transform or activation differs: " + node.path, t);
                var box = t.GetComponent<BoxCollider>();
                Vector3 expectedSize = node.kind == "box" ? node.size : Vector3.one;
                if (t.GetComponents<BoxCollider>().Length != (node.collider ? 1 : 0) || (bool)box != node.collider || (box && (!box.enabled || box.isTrigger ||
                    !Near(box.size, expectedSize) || !Near(box.center, Vector3.zero))))
                    errors += Error("M1-B collider differs: " + node.path, t);
                if (node.kind == "cube" || node.kind == "cylinder")
                {
                    var renderer = t.GetComponent<MeshRenderer>();
                    var filter = t.GetComponent<MeshFilter>();
                    var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + node.material + ".mat");
                    if (!material || !renderer || !renderer.enabled || renderer.sharedMaterial != material ||
                        !filter || !filter.sharedMesh || filter.sharedMesh.name != (node.kind == "cube" ? "Cube" : "Cylinder"))
                        errors += Error("Missing primitive mesh or wrong material: " + node.path, t);
                }
            }
            foreach (var pair in paths)
            {
                if (!expectedPaths.Contains(pair.Key)) errors += Error("Unexpected scene object: " + pair.Key, pair.Value);
                if (pair.Value.GetComponent<Rigidbody>() || pair.Value.GetComponent<MonoBehaviour>())
                    errors += Error("Unexpected runtime component: " + pair.Key, pair.Value);
            }
            foreach (var f in layout.furniture)
            {
                Transform root;
                if (!paths.TryGetValue(f.path, out root)) continue;
                if (!Near(root.position, f.position)) errors += Error("Furniture floor pivot differs: " + f.name, root);
                bool hasBounds = false;
                Bounds visualBounds = default;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!filter || !filter.sharedMesh || !Identity(renderer.transform)) continue;
                    Bounds mesh = filter.sharedMesh.bounds;
                    var bounds = new Bounds(renderer.transform.TransformPoint(mesh.center),
                        Vector3.Scale(mesh.size, renderer.transform.lossyScale));
                    if (!hasBounds) { visualBounds = bounds; hasBounds = true; }
                    else visualBounds.Encapsulate(bounds);
                }
                if (!hasBounds || !Near(visualBounds.center, f.position + Vector3.up * f.size.y / 2) ||
                    !Near(visualBounds.size, f.size))
                    errors += Error("Furniture visual envelope differs: " + f.name, root);
            }
            var solids = new List<Solid>(); var floors = new List<Solid>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    var box = collider as BoxCollider;
                    if (!box || !box.enabled || box.isTrigger || !box.gameObject.activeInHierarchy ||
                        !Identity(box.transform))
                    { errors += Error("Unsupported, inactive or rotated collider: " + collider.name, collider); continue; }
                    var solid = new Solid { bounds = BoxBounds(box), path = HierarchyPath(box.transform), collider = box };
                    foreach (var f in layout.furniture)
                        if (solid.path.StartsWith(f.path + "/", StringComparison.Ordinal)) { solid.owner = f; break; }
                    if (solid.bounds.max.y <= .02f) floors.Add(solid);
                    else solids.Add(solid);
                }
            for (int i = 0; i < solids.Count; i++)
                for (int j = i + 1; j < solids.Count; j++)
                {
                    var a = solids[i]; var b = solids[j];
                    if (a.owner == b.owner || (a.owner == null && b.owner == null)) continue;
                    if (Relation(a.bounds, b.bounds) == 2)
                        errors += Error("Furniture penetrates wall/other furniture: " + a.path + " / " + b.path, a.collider);
                }
            foreach (var portal in reference.portals)
            {
                Vector3 size = portal.axis == "X"
                    ? new Vector3(portal.width - .08f, portal.height - .08f, portal.thickness + .02f)
                    : new Vector3(portal.thickness + .02f, portal.height - .08f, portal.width - .08f);
                var probe = new Bounds(portal.position + Vector3.up * (portal.height / 2), size);
                foreach (var solid in solids)
                    if (Relation(probe, solid.bounds) == 2)
                        errors += Error("Door opening blocked by collider: " + portal.name + " / " + solid.path, solid.collider);
            }
            // Check each complete furniture footprint against the union of floor modules.
            foreach (var f in layout.furniture)
            {
                int nx = Mathf.CeilToInt(f.size.x / .04f), nz = Mathf.CeilToInt(f.size.z / .04f);
                bool missing = false;
                for (int x = 0; x <= nx && !missing; x++)
                    for (int z = 0; z <= nz && !missing; z++)
                    {
                        Vector3 p = f.position + new Vector3(f.size.x * ((float)x / nx - .5f), 0,
                            f.size.z * ((float)z / nz - .5f));
                        if (!Supported(p, floors)) missing = true;
                    }
                if (missing) errors += Error("Furniture footprint lacks floor support: " + f.name);
            }
            var old = new Route { name = "M1A_Original_Reference", points = reference.route };
            var wallsOnly = solids.FindAll(s => s.owner == null);
            foreach (float width in layout.probeWidths)
            {
                ReportRoute(old, width, layout, wallsOnly, floors, "EMPTY BASELINE");
                ReportRoute(old, width, layout, solids, floors, "FURNISHED COMPARISON");
                foreach (var route in layout.routes)
                    ReportRoute(route, width, layout, solids, floors, "STORED CHAIRS");
            }
            ReportPull(layout, solids, floors);
            var spawn = new Bounds(layout.spawnCandidate + Vector3.up * layout.probeCentreY,
                new Vector3(.60f, layout.probeHeight, .60f));
            foreach (var solid in solids)
                if (Relation(spawn, solid.bounds) == 2) errors += Error("Proposed spawn is occupied: " + solid.path);
            if (!Supported(layout.spawnCandidate, floors)) errors += Error("Proposed spawn lacks floor support.");
            if (scene.isDirty != dirtyBefore) errors += Error("Unexpected dirty-state change during validation.");
            Debug.Log("M1-B static check: furniture=" + layout.furniture.Length + "; openings=" +
                reference.portals.Length + "; modellingErrors=" + errors +
                ". Routes and virtual chair-pull restrictions reported separately. No assets saved.");
            Debug.LogWarning("DESIGN LIMITS: west sofa aisle 0.75m; dining west aisle 0.70m; Bedroom1 bed/storage gap " +
                "0.70m; Bedroom2 bed-foot gap 0.55m; kitchen entry neck 0.80m with zero margin for 0.80m body; " +
                "north chairs have only 0.25m raw pull distance; south chairs pulled 0.30m obstruct the balcony branch.");
            Debug.Log("UNVERIFIED: Unity Physics/runtime motion, VR, body rotation, shopping bag, seat ergonomics, door/" +
                "cabinet swing, accessibility and OpenXR Editor exceptions. Probe boxes are data only, not scene colliders.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }
}
#endif
