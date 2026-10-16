#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

// Explicit menus only: no automatic import callbacks, scene opening, package edits or Git operations.
public static class ApartmentTeleportSetup
{
    const string ScenePath = "Assets/Scenes/ApartmentPhase2_VR.unity";
    const string FloorLayer = "TeleportFloor";
    const string AgentName = "TeleportPlayer";

    [MenuItem("Apartment Phase 2/M2-B3/Configure A-Button Teleport")]
    public static void Configure()
    {
        try
        {
            Scene scene = RequireScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存现有场景修改，再运行传送配置；不会丢弃修改。");
            var root = scene.GetRootGameObjects().Single(o => o.name == "ApartmentRoot");
            var mover = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ApartmentVRLocomotion>(true)).Single();
            if (mover.ConfigurationError() != null) throw new InvalidOperationException(mover.ConfigurationError());
            if (root.GetComponents<NavMeshSurface>().Length > 1) throw new InvalidOperationException("ApartmentRoot 有多个导航地图，请先检查。");
            var surface = root.GetComponent<NavMeshSurface>();
            if (surface != null && surface.navMeshData != null && surface != mover.teleportSurface)
                throw new InvalidOperationException("发现已有导航地图，不会覆盖同学的数据。");
            var floors = root.GetComponentsInChildren<BoxCollider>(true).Where(c =>
                c.name.StartsWith("Floor_", StringComparison.Ordinal) || c.name.StartsWith("Threshold_Door_", StringComparison.Ordinal)).ToArray();
            if (floors.Length != 20 || floors.Any(c => !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy ||
                Mathf.Abs(c.bounds.max.y) > .01f))
                throw new InvalidOperationException("预期 20 块 Y=0、启用且非 Trigger 的地板/门槛；请检查当前场景。");
            if (!EditorUtility.DisplayDialog("设置 A 键传送",
                "将给 20 块地板/门槛设置 TeleportFloor，新增 TeleportPlayer 体型并烘焙地图，" +
                "关闭键盘 Debug Mode，并保存当前 VR 场景。不会改公寓尺寸、其他场景、包或 GitHub。", "配置并保存", "取消")) return;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure apartment A-button teleport");
            int layer = EnsureFloorLayer();
            int agent = EnsureAgent();
            foreach (var floor in floors)
            {
                Undo.RecordObject(floor.gameObject, "Classify teleport floor");
                floor.gameObject.layer = layer;
                EditorUtility.SetDirty(floor.gameObject);
            }
            if (surface == null) surface = Undo.AddComponent<NavMeshSurface>(root);
            Undo.RecordObject(surface, "Configure navigation surface");
            surface.agentTypeID = agent;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << layer) | 1;
            surface.defaultArea = NavMesh.GetAreaFromName("Walkable");
            surface.overrideVoxelSize = true;
            surface.voxelSize = .05f;
            var serializedSurface = new SerializedObject(surface);
            serializedSurface.FindProperty("m_GenerateLinks").boolValue = false;
            serializedSurface.ApplyModifiedProperties();
            Physics.SyncTransforms();
            // Keep every previous bake asset recoverable: always create a fresh asset, never delete/overwrite it.
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("导航地图烘焙失败；未保存场景，请检查 Console。");
            string dataPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/ApartmentPhase2_VR_Teleport.asset");
            AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
            EditorUtility.SetDirty(surface);
            Undo.RecordObject(mover, "Assign teleport and restore VR mode");
            mover.teleportEnabled = true;
            mover.teleportSurface = surface;
            mover.teleportFloorMask = 1 << layer;
            mover.teleportMaxDistance = 5f;
            mover.turnMode = ApartmentTurnMode.Smooth;
            mover.smoothTurnSpeed = 60f;
            mover.turnDeadZone = .20f;
            mover.debugMode = false;
            mover.desktopCamera.enabled = false;
            mover.desktopCamera.GetComponent<AudioListener>().enabled = false;
            mover.desktopCamera.gameObject.SetActive(false);
            EditorUtility.SetDirty(mover);
            var rig = mover.rig;
            rig.enabled = true;
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true)) camera.enabled = camera.transform == rig.centerEyeAnchor;
            foreach (var listener in rig.GetComponentsInChildren<AudioListener>(true)) listener.enabled = listener.transform == rig.centerEyeAnchor;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unity 未确认场景保存成功。");
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = mover.gameObject;
            Debug.Log("M2-B3 configured: right A hold/release; 20 floors; 5m; Debug Mode OFF; map=" + dataPath +
                ". No Git commit/push. Quest Link hardware acceptance still required.");
            Validate();
        }
        catch (Exception e)
        {
            Debug.LogError("M2-B3 setup stopped: " + e.Message + " Changes are left for inspection/Undo; no asset is deleted or restored automatically.");
        }
    }

    static int EnsureFloorLayer()
    {
        int existing = LayerMask.NameToLayer(FloorLayer);
        if (existing >= 0) return existing;
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {
                layers.GetArrayElementAtIndex(i).stringValue = FloorLayer;
                tags.ApplyModifiedProperties();
                return i;
            }
        throw new InvalidOperationException("没有可用的自定义 Layer。");
    }

    static int EnsureAgent()
    {
        int id = -1;
        bool found = false;
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            var settings = NavMesh.GetSettingsByIndex(i);
            if (NavMesh.GetSettingsNameFromID(settings.agentTypeID) == AgentName) { id = settings.agentTypeID; found = true; }
        }
        if (!found)
        {
            var settings = NavMesh.CreateSettings();
            id = settings.agentTypeID;
        }
        var project = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
        var agents = project.FindProperty("m_Settings");
        for (int i = 0; i < agents.arraySize; i++)
        {
            var entry = agents.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("agentTypeID").intValue != id) continue;
            project.FindProperty("m_SettingNames").GetArrayElementAtIndex(i).stringValue = AgentName;
            entry.FindPropertyRelative("agentRadius").floatValue = .30f;
            // Door openings are 2.10m. Baking the same height removes the voxelised door passages.
            // A 1.80m candidate map has clearance; runtime sweeps check the real tracked body height.
            entry.FindPropertyRelative("agentHeight").floatValue = 1.80f;
            entry.FindPropertyRelative("agentClimb").floatValue = .20f;
            entry.FindPropertyRelative("agentSlope").floatValue = 45f;
            entry.FindPropertyRelative("ledgeDropHeight").floatValue = 0f;
            entry.FindPropertyRelative("maxJumpAcrossDistance").floatValue = 0f;
            project.ApplyModifiedProperties();
            return id;
        }
        throw new InvalidOperationException("无法保存 TeleportPlayer 体型。");
    }

    [MenuItem("Apartment Phase 2/M2-B3/Validate Teleport (Read Only)")]
    public static void Validate()
    {
        try
        {
            Scene scene = RequireScene();
            var mover = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ApartmentVRLocomotion>(true)).Single();
            string error = mover.ConfigurationError() ?? mover.TeleportConfigurationError();
            if (error != null) throw new InvalidOperationException(error);
            if (mover.debugMode || mover.desktopCamera.gameObject.activeSelf) throw new InvalidOperationException("必须关闭 Debug Mode 和桌面相机。");
            if (scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<OVRCameraRig>(true)).Count() != 1)
                throw new InvalidOperationException("必须保持唯一 Camera Rig。");
            if (!AssetDatabase.Contains(mover.teleportSurface.navMeshData)) throw new InvalidOperationException("地图尚未保存为项目资源。");
            var floors = mover.teleportSurface.GetComponentsInChildren<BoxCollider>(true).Where(c =>
                c.name.StartsWith("Floor_", StringComparison.Ordinal) || c.name.StartsWith("Threshold_Door_", StringComparison.Ordinal)).ToArray();
            if (floors.Length != 20 || floors.Any(c => c.gameObject.layer != LayerMask.NameToLayer(FloorLayer)))
                throw new InvalidOperationException("地板/门槛层分类不完整。");
            Debug.Log("M2-B3 static checks PASS: unique player, saved map, 20 classified floors, right A, 5m, VR mode. " +
                "NOT VERIFIED: real controller input, visual appearance, comfort, tracking/focus loss, Quest Link.");
        }
        catch (Exception e) { Debug.LogError("M2-B3 validation failed: " + e.Message); }
    }

    internal static Scene RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("请退出 Play 并等待导入/编译完成。");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("请仅打开 ApartmentPhase2_VR 场景，不使用 Additive。");
        return scene;
    }
}

[CustomEditor(typeof(ApartmentVRLocomotion))]
public sealed class ApartmentVRLocomotionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var mover = (ApartmentVRLocomotion)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("传送操作", "右手 A：按住瞄准，松开传送");
        EditorGUILayout.LabelField("右摇杆转向", mover.turnMode == ApartmentTurnMode.Smooth ? "连续转向（角度/秒）" : "分段转向");
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("移动状态", mover.Status);
            EditorGUILayout.LabelField("传送状态", mover.TeleportState.ToString());
            if (!string.IsNullOrEmpty(mover.TeleportFailureReason))
                EditorGUILayout.HelpBox(mover.TeleportFailureReason, MessageType.Info);
        }
        else
        {
            var error = mover.TeleportConfigurationError();
            EditorGUILayout.HelpBox(error ?? "地图已配置。连接 Quest Link 后点击 Play 测试；自动检查不代表真机通过。",
                error == null ? MessageType.Info : MessageType.Warning);
        }
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
#endif
