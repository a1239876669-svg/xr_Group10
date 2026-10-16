#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;

// Explicit installation only. No import callbacks, package changes, Git operations or source-scene edits.
public static class ApartmentFurnitureSetup
{
    public const string ScenePath = "Assets/Scenes/test/ApartmentPhase2_VR.unity";
    public const string KitchenPath = "ApartmentRoot/Furniture/M1B/Kitchen";
    public const string FontPath = "Assets/Apartment/Fonts/KitchenHints-SC.asset";
    public const string FontSource = "Assets/Apartment/Fonts/NotoSansSC-Regular.otf";
    public static readonly string[] DoorPaths = {
        "CounterNorth/Door_1", "CounterNorth/Door_2", "CounterWest/Door_S", "CounterWest/Door_N",
        "CounterSouth/Door_1", "CounterSouth/Door_2" };

    [MenuItem("Apartment Phase 2/M3 Furniture/Install Kitchen Cabinet Interaction %#&i")]
    public static void Install()
    {
        try
        {
            var scene = RequireScene();
            if (scene.isDirty) throw new InvalidOperationException("请先保存当前场景；不会丢弃未保存修改。");
            if (All<ApartmentFurnitureInteractor>(scene).Any()) { ValidateOrThrow(scene); Debug.Log("M3 already installed; validated without duplicate objects or rebaking."); return; }
            var mover = All<ApartmentVRLocomotion>(scene).Single();
            if (mover.ConfigurationError() != null || mover.TeleportConfigurationError() != null)
                throw new InvalidOperationException("请先修复现有玩家/传送配置。");
            var kitchen = Find(scene, KitchenPath);
            foreach (string path in DoorPaths)
            {
                var leaf = kitchen.Find(path);
                if (leaf == null || leaf.GetComponent<MeshRenderer>() == null || leaf.GetComponents<Collider>().Length != 0 ||
                    leaf.GetComponent<Rigidbody>() != null || leaf.childCount != 0)
                    throw new InvalidOperationException("柜门结构与已检查的原型不一致：" + path);
            }
            foreach (string cabinet in new[] { "CounterNorth", "CounterWest", "CounterSouth" })
                if (kitchen.Find(cabinet + "/CabinetBody")?.GetComponent<BoxCollider>() == null)
                    throw new InvalidOperationException("Missing original cabinet body: " + cabinet);
            if (!EditorUtility.DisplayDialog("安装厨房柜门交互",
                "仅修改当前 VR 场景：三段柜体做成空心，安装 6 扇活动门、右扳机交互与中文提示，" +
                "另存导航地图。两扇转角门保持固定；不改源场景、尺寸、包或 GitHub。", "安装并保存", "取消")) return;
            var font = EnsureFont();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install six kitchen cabinet doors");
            var controllerObject = NewChild("FurnitureInteractor", mover.transform);
            var interactor = Undo.AddComponent<ApartmentFurnitureInteractor>(controllerObject);
            interactor.locomotion = mover; interactor.hintFont = font; interactor.maxDistance = 2f;
            foreach (string cabinet in new[] { "CounterNorth", "CounterWest", "CounterSouth" })
                BuildShell(kitchen.Find(cabinet), cabinet == "CounterWest");
            interactor.doors = DoorPaths.Select((path, index) => BuildDoor(kitchen.Find(path), index, interactor)).ToArray();
            foreach (string cabinet in new[] { "CounterNorth", "CounterSouth" })
                Undo.AddComponent<BoxCollider>(kitchen.Find(cabinet + "/Door_0").gameObject);
            EditorUtility.SetDirty(interactor);
            Physics.SyncTransforms();
            Undo.RecordObject(mover.teleportSurface, "Update navigation for moving cabinet doors");
            mover.teleportSurface.BuildNavMesh();
            if (mover.teleportSurface.navMeshData == null) throw new InvalidOperationException("导航地图未生成。");
            string bakePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/test/ApartmentPhase2_VR_FurnitureNav.asset");
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData, bakePath);
            EditorUtility.SetDirty(mover.teleportSurface);
            EditorSceneManager.MarkSceneDirty(scene);
            ValidateOrThrow(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("VR 场景保存失败。");
            Undo.CollapseUndoOperations(group); Selection.activeGameObject = controllerObject;
            Debug.Log("M3 installation PASS: 6 doors, 3 hollow shells, Chinese hints, 2m right-trigger ray. Nav=" + bakePath +
                ". Existing locomotion settings unchanged; no Git commit/push. Quest acceptance still required.");
        }
        catch (Exception e) { Debug.LogError("M3 install stopped: " + e + "\nChanges remain available for inspection/Undo; no automatic deletion or discard."); }
    }

    static TMP_FontAsset EnsureFont()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null) { CheckGlyphs(existing); return existing; }
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSource);
        if (source == null) throw new InvalidOperationException("Noto Sans SC 字体未下载或导入。");
        var font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, false);
        if (font == null || !font.TryAddCharacters(RequiredCharacters(), out string missing))
            throw new InvalidOperationException("中文字形生成失败，请检查字体及图集容量。");
        font.name = "KitchenHints-SC"; font.atlasPopulationMode = AtlasPopulationMode.Static;
        font.hideFlags = HideFlags.None; font.material.hideFlags = HideFlags.None;
        AssetDatabase.CreateAsset(font, FontPath);
        font.material.name = "KitchenHints-SC Material"; AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var texture in font.atlasTextures)
        { texture.name = "KitchenHints-SC Atlas"; texture.hideFlags = HideFlags.None; AssetDatabase.AddObjectToAsset(texture, font); EditorUtility.SetDirty(texture); }
        EditorUtility.SetDirty(font); EditorUtility.SetDirty(font.material); AssetDatabase.SaveAssets(); CheckGlyphs(font);
        return font;
    }
    public static string RequiredCharacters() => new string((ApartmentCabinetDoor.FontCharacters +
        new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray())).Distinct().ToArray());
    static void CheckGlyphs(TMP_FontAsset font)
    {
        foreach (char c in RequiredCharacters()) if (!font.HasCharacter(c)) throw new InvalidOperationException("字体缺少字符：" + c);
        if (font.atlasTextures.Length != 1 || !AssetDatabase.Contains(font.material) || !AssetDatabase.Contains(font.atlasTextures[0]))
            throw new InvalidOperationException("字体图集/材质未正确保存。");
    }

    static void BuildShell(Transform cabinet, bool frontPositiveX)
    {
        var original = cabinet.Find("CabinetBody"); var size = original.localScale; float thickness = .018f;
        var material = original.GetComponent<MeshRenderer>().sharedMaterial;
        foreach (var component in new UnityEngine.Object[] { original.GetComponent<MeshRenderer>(), original.GetComponent<BoxCollider>() })
            Undo.RecordObject(component, "Keep disabled original solid body");
        original.GetComponent<MeshRenderer>().enabled = false; original.GetComponent<BoxCollider>().enabled = false;
        var shell = NewChild("InteractionShell", cabinet).transform; float y0 = original.localPosition.y - size.y * .5f;
        Panel(shell, "Bottom", new Vector3(0, y0 + thickness / 2, 0), new Vector3(size.x, thickness, size.z), material);
        Panel(shell, "Top", new Vector3(0, y0 + size.y - thickness / 2, 0), new Vector3(size.x, thickness, size.z), material);
        float insideHeight = size.y - 2 * thickness;
        // 3cm front reveal clears the hinges without changing cabinet footprint/closed door meshes.
        float reveal = .03f;
        if (frontPositiveX)
        {
            Panel(shell, "Back", new Vector3(-size.x/2 + thickness/2, y0 + size.y/2, 0), new Vector3(thickness, insideHeight, size.z), material);
            foreach (int sign in new[] { -1, 1 }) Panel(shell, sign < 0 ? "SideSouth" : "SideNorth",
                new Vector3(-reveal/2, y0 + size.y/2, sign * (size.z/2 - thickness/2)), new Vector3(size.x - reveal, insideHeight, thickness), material);
        }
        else
        {
            float front = cabinet.name == "CounterNorth" ? -1f : 1f;
            Panel(shell, "Back", new Vector3(0, y0 + size.y/2, -front * (size.z/2 - thickness/2)), new Vector3(size.x, insideHeight, thickness), material);
            foreach (int sign in new[] { -1, 1 }) Panel(shell, sign < 0 ? "SideWest" : "SideEast",
                new Vector3(sign * (size.x/2 - thickness/2), y0 + size.y/2, -front * reveal/2), new Vector3(thickness, insideHeight, size.z - reveal), material);
        }
        var volumeObject = NewChild("CabinetNoTeleport", cabinet);
        var volume = Undo.AddComponent<NavMeshModifierVolume>(volumeObject);
        volume.center = new Vector3(0, size.y/2, 0); volume.size = new Vector3(size.x, size.y + .2f, size.z);
        volume.area = NavMesh.GetAreaFromName("Not Walkable"); EditorUtility.SetDirty(volume);
    }
    static ApartmentCabinetDoor BuildDoor(Transform leaf, int index, ApartmentFurnitureInteractor owner)
    {
        var cabinet = leaf.parent; Vector3 pos = leaf.localPosition, size = leaf.localScale;
        Vector3 offset; float angle;
        switch (index)
        {
            case 0: offset = Vector3.left * size.x/2; angle = 90f; break;
            case 1: offset = Vector3.right * size.x/2; angle = -90f; break;
            case 2: offset = Vector3.forward * size.z/2; angle = -90f; break;
            case 3: offset = Vector3.back * size.z/2; angle = 90f; break;
            case 4: offset = Vector3.left * size.x/2; angle = -90f; break;
            default: offset = Vector3.right * size.x/2; angle = 90f; break;
        }
        var pivot = NewChild(leaf.name + "_Hinge", cabinet); pivot.transform.localPosition = pos + offset;
        Undo.SetTransformParent(leaf, pivot.transform, "Place original door under hinge");
        // Unity preserves world pose through SetTransformParent; explicitly preserve the original local geometry too.
        Undo.RecordObject(leaf, "Preserve closed door geometry"); leaf.localPosition = -offset; leaf.localRotation = Quaternion.identity; leaf.localScale = size;
        var solid = Undo.AddComponent<BoxCollider>(leaf.gameObject);
        var body = Undo.AddComponent<Rigidbody>(pivot); body.isKinematic = true; body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None; body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        var modifier = Undo.AddComponent<NavMeshModifier>(pivot); modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
        var door = Undo.AddComponent<ApartmentCabinetDoor>(pivot); door.doorCollider = solid;
        door.doorRenderer = leaf.GetComponent<MeshRenderer>(); door.hingeBody = body; door.inputOwner = owner;
        door.closedLocalRotation = Quaternion.identity; door.openAngle = angle; door.degreesPerSecond = 90f;
        EditorUtility.SetDirty(door); EditorUtility.SetDirty(body); EditorUtility.SetDirty(modifier);
        return door;
    }
    static GameObject NewChild(string name, Transform parent)
    { var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create " + name); go.transform.SetParent(parent, false); return go; }
    static void Panel(Transform parent, string name, Vector3 pos, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Undo.RegisterCreatedObjectUndo(go, "Create shell panel");
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }
    public static Scene RequireScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("请退出 Play，仅打开 ApartmentPhase2_VR 场景。");
        return scene;
    }
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<T>(true)).ToArray();
    public static Transform Find(Scene scene, string path)
    {
        int slash = path.IndexOf('/'); var root = scene.GetRootGameObjects().Single(o => o.name == path.Substring(0, slash));
        return root.transform.Find(path.Substring(slash + 1)) ?? throw new InvalidOperationException("Missing " + path);
    }
    [MenuItem("Apartment Phase 2/M3 Furniture/Validate Kitchen Interaction (Read Only)")]
    public static void Validate()
    { try { ValidateOrThrow(RequireScene()); Debug.Log("M3 scene validation PASS. Hardware acceptance still required."); } catch (Exception e) { Debug.LogError("M3 validation: " + e.Message); } }
    public static void ValidateOrThrow(Scene scene)
    {
        var interactor = All<ApartmentFurnitureInteractor>(scene).Single(); var mover = All<ApartmentVRLocomotion>(scene).Single();
        if (interactor.transform.parent != mover.transform || interactor.locomotion != mover || interactor.maxDistance != 2f ||
            mover.ConfigurationError() != null || mover.TeleportConfigurationError() != null || All<OVRCameraRig>(scene).Length != 1)
            throw new InvalidOperationException("Player/interactor configuration conflict.");
        if (interactor.doors == null || interactor.doors.Length != 6 || All<ApartmentCabinetDoor>(scene).Length != 6 ||
            interactor.doors.Distinct().Count() != 6) throw new InvalidOperationException("Expected exactly six unique cabinet doors.");
        CheckGlyphs(interactor.hintFont);
        foreach (var door in interactor.doors)
            if (door == null || door.ConfigurationError() != null || door.inputOwner != interactor ||
                !door.GetComponent<NavMeshModifier>().ignoreFromBuild || Mathf.Abs(door.openAngle) != 90f ||
                Vector3.Distance(door.transform.localScale, Vector3.one) > .001f)
                throw new InvalidOperationException("Invalid cabinet hinge.");
        var kitchen = Find(scene, KitchenPath);
        foreach (string name in new[] { "CounterNorth", "CounterWest", "CounterSouth" })
        {
            var cabinet = kitchen.Find(name);
            if (cabinet.Find("CabinetBody").GetComponent<MeshRenderer>().enabled || cabinet.Find("CabinetBody").GetComponent<BoxCollider>().enabled ||
                cabinet.Find("InteractionShell").childCount != 5 || cabinet.GetComponentsInChildren<NavMeshModifierVolume>().Length != 1)
                throw new InvalidOperationException("Invalid hollow cabinet shell: " + name);
        }
        foreach (string name in new[] { "CounterNorth", "CounterSouth" })
            if (kitchen.Find(name + "/Door_0").GetComponentInParent<ApartmentCabinetDoor>() != null ||
                kitchen.Find(name + "/Door_0").GetComponent<BoxCollider>() == null)
                throw new InvalidOperationException("Corner panels must remain solid and fixed.");
    }
}
#endif
