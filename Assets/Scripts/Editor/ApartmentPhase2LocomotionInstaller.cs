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

// Manual menus only. Never opens/builds another scene or edits an SDK/configuration asset.
public static class ApartmentPhase2LocomotionInstaller
{
    const string ScenePath = "Assets/Scenes/ApartmentPhase2_VR.unity";
    const string RuntimePath = "Assets/Scripts/ApartmentVRLocomotion.cs";
    const string RigPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    const string DebugName = "DesktopDebugCamera";
    static readonly string[] ProtectedScenes = {
        "Assets/Scenes/ApartmentPhase2.unity", "Assets/Scenes/ApartmentPhase2_Furnished.unity" };
    static bool installing;

    [MenuItem("Apartment Phase 2/M2-B2/Install Locomotion (No Overwrite)")]
    public static void InstallLocomotion()
    {
        if (installing) { Debug.LogError("M2-B2: installation is already running."); return; }
        installing = true;
        int undoGroup = -1;
        bool saved = false;
        try
        {
            Scene scene = RequireScene();
            if (Application.unityVersion != "6000.6.4f1") throw new InvalidOperationException("Use Unity 6000.6.4f1.");
            if (scene.isDirty) throw new InvalidOperationException("Scene has unsaved edits. Save/review them yourself before installation; nothing is discarded.");
            if (!File.Exists(ScenePath) || !File.Exists(ScenePath + ".meta")) throw new IOException("VR scene or metadata is missing.");
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(RuntimePath);
            if (script == null || script.GetClass() != typeof(ApartmentVRLocomotion))
                throw new InvalidOperationException("Runtime script is not successfully imported/compiled.");
            Report baseline = Inspect(scene, false);
            if (baseline.errors.Count != 0) throw new InvalidOperationException(string.Join("; ", baseline.errors));
            if (All<ApartmentVRLocomotion>(scene).Length != 0 || Objects(scene).Any(o => o.name == DebugName))
                throw new InvalidOperationException("Installation/component/debug object already exists. Nothing is overwritten or updated.");
            string targetHash = Hash(ScenePath);
            string targetMetaHash = Hash(ScenePath + ".meta");
            var protectedHashes = ProtectedScenes.Concat(ProtectedScenes.Select(p => p + ".meta")).ToDictionary(p => p, Hash);
            if (!EditorUtility.DisplayDialog("Install M2-B2 locomotion",
                "Add one locomotion component and an inactive desktop camera, then save ONLY ApartmentPhase2_VR.unity. " +
                "No XR settings, furniture or source scenes are changed. Continue?", "Install and save VR scene", "Cancel")) return;
            if (scene.isDirty || Hash(ScenePath) != targetHash || Hash(ScenePath + ".meta") != targetMetaHash)
                throw new IOException("VR scene changed while approval was pending; installation stopped.");
            foreach (var item in protectedHashes)
                if (Hash(item.Key) != item.Value) throw new IOException("Protected source scene changed: " + item.Key);

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install M2-B2 locomotion on VRPlayer");
            var rig = All<OVRCameraRig>(scene).Single();
            var player = rig.transform.parent.gameObject;
            var debug = new GameObject(DebugName);
            SceneManager.MoveGameObjectToScene(debug, scene);
            Undo.RegisterCreatedObjectUndo(debug, "Create inactive desktop debug camera");
            debug.transform.SetParent(player.transform, false);
            debug.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            debug.transform.localRotation = Quaternion.identity;
            debug.transform.localScale = Vector3.one;
            debug.tag = "MainCamera";
            var camera = Undo.AddComponent<Camera>(debug);
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 75f;
            camera.enabled = false;
            var audio = Undo.AddComponent<AudioListener>(debug);
            audio.enabled = false;
            debug.SetActive(false);
            var locomotion = Undo.AddComponent<ApartmentVRLocomotion>(player);
            Undo.RecordObject(locomotion, "Assign existing Rig and debug camera references");
            locomotion.rig = rig;
            locomotion.manager = rig.GetComponent<OVRManager>();
            locomotion.desktopCamera = camera;
            locomotion.debugMode = false;
            EditorUtility.SetDirty(locomotion);

            Report installed = Inspect(scene, true);
            if (installed.errors.Count != 0) throw new InvalidOperationException(string.Join("; ", installed.errors));
            if (Hash(ScenePath) != targetHash || Hash(ScenePath + ".meta") != targetMetaHash)
                throw new IOException("VR scene changed externally before saving; installation stopped.");
            foreach (var item in protectedHashes)
                if (Hash(item.Key) != item.Value) throw new IOException("Protected source changed before save: " + item.Key);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath, false)) throw new IOException("Unity did not confirm a successful VR scene save.");
            saved = true;
            Undo.CollapseUndoOperations(undoGroup);
            if (Hash(ScenePath + ".meta") != targetMetaHash) throw new IOException("VR scene GUID metadata changed unexpectedly.");
            foreach (var item in protectedHashes)
                if (Hash(item.Key) != item.Value) throw new IOException("Protected source hash changed unexpectedly: " + item.Key);
            Log(Inspect(scene, true));
            Debug.Log("M2-B2 installed and saved ONLY " + ScenePath + ". Debug Mode defaults OFF. No Play Mode/Quest Link test performed.");
        }
        catch (Exception exception)
        {
            // Undo only changes registered by this installation; never restore/delete asset files.
            if (!saved && undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogError("M2-B2 installation stopped: " + exception.Message +
                (saved ? " VR scene was saved; inspect it before further work." : " No successful save. Review scene/Undo state before retrying.") +
                " No recovery writes to existing assets are attempted.");
        }
        finally { installing = false; }
    }

    [MenuItem("Apartment Phase 2/M2-B2/Validate Active VR Scene (Read Only)")]
    public static void ValidateActiveVRScene()
    {
        try
        {
            Scene scene = RequireScene();
            Log(Inspect(scene, true));
            if (scene.isDirty) Debug.LogWarning("M2-B2 checked current unsaved scene state only. Nothing was saved.");
        }
        catch (Exception exception) { Debug.LogError("M2-B2 read-only check stopped: " + exception.Message); }
    }

    static Scene RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Wait for import/compilation and leave Play Mode first.");
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("Manually open only ApartmentPhase2_VR.unity (not additively). No scene is opened automatically.");
        return scene;
    }

    sealed class Report
    {
        public readonly List<string> errors = new List<string>();
        public readonly List<string> limits = new List<string>();
        public int rigs, controllers, movers, cameras, listeners, overlaps;
    }

    static Report Inspect(Scene scene, bool requireInstalled)
    {
        var report = new Report();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            report.limits.Add("Active build target is not Windows 64-bit. Verify the PC VR target separately; no target/settings are changed by this tool.");
        var rigs = All<OVRCameraRig>(scene);
        var managers = All<OVRManager>(scene);
        var controllers = All<CharacterController>(scene);
        var movers = All<ApartmentVRLocomotion>(scene);
        var cameras = All<Camera>(scene).Where(c => c.isActiveAndEnabled).ToArray();
        var listeners = All<AudioListener>(scene).Where(a => a.isActiveAndEnabled).ToArray();
        report.rigs = rigs.Length; report.controllers = controllers.Length; report.movers = movers.Length;
        report.cameras = cameras.Length; report.listeners = listeners.Length;
        if (rigs.Length != 1 || managers.Length != 1 || controllers.Length != 1)
            report.errors.Add("Exactly one Rig, OVRManager and CharacterController is required (including inactive duplicates).");
        if (cameras.Length != 1 || !cameras[0].CompareTag("MainCamera") || listeners.Length != 1)
            report.errors.Add("Exactly one active main Camera and AudioListener is required.");
        foreach (GameObject obj in Objects(scene))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(obj) > 0) report.errors.Add("Missing script: " + obj.name);
            if (obj.name == "InspectionCamera" && obj.activeInHierarchy) report.errors.Add("InspectionCamera must remain inactive.");
        }
        foreach (MonoBehaviour behaviour in All<MonoBehaviour>(scene))
            if (behaviour != null && (behaviour.GetType().Name == "JoystickLocomotion" ||
                behaviour.GetType().Name == "OVRPlayerController" || behaviour.GetType().Namespace?.StartsWith("UnityEngine.XR.Interaction.Toolkit") == true))
                report.errors.Add("Conflicting/unsupported player system exists: " + behaviour.GetType().Name);
        if (requireInstalled && movers.Length != 1) report.errors.Add("Exactly one installed ApartmentVRLocomotion is required.");
        if (!requireInstalled && movers.Length != 0) report.errors.Add("Locomotion already installed; no overwrite permitted.");
        if (rigs.Length != 1 || managers.Length != 1 || controllers.Length != 1) return report;
        OVRCameraRig rig = rigs[0]; CharacterController cc = controllers[0];
        Transform player = rig.transform.parent;
        if (!rig.isActiveAndEnabled || !managers[0].isActiveAndEnabled || !cc.enabled || player == null ||
            player.name != "VRPlayer" || player.parent != null || cc.transform != player || managers[0].gameObject != rig.gameObject)
            report.errors.Add("VRPlayer/Rig/controller hierarchy or enabled state is invalid.");
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rig.gameObject) != RigPath)
            report.errors.Add("Rig must retain its original SDK prefab association.");
        var origin = new SerializedObject(managers[0]).FindProperty("_trackingOriginType");
        if (origin == null || origin.intValue != (int)OVRManager.TrackingOrigin.FloorLevel || rig.usePerEyeCameras)
            report.errors.Add("Expected FloorLevel and usePerEyeCameras=false.");
        if (All<OVRHeadsetEmulator>(scene).Any(e => e.enabled)) report.errors.Add("OVRHeadsetEmulator must stay disabled.");
        Transform eye = rig.transform.Find("TrackingSpace/CenterEyeAnchor");
        if (eye == null || cameras.Length != 1 || cameras[0].transform != eye ||
            listeners.Length != 1 || listeners[0].transform != eye)
            report.errors.Add("Active camera/audio must be the Rig CenterEyeAnchor.");
        if (player != null)
        {
            if (Vector3.Distance(player.lossyScale, Vector3.one) > .001f || Vector3.Angle(player.up, Vector3.up) > .01f)
                report.errors.Add("VRPlayer scale/orientation is unsupported.");
            if (player.GetComponent<Rigidbody>() != null || rig.transform.localPosition.sqrMagnitude > .000001f ||
                Quaternion.Angle(rig.transform.localRotation, Quaternion.identity) > .01f ||
                Vector3.Distance(rig.transform.localScale, Vector3.one) > .001f)
                report.errors.Add("Rig local transform must be identity; no competing VRPlayer Rigidbody is allowed.");
            foreach (MonoBehaviour rootScript in player.GetComponents<MonoBehaviour>())
                if (rootScript != null && !(rootScript is ApartmentVRLocomotion))
                    report.errors.Add("Unexpected root script could compete for motion: " + rootScript.GetType().Name);
        }
        if (cc.radius <= 0f || cc.height < 2f * cc.radius || cc.skinWidth <= 0f || cc.skinWidth >= cc.radius ||
            cc.minMoveDistance != 0f || cc.stepOffset < 0f || cc.stepOffset >= cc.height ||
            Mathf.Abs(cc.center.y - cc.height * .5f) > .001f)
            report.errors.Add("CharacterController shape/feet/minMoveDistance parameters are invalid.");
        if (movers.Length == 1)
        {
            var mover = movers[0];
            string error = mover.ConfigurationError();
            if (error != null) report.errors.Add(error);
            if (!mover.enabled || mover.transform != player || mover.rig != rig || mover.manager != managers[0])
                report.errors.Add("Locomotion component/ref assignments are invalid.");
            if (Mathf.Abs(cc.radius - mover.capsuleRadius) > .001f || Mathf.Abs(cc.skinWidth - mover.skinWidth) > .001f ||
                Mathf.Abs(cc.stepOffset - mover.stepOffset) > .001f || Mathf.Abs(cc.slopeLimit - mover.slopeLimit) > .001f)
                report.errors.Add("Saved CharacterController parameters differ from locomotion initialization parameters; static shape would not represent Play Mode.");
            if (mover.debugMode) report.limits.Add("Debug Mode is selected: desktop tests do not verify Quest Link.");
            var debug = mover.desktopCamera;
            if (debug != null && (debug.gameObject.activeSelf || debug.enabled || debug.stereoTargetEye != StereoTargetEyeMask.None ||
                debug.GetComponent<AudioListener>() == null || debug.GetComponent<AudioListener>().enabled))
                report.errors.Add("Desktop camera/audio must be inactive in the saved Edit Mode scene and stereo target must be None.");
        }
        InspectInitialCapsule(scene, cc, report);
        return report;
    }

    static void InspectInitialCapsule(Scene scene, CharacterController cc, Report report)
    {
        // Analytic vertical capsule vs axis-aligned box only. No Physics sync, writes or temporary objects.
        Vector3 center = cc.transform.TransformPoint(cc.center);
        float half = Mathf.Max(0f, cc.height * .5f - cc.radius);
        float low = center.y - half, high = center.y + half;
        foreach (Collider collider in All<Collider>(scene))
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger ||
                collider.transform.IsChildOf(cc.transform)) continue;
            if (!(collider is BoxCollider box) || !AxisAligned(box.transform))
            { report.limits.Add("Initial overlap not analytically verified for " + collider.name + "; check in Play Mode."); continue; }
            Bounds bounds = collider.bounds;
            float dx = Mathf.Max(bounds.min.x - center.x, center.x - bounds.max.x, 0f);
            float dz = Mathf.Max(bounds.min.z - center.z, center.z - bounds.max.z, 0f);
            float dy = Mathf.Max(bounds.min.y - high, low - bounds.max.y, 0f);
            float clearance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) - cc.radius;
            if (clearance < -.001f)
            { report.overlaps++; report.errors.Add("Initial capsule overlap: " + collider.name + " (" + clearance.ToString("F3") + " m)."); }
            else if (clearance > .001f && clearance < cc.skinWidth)
                report.limits.Add("Narrow initial clearance: " + collider.name + " (" + clearance.ToString("F3") + " m).");
        }
    }

    static bool AxisAligned(Transform transform)
    {
        Matrix4x4 matrix = transform.localToWorldMatrix;
        for (int column = 0; column < 3; column++)
        {
            Vector3 axis = ((Vector3)matrix.GetColumn(column)).normalized;
            if (Mathf.Max(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)) < .99999f) return false;
        }
        return true;
    }

    static IEnumerable<GameObject> Objects(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
    static void Log(Report report)
    {
        foreach (string error in report.errors) Debug.LogError("M2-B2 MODEL/CONFIG ERROR: " + error);
        foreach (string limit in report.limits) Debug.LogWarning("M2-B2 DESIGN LIMIT / CHECK COVERAGE: " + limit);
        Debug.Log("M2-B2 static check: rigs=" + report.rigs + ", characterControllers=" + report.controllers +
            ", locomotion=" + report.movers + ", activeCameras=" + report.cameras + ", activeAudioListeners=" + report.listeners +
            ", initialOverlaps=" + report.overlaps + ", errors=" + report.errors.Count + ", limits=" + report.limits.Count +
            ". No assets saved by validation. UNVERIFIED: Play Mode, Physics movement/turning, controller input, room-scale safety, Quest Link, comfort and route accessibility.");
    }
}
#endif
