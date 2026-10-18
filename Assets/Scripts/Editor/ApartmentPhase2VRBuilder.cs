#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu driven only: no InitializeOnLoad, runtime component, or automatic build.
public static class ApartmentPhase2VRBuilder
{
    const string SourceScene = "Assets/Scenes/test/ApartmentPhase2_Furnished.unity";
    const string TargetScene = "Assets/Scenes/test/ApartmentPhase2_VR.unity";
    const string RigPrefab = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    const string RigGuid = "126d619cf4daa52469682f85c1378b4a";
    const string CreateMenu = "Apartment Phase 2/M2-B1/Create VR Scene (No Overwrite)";
    const string ValidateMenu = "Apartment Phase 2/M2-B1/Validate Active VR Scene (Read Only)";
    const float Tolerance = .001f;
    static readonly Vector3 Spawn = new Vector3(.45f, 0f, .70f);
    static bool building;

    [MenuItem(CreateMenu)]
    public static void CreateVRScene()
    {
        if (building) { Debug.LogError("M2-B1: a build is already in progress."); return; }
        building = true;
        bool copied = false;
        bool saved = false;
        try
        {
            RequireEditorIdle();
            if (Application.unityVersion != "6000.6.4f1")
                throw new InvalidOperationException("Use Unity 6000.6.4f1 for this builder.");
            RequireNoDirtyScenes();
            RequireNewTarget();
            if (!File.Exists(Absolute(SourceScene)) || !File.Exists(Absolute(SourceScene + ".meta")) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
                throw new FileNotFoundException("Source scene or its imported metadata is missing: " + SourceScene);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
            if (prefab == null || AssetDatabase.AssetPathToGUID(RigPrefab) != RigGuid ||
                prefab.GetComponentsInChildren<OVRCameraRig>(true).Length != 1 ||
                prefab.GetComponentsInChildren<OVRManager>(true).Length != 1 ||
                prefab.GetComponentsInChildren<CharacterController>(true).Length != 0)
                throw new InvalidOperationException("Expected SDK 205 OVRCameraRig prefab/components could not be confirmed.");

            // A lock failure stops before copying. Never save the source scene.
            using (var sourceLock = new FileStream(Absolute(SourceScene), FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var metaLock = new FileStream(Absolute(SourceScene + ".meta"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                string sourceHash = HashFile(SourceScene);
                string metaHash = HashFile(SourceScene + ".meta");
                RequireNewTarget();
                if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
                    throw new IOException("Unity could not copy the scene. Any partial target is left for review.");
                copied = true;
                string targetGuid = AssetDatabase.AssetPathToGUID(TargetScene);
                if (string.IsNullOrEmpty(targetGuid) || targetGuid == AssetDatabase.AssetPathToGUID(SourceScene) ||
                    HashFile(TargetScene) != sourceHash)
                    throw new InvalidOperationException("Scene copy/GUID verification failed; target will not be saved.");

                // All loaded scenes were clean. Single mode avoids additive duplicate cameras/rigs.
                RequireNoDirtyScenes();
                Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
                RequireEmptyPlayerBaseline(scene);
                ConfigureInspectionCamera(scene);
                CreatePlayer(scene, prefab);
                Report structure = InspectStructure(scene);
                if (structure.errors.Count != 0)
                    throw new InvalidOperationException("Rig structure check failed: " + string.Join("; ", structure.errors));
                if (HashFile(SourceScene) != sourceHash || HashFile(SourceScene + ".meta") != metaHash ||
                    HashFile(TargetScene) != sourceHash || AssetDatabase.AssetPathToGUID(TargetScene) != targetGuid)
                    throw new IOException("A source or target changed during building; saving is stopped.");

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, TargetScene, false))
                    throw new IOException("Unity did not report a successful target scene save.");
                saved = true;
                if (HashFile(SourceScene) != sourceHash || HashFile(SourceScene + ".meta") != metaHash)
                    throw new IOException("Source content changed unexpectedly. No restoration is attempted.");
                Debug.Log("M2-B1 scene saved: " + TargetScene + ". Source scene and source meta hashes unchanged.");
                ValidateScene(scene);
            }
        }
        catch (Exception exception)
        {
            string state = saved ? "The new scene was saved; verification needs review." :
                copied ? "A new target copy may exist; it is not a completed VR scene." :
                "No completed VR scene was created. Check whether a partial target exists.";
            Debug.LogError("M2-B1 stopped: " + exception.Message + "\n" + state +
                " No existing asset is deleted, restored, or overwritten by recovery. Do not rerun against an existing target.");
            Debug.LogException(exception);
        }
        finally { building = false; }
    }

    [MenuItem(ValidateMenu)]
    public static void ValidateActiveVRScene()
    {
        try
        {
            RequireEditorIdle();
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != TargetScene)
                throw new InvalidOperationException("Open ApartmentPhase2_VR.unity and make it the active scene first.");
            ValidateScene(scene);
        }
        catch (Exception exception) { Debug.LogError("M2-B1 read-only validation stopped: " + exception.Message); }
    }

    static void RequireEditorIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Exit Play Mode and wait for compilation/import to finish.");
    }

    static void RequireNoDirtyScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Unsaved scene: " + scene.name +
                    ". Save your intended work manually or cancel this operation. Nothing is auto-saved or discarded.");
        }
    }

    static void RequireNewTarget()
    {
        if (File.Exists(Absolute(TargetScene)) || Directory.Exists(Absolute(TargetScene)) ||
            File.Exists(Absolute(TargetScene + ".meta")) || Directory.Exists(Absolute(TargetScene + ".meta")) ||
            !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(TargetScene)))
            throw new IOException("Target scene or metadata already exists. No overwrite: " + TargetScene);
    }

    static string Absolute(string relative)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (Path.IsPathRooted(relative) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path is outside the project: " + relative);
        return path;
    }

    static string HashFile(string relative)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(Absolute(relative)))
            return BitConverter.ToString(sha.ComputeHash(stream));
    }

    static List<T> Components<T>(Scene scene) where T : Component
    {
        var result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }

    static void RequireEmptyPlayerBaseline(Scene scene)
    {
        if (Components<OVRCameraRig>(scene).Count != 0 || Components<OVRManager>(scene).Count != 0 ||
            Components<CharacterController>(scene).Count != 0)
            throw new InvalidOperationException("Source copy already contains a rig, manager, or character controller.");
        foreach (Transform transform in Components<Transform>(scene))
            if (transform.name == "VRPlayer") throw new InvalidOperationException("VRPlayer already exists in source copy.");
        foreach (MonoBehaviour behaviour in Components<MonoBehaviour>(scene))
        {
            if (behaviour == null) throw new InvalidOperationException("Source copy contains a missing script.");
            string type = behaviour.GetType().Name;
            if (type == "JoystickLocomotion" || type == "OVRPlayerController")
                throw new InvalidOperationException("Source copy contains an existing locomotion controller: " + type);
        }
        var cameras = Components<Camera>(scene);
        if (cameras.Count != 1 || cameras[0].name != "InspectionCamera")
            throw new InvalidOperationException("Expected exactly one source camera named InspectionCamera.");
    }

    static void ConfigureInspectionCamera(Scene scene)
    {
        Camera camera = Components<Camera>(scene)[0];
        camera.enabled = false;
        foreach (AudioListener listener in camera.gameObject.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        camera.gameObject.tag = "Untagged";
        camera.gameObject.SetActive(false);
    }

    static void CreatePlayer(Scene scene, GameObject prefab)
    {
        var player = new GameObject("VRPlayer");
        SceneManager.MoveGameObjectToScene(player, scene);
        player.transform.SetPositionAndRotation(Spawn, Quaternion.identity);
        player.transform.localScale = Vector3.one;
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.80f;
        controller.radius = .30f;
        controller.center = new Vector3(0, .90f, 0);
        controller.skinWidth = .03f;
        controller.stepOffset = .20f;
        controller.slopeLimit = 45f;
        controller.minMoveDistance = 0f;

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null) throw new InvalidOperationException("Prefab instantiation failed.");
        instance.transform.SetParent(player.transform, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        OVRCameraRig rig = instance.GetComponent<OVRCameraRig>();
        OVRManager manager = instance.GetComponent<OVRManager>();
        if (rig == null || manager == null) throw new InvalidOperationException("Expected rig components must be on the prefab root.");
        rig.usePerEyeCameras = false;
        manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
        Transform eye = instance.transform.Find("TrackingSpace/CenterEyeAnchor");
        Camera main = eye != null ? eye.GetComponent<Camera>() : null;
        AudioListener audio = eye != null ? eye.GetComponent<AudioListener>() : null;
        if (main == null || audio == null) throw new InvalidOperationException("CenterEyeAnchor camera/audio missing.");
        foreach (Camera camera in instance.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = camera == main;
            camera.gameObject.tag = camera == main ? "MainCamera" : "Untagged";
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera.gameObject);
        }
        foreach (AudioListener listener in instance.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = listener == audio;
            PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
        }
        foreach (OVRHeadsetEmulator emulator in instance.GetComponentsInChildren<OVRHeadsetEmulator>(true))
        {
            emulator.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(emulator);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
    }

    class Report
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public int boxes, overlaps, contacts, unsupported;
    }

    static Report InspectStructure(Scene scene)
    {
        var report = new Report();
        var rigs = Components<OVRCameraRig>(scene);
        var managers = Components<OVRManager>(scene);
        var controllers = Components<CharacterController>(scene);
        if (rigs.Count != 1 || !rigs[0].isActiveAndEnabled) report.errors.Add("Expected one active OVRCameraRig.");
        if (managers.Count != 1 || !managers[0].isActiveAndEnabled) report.errors.Add("Expected one active OVRManager.");
        if (controllers.Count != 1 || !controllers[0].enabled) report.errors.Add("Expected one enabled CharacterController.");
        var activeCameras = Components<Camera>(scene).FindAll(c => c.isActiveAndEnabled);
        var activeAudio = Components<AudioListener>(scene).FindAll(a => a.isActiveAndEnabled);
        if (activeCameras.Count != 1 || !activeCameras[0].CompareTag("MainCamera") || activeCameras[0].name != "CenterEyeAnchor")
            report.errors.Add("Expected one effective MainCamera on CenterEyeAnchor.");
        if (activeAudio.Count != 1 || activeAudio[0].name != "CenterEyeAnchor")
            report.errors.Add("Expected one effective AudioListener on CenterEyeAnchor.");
        foreach (MonoBehaviour behaviour in Components<MonoBehaviour>(scene))
        {
            if (behaviour == null) { report.errors.Add("Missing script in scene."); continue; }
            string type = behaviour.GetType().Name;
            if (type == "JoystickLocomotion" || type == "OVRPlayerController") report.errors.Add("Locomotion controller present: " + type);
        }
        foreach (OVRHeadsetEmulator emulator in Components<OVRHeadsetEmulator>(scene))
            if (emulator.enabled) report.errors.Add("OVRHeadsetEmulator must be disabled.");
        foreach (Camera camera in Components<Camera>(scene))
            if (camera.name == "InspectionCamera" && (camera.enabled || camera.gameObject.activeSelf))
                report.errors.Add("InspectionCamera must be disabled/inactive.");
        if (rigs.Count == 1 && controllers.Count == 1 && managers.Count == 1)
        {
            OVRCameraRig rig = rigs[0];
            CharacterController cc = controllers[0];
            Transform player = cc.transform;
            if (player.name != "VRPlayer" || player.parent != null || rig.transform.parent != player ||
                managers[0].gameObject != rig.gameObject || (player.position - Spawn).sqrMagnitude > Tolerance * Tolerance ||
                Quaternion.Angle(player.rotation, Quaternion.identity) > Tolerance ||
                (player.lossyScale - Vector3.one).sqrMagnitude > Tolerance * Tolerance ||
                rig.transform.localPosition.sqrMagnitude > Tolerance * Tolerance ||
                Quaternion.Angle(rig.transform.localRotation, Quaternion.identity) > Tolerance ||
                (rig.transform.localScale - Vector3.one).sqrMagnitude > Tolerance * Tolerance)
                report.errors.Add("Player/rig hierarchy, spawn, rotation, or scale differs from M2-B1 contract.");
            if (rig.usePerEyeCameras) report.errors.Add("usePerEyeCameras must be false.");
            Transform centerEye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
            if (centerEye == null || activeCameras.Count != 1 || activeAudio.Count != 1 ||
                activeCameras[0].transform != centerEye || activeAudio[0].transform != centerEye)
                report.errors.Add("The effective camera/audio must belong to this rig's CenterEyeAnchor.");
            // This exact serialized SDK 205 field was verified in OVRManager.cs; no plugin call is needed.
            var serializedManager = new SerializedObject(managers[0]);
            var origin = serializedManager.FindProperty("_trackingOriginType");
            if (origin == null || origin.intValue != (int)OVRManager.TrackingOrigin.FloorLevel)
                report.errors.Add("Serialized tracking origin must be FloorLevel.");
            if (Mathf.Abs(cc.height - 1.80f) > Tolerance || Mathf.Abs(cc.radius - .30f) > Tolerance ||
                (cc.center - new Vector3(0, .90f, 0)).sqrMagnitude > Tolerance * Tolerance ||
                Mathf.Abs(cc.skinWidth - .03f) > Tolerance || Mathf.Abs(cc.stepOffset - .20f) > Tolerance ||
                Mathf.Abs(cc.slopeLimit - 45f) > Tolerance || Mathf.Abs(cc.minMoveDistance) > Tolerance)
                report.errors.Add("CharacterController parameters differ from M2-B1 contract.");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject) != RigPrefab)
                report.errors.Add("OVRCameraRig prefab association is missing or unexpected.");
        }
        return report;
    }

    static void ValidateScene(Scene scene)
    {
        Report report = InspectStructure(scene);
        if (report.errors.Count == 0) CheckCapsule(scene, Components<CharacterController>(scene)[0], report);
        if (scene.isDirty) report.warnings.Add("Scene has unsaved changes; this checks current memory, not the saved file.");
        if (SceneManager.sceneCount != 1) report.warnings.Add("Other scenes are loaded. This check only covers the target; use it alone for Play Mode.");
        foreach (string error in report.errors) Debug.LogError("M2-B1 modeling/configuration error: " + error);
        foreach (string warning in report.warnings) Debug.LogWarning("M2-B1 review required: " + warning);
        string summary = "M2-B1 static check: rigs=" + Components<OVRCameraRig>(scene).Count +
            "; characterControllers=" + Components<CharacterController>(scene).Count + "; boxes=" + report.boxes +
            "; overlaps=" + report.overlaps + "; contacts=" + report.contacts + "; unsupported=" + report.unsupported +
            "; errors=" + report.errors.Count + ". No assets saved by validation. Unity Physics, HMD and Quest Link are NOT verified.";
        if (report.errors.Count > 0 || report.unsupported > 0) Debug.LogWarning(summary); else Debug.Log(summary);
    }

    static void CheckCapsule(Scene scene, CharacterController cc, Report report)
    {
        Vector3 center = cc.transform.TransformPoint(cc.center);
        float halfSegment = Mathf.Max(0, cc.height * .5f - cc.radius);
        float low = center.y - halfSegment, high = center.y + halfSegment;
        foreach (Collider collider in Components<Collider>(scene))
        {
            if (collider == cc || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger) continue;
            Bounds bounds;
            var box = collider as BoxCollider;
            if (box == null || !TryWorldBox(box, out bounds))
            {
                report.unsupported++;
                report.warnings.Add("Initial overlap not checked for collider: " + ObjectPath(collider.transform));
                continue;
            }
            report.boxes++;
            // Exact segment-to-AABB distance for the prescribed vertical capsule, without Physics queries.
            float dx = Mathf.Max(bounds.min.x - center.x, center.x - bounds.max.x, 0f);
            float dz = Mathf.Max(bounds.min.z - center.z, center.z - bounds.max.z, 0f);
            float dy = Mathf.Max(bounds.min.y - high, low - bounds.max.y, 0f);
            float clearance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - cc.radius;
            if (clearance < -Tolerance)
            {
                report.overlaps++;
                report.errors.Add("Initial capsule overlaps " + ObjectPath(collider.transform) + "; clearance=" + clearance.ToString("F4") + "m. Geometry was not moved.");
            }
            else if (clearance <= Tolerance) report.contacts++; // Floor contact is expected, not penetration.
            else if (clearance < cc.skinWidth)
                report.warnings.Add("Initial clearance below SkinWidth near " + ObjectPath(collider.transform) + ": " + clearance.ToString("F4") + "m.");
        }
    }

    static bool TryWorldBox(BoxCollider box, out Bounds bounds)
    {
        bounds = new Bounds();
        Matrix4x4 matrix = box.transform.localToWorldMatrix;
        Vector3 x = matrix.MultiplyVector(Vector3.right), y = matrix.MultiplyVector(Vector3.up), z = matrix.MultiplyVector(Vector3.forward);
        int ax = WorldAxis(x), ay = WorldAxis(y), az = WorldAxis(z);
        if (ax < 0 || ay < 0 || az < 0 || ax == ay || ax == az || ay == az) return false;
        Vector3 half = box.size * .5f;
        Vector3 extent = Abs(x) * half.x + Abs(y) * half.y + Abs(z) * half.z;
        bounds = new Bounds(matrix.MultiplyPoint3x4(box.center), extent * 2f);
        return true;
    }

    static int WorldAxis(Vector3 vector)
    {
        if (vector.sqrMagnitude < 1e-12f) return -1;
        Vector3 v = Abs(vector.normalized);
        if (v.x > .999999f) return 0;
        if (v.y > .999999f) return 1;
        if (v.z > .999999f) return 2;
        return -1;
    }

    static Vector3 Abs(Vector3 vector) { return new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z)); }
    static string ObjectPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
}
#endif
