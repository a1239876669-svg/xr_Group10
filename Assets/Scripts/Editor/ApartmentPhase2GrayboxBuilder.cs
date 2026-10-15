#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// M1-A only: no XR, input, runtime behaviour, Build Settings or package changes.
public static class ApartmentPhase2GrayboxBuilder
{
    const string LayoutPath = "Assets/Apartment/ApartmentGrayboxLayout.json";
    const string ScenePath = "Assets/Scenes/ApartmentPhase2.unity";
    const string MaterialFolder = "Assets/Apartment/Materials/";

    static string AbsolutePath(string projectPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", projectPath));
    }

    [Serializable] public class Layout
    {
        public int schemaVersion;
        public string source;
        public Node[] objects;
        public Portal[] portals;
        public RoutePoint[] route;
    }
    [Serializable] public class Node
    {
        public string path;
        public string kind;
        public Vector3 position;
        public Vector3 size;
        public string material;
        public bool collider;
    }
    [Serializable] public class Portal
    {
        public string name;
        public string axis;
        public Vector3 position;
        public float width;
        public float height;
        public float thickness;
    }
    [Serializable] public class RoutePoint
    {
        public string name;
        public Vector3 position;
    }

    static Layout ReadLayout()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (!asset) throw new InvalidOperationException("Layout asset not imported: " + LayoutPath);
        var layout = JsonUtility.FromJson<Layout>(asset.text);
        if (layout == null || layout.schemaVersion != 1 || layout.objects == null ||
            layout.portals == null || layout.route == null)
            throw new InvalidOperationException("Missing or unsupported graybox layout data.");
        if (!File.Exists(AbsolutePath(layout.source)))
            throw new FileNotFoundException("Floor-plan reference is missing; no files changed.", layout.source);
        return layout;
    }

    [MenuItem("Apartment Phase 2/M1-A/Create New Graybox Copy")]
    public static void CreateNewCopy()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Exit Play Mode before generating an editor-only graybox copy.");
            return;
        }
        Scene created = default;
        Scene previous = SceneManager.GetActiveScene();
        bool saved = false;
        try
        {
            var layout = ReadLayout();
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in layout.objects)
            {
                if (string.IsNullOrEmpty(node.path) || !paths.Add(node.path))
                    throw new InvalidOperationException("Empty or duplicate layout object path.");
                int slash = node.path.LastIndexOf('/');
                if (slash >= 0 && !paths.Contains(node.path.Substring(0, slash)))
                    throw new InvalidOperationException("Parent must precede child: " + node.path);
                if (node.kind != "cube" && node.kind != "empty" &&
                    node.kind != "camera" && node.kind != "light")
                    throw new InvalidOperationException("Unsupported object kind: " + node.kind);
                if (node.size.x <= 0 || node.size.y <= 0 || node.size.z <= 0)
                    throw new InvalidOperationException("Nonpositive object size: " + node.path);
                if (node.kind != "cube" || materials.ContainsKey(node.material)) continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + node.material + ".mat");
                if (!material) throw new InvalidOperationException("Material not imported: " + node.material);
                materials.Add(node.material, material);
            }

            // Always select a new path; never overwrite the supplied scene or a teammate's scene.
            string output = !File.Exists(AbsolutePath(ScenePath)) && !File.Exists(AbsolutePath(ScenePath + ".meta"))
                ? ScenePath
                : AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ApartmentPhase2_Copy.unity");
            if (File.Exists(AbsolutePath(output)) || File.Exists(AbsolutePath(output + ".meta")))
                throw new IOException("Output already exists; refusing to overwrite: " + output);

            created = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(created);
            var transforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var node in layout.objects)
            {
                var go = node.kind == "cube" ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject();
                int slash = node.path.LastIndexOf('/');
                go.name = slash < 0 ? node.path : node.path.Substring(slash + 1);
                if (slash >= 0) go.transform.SetParent(transforms[node.path.Substring(0, slash)], false);
                go.transform.localPosition = node.position;
                go.transform.localScale = node.size;
                transforms.Add(node.path, go.transform);
                if (node.kind == "cube")
                {
                    go.GetComponent<MeshRenderer>().sharedMaterial = materials[node.material];
                    if (!node.collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                }
                else if (node.kind == "camera")
                {
                    go.tag = "MainCamera";
                    go.transform.rotation = Quaternion.Euler(90, 0, 0);
                    var camera = go.AddComponent<Camera>();
                    camera.orthographic = true;
                    camera.orthographicSize = 6.2f;
                    camera.nearClipPlane = .1f;
                    camera.farClipPlane = 100f;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(.19215687f, .3019608f, .4745098f);
                    camera.stereoTargetEye = StereoTargetEyeMask.None;
                }
                else if (node.kind == "light")
                {
                    go.transform.rotation = Quaternion.Euler(50, -30, 0);
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.shadows = LightShadows.None;
                    RenderSettings.sun = light;
                }
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
            RenderSettings.skybox = null;
            if (!EditorSceneManager.SaveScene(created, output))
                throw new IOException("Unity did not save the new scene: " + output);
            saved = true;
            Debug.Log("Saved a NEW graybox scene: " + output +
                ". Existing scenes and Build Settings were not edited. Open the copy alone to inspect.");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(output));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (!saved && created.IsValid()) EditorSceneManager.CloseScene(created, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    [MenuItem("Apartment Phase 2/M1-A/Validate Active Graybox (Read Only)")]
    public static void ValidateActiveGraybox()
    {
        try
        {
            var layout = ReadLayout();
            var scene = SceneManager.GetActiveScene();
            Transform root = null;
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "ApartmentRoot") root = go.transform;
            if (!root) throw new InvalidOperationException("Open the graybox and make it the active scene first.");
            if (root.position != Vector3.zero || root.rotation != Quaternion.identity || root.localScale != Vector3.one)
                throw new InvalidOperationException("ApartmentRoot must retain its identity transform.");

            var solids = root.GetComponentsInChildren<BoxCollider>();
            int failures = 0;
            foreach (var portal in layout.portals)
            {
                Vector3 size = portal.axis == "X"
                    ? new Vector3(portal.width - .08f, portal.height - .2f, portal.thickness + .02f)
                    : new Vector3(portal.thickness + .02f, portal.height - .2f, portal.width - .08f);
                var probe = new Bounds(portal.position + Vector3.up * (portal.height / 2), size);
                foreach (var solid in solids)
                    if (IsObstacle(solid) && probe.Intersects(solid.bounds))
                    {
                        failures++;
                        Debug.LogError("Door opening overlaps collider: " + portal.name + " / " + solid.name, solid);
                    }
            }
            for (int i = 1; i < layout.route.Length; i++)
            {
                Vector3 a = layout.route[i - 1].position;
                Vector3 b = layout.route[i].position;
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .04f));
                for (int j = 0; j <= steps; j++)
                {
                    Vector3 ground = Vector3.Lerp(a, b, (float)j / steps);
                    var probe = new Bounds(ground + Vector3.up * .95f, new Vector3(.6f, 1.7f, .6f));
                    foreach (var solid in solids)
                        if (IsObstacle(solid) && probe.Intersects(solid.bounds))
                        {
                            failures++;
                            Debug.LogError("Route clearance overlaps collider near " + layout.route[i].name + " / " + solid.name, solid);
                            break;
                        }
                    bool supported = false;
                    foreach (var solid in solids)
                        if (solid.enabled && !solid.isTrigger && solid.gameObject.activeInHierarchy &&
                            solid.bounds.max.y <= .02f && solid.bounds.Contains(ground - Vector3.up * .05f))
                        { supported = true; break; }
                    if (!supported)
                    {
                        failures++;
                        Debug.LogError("Route sample has no floor support near " + layout.route[i].name);
                    }
                }
            }
            Debug.Log("M1-A static check: " + layout.portals.Length + " openings; " + (layout.route.Length - 1) +
                " route segments; failures=" + failures +
                ". No assets saved. This is not a CharacterController, shopping-bag, accessibility or VR test.");
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    static bool IsObstacle(BoxCollider solid)
    {
        return solid.enabled && !solid.isTrigger && solid.gameObject.activeInHierarchy && solid.bounds.max.y > .1f;
    }
}
#endif
