using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class TwoRoomSceneBuilder
{
    const string ScenePath = "Assets/Scenes/test/TwoRooms.unity";
    const string RigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
    const float H = 2.8f;       // 墙高
    const float T = 0.15f;      // 墙厚
    const float DoorW = 1.2f;   // 门洞宽
    const float DoorH = 2.1f;   // 门洞高

    [MenuItem("Quest Demo/Build Two-Room Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rooms = new GameObject("Rooms").transform;

        // 客厅 x[-7,0] 厨房 x[0,5] z[-3,3]
        Box(rooms, "Floor_LivingRoom", new Vector3(-3.5f, -0.05f, 0f), new Vector3(7f, 0.1f, 6f));
        Box(rooms, "Floor_Kitchen", new Vector3(2.5f, -0.05f, 0f), new Vector3(5f, 0.1f, 6f));
        Box(rooms, "Wall_West", new Vector3(-7f, H * 0.5f, 0f), new Vector3(T, H, 6f + T));
        Box(rooms, "Wall_East", new Vector3(5f, H * 0.5f, 0f), new Vector3(T, H, 6f + T));
        Box(rooms, "Wall_North", new Vector3(-1f, H * 0.5f, 3f), new Vector3(12f + T, H, T));
        Box(rooms, "Wall_South", new Vector3(-1f, H * 0.5f, -3f), new Vector3(12f + T, H, T));

        float seg = 3f - DoorW * 0.5f;
        Box(rooms, "Wall_Divider_A", new Vector3(0f, H * 0.5f, -(DoorW + seg) * 0.5f), new Vector3(T, H, seg));
        Box(rooms, "Wall_Divider_B", new Vector3(0f, H * 0.5f, (DoorW + seg) * 0.5f), new Vector3(T, H, seg));
        Box(rooms, "Wall_DoorTop", new Vector3(0f, (H + DoorH) * 0.5f, 0f), new Vector3(T, H - DoorH, DoorW));

        var lightGO = new GameObject("Directional Light");
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        lightGO.AddComponent<Light>().type = LightType.Directional;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

        var player = new GameObject("Player");
        player.transform.position = new Vector3(-4f, 0f, 0f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        player.AddComponent<JoystickLocomotion>();

        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        if (rigPrefab)
            ((GameObject)PrefabUtility.InstantiatePrefab(rigPrefab)).transform.SetParent(player.transform, false);
        else
            Debug.LogWarning($"未找到 {RigPrefabPath}，请手动把手柄/头显相机放在 Player 下");

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (!scenes.Exists(s => s.path == ScenePath))
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"已生成 {ScenePath}");
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
    }
}
