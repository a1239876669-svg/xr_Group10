#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// M9 installs the comfort sub page wiring: the Meta OVRVignette on the stereo camera, the driver that
// feeds it real motion, and the clamped profile entry point on the mover. It also tops up the static
// font atlas, because new menu labels cannot be drawn by a font asset that lacks their glyphs.
public static class ApartmentComfortUpgrade
{
    // Must match the path used by M8's "Apply Standalone UI Font".
    const string FontPath = "Assets/Apartment/Fonts/VRStandaloneUI-SC.asset";
    // Same source font M8 builds the atlas from (ApartmentFurnitureSetup.FontSource).
    const string FontSourcePath = "Assets/Apartment/Fonts/NotoSansSC-Regular.otf";
    const string VignetteShaderName = "Oculus/OVRVignette";
    const string DriverObjectName = "ComfortVignette";

    [MenuItem("Apartment Phase 2/M9 Comfort/Install Comfort Settings and Vignette %#&6")]
    public static void Install()
    {
        try
        {
            var scene = ApartmentMenuUpgrade.RequireScene();
            var hashes = ApartmentMenuUpgrade.Protected.Select(ApartmentMenuUpgrade.Hash).ToArray();
            var menu = ApartmentMenuUpgrade.All<ApartmentMenuController>(scene).Single();
            var mover = ApartmentMenuUpgrade.All<ApartmentVRLocomotion>(scene).Single();
            if (menu.locomotion != mover) throw new InvalidOperationException("菜单没有指向唯一的移动组件。");
            var camera = mover.rig == null || mover.rig.centerEyeAnchor == null ? null : mover.rig.centerEyeAnchor.GetComponent<Camera>();
            if (camera == null) throw new InvalidOperationException("找不到立体相机，无法安装暗角。");
            // Repaired first: a later label addition must be able to fix the atlas even when the rest of
            // the wiring is already installed, and validation would otherwise fail on the missing glyph.
            EnsureGlyphs();
            if (RendererFullyInstalled(menu, mover))
            {
                Validate(scene);
                Debug.Log("M9 已安装；仅重新校验，未创建重复对象。");
                return;
            }
            string backup = "Logs/M9Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(scene.path, backup + "/Apartment_Detailed_VR.unity");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Install apartment comfort settings");
            Undo.RecordObjects(new Object[] { menu, mover }, "Connect comfort settings");
            var vignette = camera.GetComponent<OVRVignette>();
            if (vignette == null) vignette = Undo.AddComponent<OVRVignette>(camera.gameObject);
            vignette.VignetteFieldOfView = 120f;   // above any camera field of view: nothing is drawn at rest
            vignette.VignetteFalloffDegrees = 24f;
            vignette.VignetteColor = Color.black;
            vignette.enabled = true;
            SetVignetteShader(vignette);
            var host = menu.transform.Find(DriverObjectName);
            if (host == null)
            {
                host = new GameObject(DriverObjectName).transform;
                Undo.RegisterCreatedObjectUndo(host.gameObject, "Create comfort vignette driver");
                host.SetParent(menu.transform, false);
            }
            var driver = host.GetComponent<ApartmentComfortVignette>();
            if (driver == null) driver = Undo.AddComponent<ApartmentComfortVignette>(host.gameObject);
            driver.locomotion = mover; driver.vignette = vignette;
            driver.strengthPercent = ApartmentComfortProfile.Default.vignettePercent;
            menu.comfortVignette = driver;
            // Written explicitly so the authored scene does not depend on C# field initializers surviving
            // a hot reload of an already instantiated component.
            mover.moveSmoothTime = ApartmentComfortProfile.Default.moveSmoothTime;
            mover.turnSmoothTime = ApartmentComfortProfile.Default.turnSmoothTime;
            foreach (var component in new Component[] { menu, mover, driver, vignette }) EditorUtility.SetDirty(component);
            Validate(scene);
            for (int i = 0; i < ApartmentMenuUpgrade.Protected.Length; i++)
                if (ApartmentMenuUpgrade.Hash(ApartmentMenuUpgrade.Protected[i]) != hashes[i])
                    throw new InvalidOperationException("Protected scene changed: " + ApartmentMenuUpgrade.Protected[i]);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("M9 已安装并保存：暗角驱动 + 舒适度子页面接线。备份：" + backup + "。普通游玩无需再次执行。");
        }
        catch (Exception e)
        {
            Debug.LogError("M9 安装停止：" + e.Message + "\n场景改动仍可撤销，未执行任何 Git 操作。");
        }
    }

    static bool RendererFullyInstalled(ApartmentMenuController menu, ApartmentVRLocomotion mover) =>
        menu.comfortVignette != null && menu.comfortVignette.vignette != null && mover.rig != null &&
        menu.comfortVignette.locomotion == mover && mover.rig.centerEyeAnchor.GetComponent<OVRVignette>() != null;

    // Diagnostics and repair for the one failure mode this feature can produce on its own: a newly added
    // label whose characters are absent from the static atlas renders as a blank box. Asset only, no
    // scene is opened or saved, so it is safe to run on its own.
    [MenuItem("Apartment Phase 2/M9 Comfort/Repair Menu Font Glyphs (Asset Only)")]
    public static void RepairGlyphs()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请退出 Play 并等待编译完成后再修复字体资产。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("字体资产不存在：" + FontPath);
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                Debug.Log("M9 字形检查：该字体为 Dynamic，字形按需生成，无需补字。");
                return;
            }
            string missing = MissingRequiredCharacters(font);
            // Always dump the full set so the GUI fallback (Font Asset Creator -> Update Atlas Texture)
            // can be driven by copy-paste rather than by hand-collecting characters.
            Directory.CreateDirectory("Logs");
            string requiredPath = "Logs/VRStandaloneUI-required-characters.txt";
            File.WriteAllText(requiredPath, ApartmentMenuUpgrade.RequiredCharacters());
            if (missing.Length == 0)
            {
                Debug.Log("M9 字形检查通过：独立界面字体已含全部所需字符（共 " + font.characterTable.Count + " 个），无需修复。完整字符集已写入 " + requiredPath + "。");
                return;
            }
            Debug.Log("M9 字形检查：缺少 " + missing.Length + " 个字符 [" + missing + "]，开始补充。完整字符集见 " + requiredPath + "。");
            EnsureGlyphs();
        }
        catch (Exception e) { Debug.LogError("M9 字体修复停止：" + e.Message); }
    }

    static string MissingRequiredCharacters(TMP_FontAsset font) =>
        new string(ApartmentMenuUpgrade.RequiredCharacters().Where(c => !font.HasCharacter(c)).Distinct().ToArray());

    // Recommended long-term setting. Measured capacity of this project's atlas (48 pt sampling, padding 5,
    // Alpha8): 2048x2048 holds about 1225 glyphs and 4096x4096 about 4900, so pre-generating the whole GBK
    // set (21886 characters) would need roughly 18 atlases / 71 MB of texture. A Dynamic atlas instead
    // rasterises on demand, which covers every character the source font provides (NotoSansSC-Regular.otf
    // maps 30890 code points, including 20976 of the 20992 BMP CJK ideographs) for the cost of shipping
    // that 7.95 MB font file.
    [MenuItem("Apartment Phase 2/M9 Comfort/Use Dynamic Menu Font (Any Chinese, Recommended)")]
    public static void UseDynamicFont()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请退出 Play 并等待编译完成。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("字体资产不存在：" + FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (source == null) throw new InvalidOperationException("源字体缺失：" + FontSourcePath);
            if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.sourceFontFile != null && font.isMultiAtlasTexturesEnabled)
            {
                Debug.Log("M9 菜单字体已是 Dynamic、已引用源字体并已启用多图集，无需改动。");
                return;
            }
            bool alreadyDynamic = font.atlasPopulationMode == AtlasPopulationMode.Dynamic && font.sourceFontFile != null;
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            // A Dynamic asset must keep the source font, otherwise new glyphs cannot be rasterised at runtime.
            if (font.sourceFontFile == null) AssignSourceFont(font, source);
            // Allow the atlas to grow past one texture if thousands of distinct characters are ever rendered.
            font.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(font);
            if (font.material != null) EditorUtility.SetDirty(font.material);
            AssetDatabase.SaveAssets();
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic || font.sourceFontFile == null)
                throw new InvalidOperationException("切换后自检失败：模式或源字体引用未生效。");
            Debug.Log(alreadyDynamic
                ? "M9 已为 Dynamic 菜单字体启用多图集兜底（图集写满时可自动追加纹理）。"
                : "M9 菜单字体已切换为 Dynamic。凡 " + FontSourcePath + " 支持的字符（含 GBK 全集）都会在首次渲染时自动生成，" +
                  "不再需要预生成或补字；源字体约 7.95 MB 会随包体发布，M5/M6/M7/M8 的预生成字形校验会自动跳过。");
        }
        catch (Exception e) { Debug.LogError("M9 切换 Dynamic 失败：" + e.Message); }
    }

    [MenuItem("Apartment Phase 2/M9 Comfort/Use Static Menu Font (Curated Atlas)")]
    public static void UseStaticFont()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请退出 Play 并等待编译完成。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null) throw new InvalidOperationException("字体资产不存在：" + FontPath);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.isMultiAtlasTexturesEnabled = false;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            string missing = MissingRequiredCharacters(font);
            Debug.Log(missing.Length == 0
                ? "M9 菜单字体已切回 Static，图集已含全部所需字符。"
                : "M9 菜单字体已切回 Static，图集仍缺 " + missing.Length + " 个字符 [" + missing + "]，请接着运行 Repair Menu Font Glyphs (Asset Only)。");
        }
        catch (Exception e) { Debug.LogError("M9 切回 Static 失败：" + e.Message); }
    }

    // The static atlas has to gain the new labels. M8's EnsureFont deliberately throws instead of
    // repairing, so the repair lives here and must run before M8 is asked to validate again.
    //
    // TMP refuses to add characters while the asset is Static (TMP_FontAsset.TryAddCharacters returns
    // false with "because its AtlasPopulationMode is set to Static"), and the scene references this asset
    // by GUID in three places, so the asset is switched to Dynamic for the write and switched back
    // afterwards instead of being deleted and recreated. LoadFontFace() re-resolves the source font from
    // the stored GUID on its own; the explicit assignment below is only a fallback.
    static void EnsureGlyphs()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new InvalidOperationException("独立界面字体缺失：请先运行 M8 的 Apply Standalone UI Font。");
        if (font.atlasPopulationMode == AtlasPopulationMode.Dynamic) return;
        string missing = MissingRequiredCharacters(font);
        if (missing.Length == 0) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (source == null) throw new InvalidOperationException("源字体缺失，无法补充字形：" + FontSourcePath);
        var original = font.atlasPopulationMode;
        string stillMissing = null;
        try
        {
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (font.sourceFontFile == null) AssignSourceFont(font, source);
            if (!font.TryAddCharacters(missing, out stillMissing))
            {
                // The editor-only source-font hook may not have resolved; retry once with it assigned.
                AssignSourceFont(font, source);
                font.TryAddCharacters(missing, out stillMissing);
            }
        }
        finally
        {
            font.atlasPopulationMode = original;
        }
        EditorUtility.SetDirty(font);
        if (font.material != null) EditorUtility.SetDirty(font.material);
        foreach (var texture in font.atlasTextures) if (texture != null) EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();
        string remaining = MissingRequiredCharacters(font);
        if (remaining.Length > 0)
            throw new InvalidOperationException("字形写入后仍缺少 [" + remaining + "]" +
                (string.IsNullOrEmpty(stillMissing) ? "" : "（TMP 报告：" + stillMissing + "）") +
                "。图集可能已满或字模缺失，请用 GUI 路径重新生成：Window → TextMeshPro → Font Asset Creator → 选中 " +
                FontPath + " → Update Atlas Texture（点号 48 / padding 5 / SDFAA / 2048x2048 保持不变，GUID 不变）。");
        Debug.Log("M9 已向独立界面字体补充 " + missing.Length + " 个字形 [" + missing + "]，当前共 " +
            font.characterTable.Count + " 个字符。");
    }

    static void AssignSourceFont(TMP_FontAsset font, Font source)
    {
        var serialized = new SerializedObject(font);
        var property = serialized.FindProperty("m_SourceFontFile");
        if (property == null) return;   // TMP layout changed; LoadFontFace still resolves the stored GUID
        property.objectReferenceValue = source;
        serialized.ApplyModifiedProperties();
    }

    // A scene reference is what keeps the shader in the player build. OVRVignette only falls back to
    // Shader.Find when this field is empty, and Shader.Find can be stripped from a build.
    static void SetVignetteShader(OVRVignette vignette)
    {
        var shader = Shader.Find(VignetteShaderName);
        if (shader == null) throw new InvalidOperationException("找不到着色器 " + VignetteShaderName + "。");
        var serialized = new SerializedObject(vignette);
        var property = serialized.FindProperty("VignetteShader");
        if (property == null) throw new InvalidOperationException("OVRVignette 结构已变化，请人工检查着色器引用。");
        property.objectReferenceValue = shader;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(vignette);
    }

    static Shader VignetteShader(OVRVignette vignette) =>
        new SerializedObject(vignette).FindProperty("VignetteShader")?.objectReferenceValue as Shader;

    public static void Validate(Scene scene)
    {
        ApartmentMenuUpgrade.Validate(scene);
        var menu = ApartmentMenuUpgrade.All<ApartmentMenuController>(scene).Single();
        var mover = ApartmentMenuUpgrade.All<ApartmentVRLocomotion>(scene).Single();
        var driver = menu.comfortVignette;
        if (driver == null || driver.locomotion != mover || driver.transform.parent != menu.transform)
            throw new InvalidOperationException("舒适度暗角驱动未接线。");
        if (driver.vignette == null || driver.vignette.gameObject != mover.rig.centerEyeAnchor.gameObject)
            throw new InvalidOperationException("暗角必须挂在立体相机上。");
        if (VignetteShader(driver.vignette) == null || Shader.Find(VignetteShaderName) == null)
            throw new InvalidOperationException("暗角着色器引用为空，打包后会静默失效。");
        if (AssetDatabase.GetAssetPath(menu.font) != FontPath)
            throw new InvalidOperationException("菜单字体不是独立界面字体：" + AssetDatabase.GetAssetPath(menu.font));
        if (mover.ConfigurationError() != null) throw new InvalidOperationException("移动配置无效：" + mover.ConfigurationError());
        if (mover.moveSmoothTime < 0f || mover.moveSmoothTime > 1f || mover.turnSmoothTime < 0f || mover.turnSmoothTime > 1f)
            throw new InvalidOperationException("速度/转向平滑时间超出支持范围。");
        // A Dynamic atlas populates on demand, so HasCharacter only turns true after a glyph is first used.
        if (menu.font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            foreach (char c in ApartmentMenuUpgrade.RequiredCharacters())
                if (!menu.font.HasCharacter(c)) throw new InvalidOperationException("缺少字形：" + c);
    }

    [MenuItem("Apartment Phase 2/M9 Comfort/Forget Saved Comfort Settings")]
    public static void ForgetSaved()
    {
        ApartmentComfortStore.Clear();
        Debug.Log("已删除 PlayerPrefs 中的舒适度记录（" + ApartmentComfortStore.Key + "）；下次进入 Play 使用标准预设。");
    }

    [Serializable] public sealed class Report { public int passed; public List<string> failures = new List<string>(); public bool questVerified; public string[] previews; }
    static Report report;
    static void Assert(bool value, string description) { if (value) report.passed++; else report.failures.Add(description); }

    [MenuItem("Apartment Phase 2/M9 Comfort/Run Comfort Checks %#&7")]
    public static void Check()
    {
        report = new Report();
        bool hadSaved = PlayerPrefs.HasKey(ApartmentComfortStore.Key);
        string saved = hadSaved ? PlayerPrefs.GetString(ApartmentComfortStore.Key, "") : "";
        Scene original = default; GameObject go = null;
        var previews = new List<string>();
        try
        {
            original = ApartmentMenuUpgrade.RequireScene();
            string before = File.ReadAllText(original.path);
            Validate(original);
            var source = ApartmentMenuUpgrade.All<ApartmentMenuController>(original).Single();
            if (ApartmentExperienceUpgrade.RequiresPreloadedGlyphs(source.font))
                foreach (char c in ApartmentMenuUpgrade.RequiredCharacters()) Assert(source.font.HasCharacter(c), "glyph " + c);

            CheckProfile();
            CheckStore();
            CheckVignetteMath();
            CheckRamp();

            // Edit-mode fixture: real UI objects and real hit testing, no tracked headset input.
            go = new GameObject("Comfort preview scratch");
            var menu = go.AddComponent<ApartmentMenuController>();
            menu.font = source.font; menu.panelShader = source.panelShader; menu.textOverlayShader = source.textOverlayShader;
            var mover = go.AddComponent<ApartmentVRLocomotion>();
            mover.rig = source.locomotion.rig; menu.locomotion = mover;
            typeof(ApartmentMenuController).GetProperty("InputReady").SetValue(menu, true);
            menu.BuildComfortPreview();
            Assert(menu.Page == 4 && menu.ComfortSection == 1, "comfort sub page opens inside the settings page");
            Render(go, menu.PanelRect, "Logs/M9-Comfort.png"); previews.Add("Logs/M9-Comfort.png");

            CheckRows(menu, mover);
            CheckFlatRows(menu, mover);
            CheckPersistence(menu);
            menu.SelectSnapTurn(true);
            Assert(menu.Comfort.snapTurn && mover.turnMode == ApartmentTurnMode.Snap, "snap turn reaches the mover");
            Render(go, menu.PanelRect, "Logs/M9-Comfort-Snap.png"); previews.Add("Logs/M9-Comfort-Snap.png");
            menu.SelectSnapTurn(false);
            Assert(mover.turnMode == ApartmentTurnMode.Smooth, "smooth turning restored");

            // The settings page keeps its original contract: three movement modes and the safety reset.
            menu.BuildPreview(4);
            Assert(menu.ComfortSection == 0, "returning to the settings page leaves the sub page");
            Render(go, menu.PanelRect, "Logs/M9-Settings.png"); previews.Add("Logs/M9-Settings.png");
            Assert(menu.PanelRect.Find("Mode 0") != null && menu.PanelRect.Find("Mode 2") != null && menu.PanelRect.Find("Safety reset") != null,
                "settings page still offers the three modes and the safety reset");
            Click(menu, "Comfort entry");
            Assert(menu.ComfortSection == 1, "comfort entry opens the sub page");
            Click(menu, "Comfort back");
            Assert(menu.ComfortSection == 0, "back returns to the settings page");
            Click(menu, "Comfort entry");
            Click(menu, "Tab 2");
            Assert(menu.Page == 2 && menu.ComfortSection == 0, "switching tabs leaves the sub page");
            CheckPageGlyphs(menu, source.font);
            Assert(File.ReadAllText(original.path) == before, "check changed the saved scene");
        }
        catch (Exception e) { report.failures.Add(e.ToString()); }
        finally
        {
            if (go != null) Object.DestroyImmediate(go);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            if (hadSaved) PlayerPrefs.SetString(ApartmentComfortStore.Key, saved);
            else PlayerPrefs.DeleteKey(ApartmentComfortStore.Key);
            PlayerPrefs.Save();
            report.previews = previews.ToArray();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/M9ComfortChecks.json", JsonUtility.ToJson(report, true));
            if (report.failures.Count == 0) Debug.Log("M9 检查通过：" + report.passed + " 项断言；未做 Quest 真机验收。");
            else Debug.LogError("M9 检查失败：" + string.Join(" | ", report.failures));
        }
    }

    // Anything a stored or hand-edited profile can contain must be pulled back into the supported
    // window, because an out-of-range value disables VR input entirely (including this menu).
    static void CheckProfile()
    {
        var garbage = new ApartmentComfortProfile
        {
            vignettePercent = 999f, moveSpeed = float.NaN, moveSmoothTime = -3f,
            turnSpeed = 5000f, turnSmoothTime = 9f, snapTurn = true, snapAngle = 37f
        };
        Assert(garbage.Clamp(), "garbage profile reports a repair");
        Assert(garbage.vignettePercent == ApartmentComfortProfile.VignetteMax, "vignette clamped to its maximum");
        Assert(garbage.moveSpeed == ApartmentComfortProfile.SpeedMin, "non-finite speed falls back to the minimum");
        Assert(garbage.moveSmoothTime == ApartmentComfortProfile.SpeedSmoothMin, "negative ramp clamped");
        Assert(garbage.turnSpeed == ApartmentComfortProfile.TurnSpeedMax, "turn rate clamped");
        Assert(garbage.turnSmoothTime == ApartmentComfortProfile.TurnSmoothMax, "turn ramp clamped");
        Assert(Mathf.Abs(garbage.snapAngle - 30f) < .001f, "snap angle rounded to a supported value");
        Assert(!garbage.Clamp(), "a repaired profile is stable");
        var offStep = new ApartmentComfortProfile { moveSpeed = 1.23f, turnSpeed = 67f };
        offStep.Clamp();
        Assert(Mathf.Abs(offStep.moveSpeed - 1.2f) < .001f && Mathf.Abs(offStep.turnSpeed - 60f) < .001f, "values land on a step boundary");
        // Every preset must already be legal, otherwise a preset would silently rewrite itself.
        for (int index = 0; index < 3; index++)
            Assert(!ApartmentComfortProfile.Preset(index).Clamp(), "preset " + ApartmentComfortProfile.PresetName(index) + " is already in range");
        Assert(ApartmentMenuController.MatchesPreset(ApartmentComfortProfile.Comfort, 0) &&
            !ApartmentMenuController.MatchesPreset(ApartmentComfortProfile.Comfort, 2), "preset highlight matches values");
    }

    static void CheckStore()
    {
        var profile = ApartmentComfortProfile.Preset(2);
        ApartmentComfortStore.Save(profile);
        var restored = ApartmentComfortStore.Load();
        Assert(restored.moveSpeed == profile.moveSpeed && restored.vignettePercent == profile.vignettePercent &&
            restored.turnSmoothTime == profile.turnSmoothTime && restored.snapAngle == profile.snapAngle, "profile survives a store round trip");
        PlayerPrefs.SetString(ApartmentComfortStore.Key, "{not json");
        var recovered = ApartmentComfortStore.Load();
        Assert(!PlayerPrefs.HasKey(ApartmentComfortStore.Key), "a corrupt record is dropped instead of being trusted");
        Assert(recovered.moveSpeed == ApartmentComfortProfile.Default.moveSpeed, "a corrupt record falls back to the default profile");
        PlayerPrefs.SetString(ApartmentComfortStore.Key, JsonUtility.ToJson(new ApartmentComfortProfile { moveSpeed = 99f }));
        var repaired = ApartmentComfortStore.Load();
        Assert(repaired.moveSpeed == ApartmentComfortProfile.SpeedMax, "an out-of-range stored value is clamped on load");
        ApartmentComfortStore.Clear();
    }

    // The comfort requirement in one place: the start-of-motion ease is separate from the brake-to-stop,
    // both are adjustable, and stop 0 means the motion ends in the same frame with zero further travel.
    static void CheckRamp()
    {
        const float full = 1.2f, smooth = .25f, stopZero = 0f, dt = 1f / 72f;
        var start = new ApartmentMotionRamp();
        float value = 0f; int frames = 0;
        while (value < full - .0001f && frames < 2000) { value = start.Step(value, full, full, smooth, stopZero, dt, false); frames++; }
        Assert(frames > 1 && Mathf.Abs(value - full) < .001f && !start.IsRamping, "a start from rest is ramped up to the target");
        Assert(Mathf.Abs(frames - smooth / dt) <= 2f, "the ramp takes about smoothTime (frames=" + frames + ")");
        var release = new ApartmentMotionRamp();
        float partial = 0f; for (int i = 0; i < 5; i++) partial = release.Step(partial, full, full, smooth, stopZero, dt, false);
        Assert(partial > 0f && partial < full, "the value is still ramping after 5 frames");
        Assert(release.Step(partial, 0f, full, smooth, stopZero, dt, false) == 0f, "stop 0 ends the motion in the same frame");
        Assert(!release.IsRamping, "the ramp state is cleared on release");
        Assert(new ApartmentMotionRamp().Step(partial, full, full, smooth, stopZero, dt, true) == 0f, "a hard stop collapses to zero in the same frame");
        float settled = 0f; var steady = new ApartmentMotionRamp();
        while (settled < full - .0001f && settled >= 0f) { float next = steady.Step(settled, full, full, smooth, stopZero, dt, false); if (next <= settled) break; settled = next; }
        Assert(steady.Step(settled, full * .5f, full, smooth, stopZero, dt, false) == full * .5f, "slowing down mid-motion is immediate");
        Assert(steady.Step(settled, full, full, smooth, stopZero, dt, false) == full, "speeding up after the ramp finished is immediate");
        Assert(new ApartmentMotionRamp().Step(0f, full, full, 0f, stopZero, dt, false) == full, "smoothTime 0 keeps the instant start");
        var turning = new ApartmentMotionRamp();
        float turn = 0f; int turnFrames = 0;
        while (turn < 60f - .0001f && turnFrames < 2000) { turn = turning.Step(turn, 60f, 60f, .2f, stopZero, dt, false); turnFrames++; }
        Assert(turnFrames > 1 && Mathf.Abs(turn - 60f) < .001f, "the turn rate ramps up from rest");
        Assert(turning.Step(turn, 0f, 60f, .2f, stopZero, dt, false) == 0f, "centring the stick stops the turn in the same frame with stop 0");
        var travel = new ApartmentMotionRamp();
        float speed = 0f, distance = 0f;
        for (int i = 0; i < 40; i++) { speed = travel.Step(speed, full, full, smooth, stopZero, dt, false); distance += speed * dt; }
        float cruising = distance;
        for (int i = 0; i < 10; i++) { speed = travel.Step(speed, 0f, full, smooth, stopZero, dt, false); distance += speed * dt; }
        Assert(Mathf.Abs(distance - cruising) < 1e-6f, "zero distance is travelled after release with stop 0");
        var monotonic = new ApartmentMotionRamp();
        float ramped = 0f; bool overshoot = false;
        for (int i = 0; i < 400; i++)
        {
            float target = (i % 40 < 20) ? full : full * .3f;
            ramped = monotonic.Step(ramped, target, full, smooth, stopZero, dt, false);
            if (ramped > target + .0001f) overshoot = true;
        }
        Assert(!overshoot, "the ramped value never exceeds its target");

        // Independent stop smoothing: braking from full speed takes about stopSmoothTime, and the two
        // phases do not leak into each other.
        const float brake = .20f;
        var braking = new ApartmentMotionRamp();
        float cruise = 0f;
        for (int i = 0; i < 200; i++) cruise = braking.Step(cruise, full, full, smooth, brake, dt, false);
        Assert(Mathf.Abs(cruise - full) < .001f, "a ramped start still reaches the target when stop smoothing is configured");
        float braked = braking.Step(cruise, 0f, full, smooth, brake, dt, false);
        Assert(braked > 0f && braked < cruise, "stop smoothing brakes instead of stopping dead");
        int brakeFrames = 1;
        while (braked > 0f && brakeFrames < 2000) { braked = braking.Step(braked, 0f, full, smooth, brake, dt, false); brakeFrames++; }
        Assert(braked == 0f, "braking reaches a complete stop");
        Assert(Math.Abs(brakeFrames - brake / dt) <= 2f, "braking from full speed takes about stopSmoothTime (frames=" + brakeFrames + ")");
        var coast = new ApartmentMotionRamp();
        float speed2 = 0f, distance2 = 0f;
        for (int i = 0; i < 200; i++) { speed2 = coast.Step(speed2, full, full, smooth, brake, dt, false); distance2 += speed2 * dt; }
        float before = distance2;
        for (int i = 0; i < 60; i++) { speed2 = coast.Step(speed2, 0f, full, smooth, brake, dt, false); distance2 += speed2 * dt; }
        float coasted = distance2 - before;
        Assert(coasted > 0f, "stop smoothing produces a bounded glide instead of an instant stop");
        Assert(coasted < full * brake, "the glide never exceeds full speed times stopSmoothTime");
        Assert(new ApartmentMotionRamp().Step(full, 0f, full, smooth, 0f, dt, false) == 0f, "stop 0 still stops in the same frame");
        // The two parameters are independent: an instant start can coexist with a braked stop.
        var mixed = new ApartmentMotionRamp();
        Assert(mixed.Step(0f, full, full, 0f, brake, dt, false) == full, "start 0 stays instant while stop smoothing is configured");
        Assert(mixed.Step(full, 0f, full, 0f, brake, dt, false) > 0f, "and that same value still brakes to a stop");
    }

    static void CheckVignetteMath()
    {
        const float ceiling = 120f, minimum = 45f;
        Assert(ApartmentComfortVignette.TargetClearFov(0f, 1f, ceiling, minimum) >= ceiling - .001f, "zero strength never darkens");
        Assert(ApartmentComfortVignette.TargetClearFov(40f, 0f, ceiling, minimum) >= ceiling - .001f, "no motion never darkens");
        Assert(ApartmentComfortVignette.TargetClearFov(80f, 1f, ceiling, minimum) <= minimum + .001f, "full strength and motion reach the strongest darkening");
        float previous = float.MaxValue;
        for (float strength = 0f; strength <= 80f; strength += 10f)
        {
            float target = ApartmentComfortVignette.TargetClearFov(strength, 1f, ceiling, minimum);
            Assert(target <= previous + .001f, "strength " + strength + " never lightens the vignette");
            Assert(target >= minimum - .001f, "strength " + strength + " stays above the minimum clear field of view");
            previous = target;
        }
    }

    // Fuzz every row in both directions through the real stick-step path and confirm the mover never
    // receives a value outside the window its own configuration check enforces.
    static void CheckRows(ApartmentMenuController menu, ApartmentVRLocomotion mover)
    {
        menu.RestoreDefaultComfort();
        Assert(ApartmentMenuController.MatchesPreset(menu.Comfort, 1), "restore returns the standard preset");
        string[] rows = { "Comfort vignette", "Comfort speed", "Comfort move start", "Comfort move stop",
            "Comfort turn speed", "Comfort turn start", "Comfort turn stop" };
        for (int row = 0; row < rows.Length; row++)
        {
            int before = row == 0 ? Mathf.RoundToInt(menu.Comfort.vignettePercent) : -1;
            Step(menu, rows[row], +1);
            if (row == 0) Assert(Mathf.RoundToInt(menu.Comfort.vignettePercent) == before + 10, "stick up steps the vignette one notch");
            for (int i = 0; i < 200; i++) Step(menu, rows[row], +1);
            for (int i = 0; i < 200; i++) Step(menu, rows[row], -1);
            RowInRange(menu, mover, rows[row]);
        }
        // The two smoothing cells of one row are separate controls: stepping one must not touch the other.
        menu.RestoreDefaultComfort();
        float start = menu.Comfort.moveSmoothTime, stop = menu.Comfort.moveStopSmoothTime;
        Step(menu, "Comfort move start", +1);
        Assert(menu.Comfort.moveSmoothTime > start && menu.Comfort.moveStopSmoothTime == stop,
            "the start cell steps only the start value");
        Step(menu, "Comfort move stop", +1);
        Assert(menu.Comfort.moveStopSmoothTime > stop, "the stop cell steps the stop value");
        Assert(menu.Comfort.moveSmoothTime > start, "and leaves the start value alone");
        // A missing recentre must not step twice.
        var latch = new ApartmentMenuStickStepLatch();
        Assert(latch.Sample(.9f, .2f) == 1, "a fresh stick flick steps once");
        Assert(latch.Sample(.9f, .2f) == 0, "a held stick does not step again");
        latch.Sample(0f, .2f);
        Assert(latch.Sample(-.9f, .2f) == -1, "the opposite direction steps after a recentre");
    }

    // With snap turn selected the three continuous-turn controls render flat, so they must not respond.
    static void CheckFlatRows(ApartmentMenuController menu, ApartmentVRLocomotion mover)
    {
        menu.SelectSnapTurn(true);
        float rate = menu.Comfort.turnSpeed, start = menu.Comfort.turnSmoothTime, stop = menu.Comfort.turnStopSmoothTime;
        Step(menu, "Comfort turn speed", +1);
        Step(menu, "Comfort turn start", +1);
        Step(menu, "Comfort turn stop", +1);
        Assert(menu.Comfort.turnSpeed == rate && menu.Comfort.turnSmoothTime == start && menu.Comfort.turnStopSmoothTime == stop,
            "flat turning controls ignore the stick");
        Assert(menu.PanelRect.Find("Comfort turn start minus") != null, "flat controls still render their placeholders");
        Click(menu, "Comfort turn start minus");
        Assert(menu.Comfort.turnSmoothTime == start, "flat controls ignore a trigger click as well");
        Step(menu, "Comfort snap angle", +1);
        Assert(Mathf.Abs(menu.Comfort.snapAngle - 60f) < .001f, "snap angle steps while snap turn is active");
        Step(menu, "Comfort snap angle", +1);
        Assert(Mathf.Abs(menu.Comfort.snapAngle - 60f) < .001f, "snap angle stops at its maximum");
        Assert(mover.snapAngle == menu.Comfort.snapAngle, "the mover receives the snap angle");
    }

    static void RowInRange(ApartmentMenuController menu, ApartmentVRLocomotion mover, string row)
    {
        var c = menu.Comfort;
        Assert(c.vignettePercent >= ApartmentComfortProfile.VignetteMin && c.vignettePercent <= ApartmentComfortProfile.VignetteMax, row + " vignette in range");
        Assert(c.moveSpeed >= ApartmentComfortProfile.SpeedMin && c.moveSpeed <= ApartmentComfortProfile.SpeedMax, row + " speed in range");
        Assert(c.moveSmoothTime >= ApartmentComfortProfile.SpeedSmoothMin && c.moveSmoothTime <= ApartmentComfortProfile.SpeedSmoothMax, row + " start ramp in range");
        Assert(c.moveStopSmoothTime >= ApartmentComfortProfile.MoveStopMin && c.moveStopSmoothTime <= ApartmentComfortProfile.MoveStopMax, row + " stop ramp in range");
        Assert(c.turnSpeed >= ApartmentComfortProfile.TurnSpeedMin && c.turnSpeed <= ApartmentComfortProfile.TurnSpeedMax, row + " turn rate in range");
        Assert(c.turnSmoothTime >= ApartmentComfortProfile.TurnSmoothMin && c.turnSmoothTime <= ApartmentComfortProfile.TurnSmoothMax, row + " turn ramp in range");
        Assert(c.turnStopSmoothTime >= ApartmentComfortProfile.TurnStopMin && c.turnStopSmoothTime <= ApartmentComfortProfile.TurnStopMax, row + " turn stop ramp in range");
        Assert(mover.moveSpeed == c.moveSpeed && mover.smoothTurnSpeed == c.turnSpeed &&
            mover.moveSmoothTime == c.moveSmoothTime && mover.turnSmoothTime == c.turnSmoothTime &&
            mover.moveStopSmoothTime == c.moveStopSmoothTime && mover.turnStopSmoothTime == c.turnStopSmoothTime,
            row + " reached the mover verbatim");
        // Mirrors ApartmentVRLocomotion.ConfigurationError(): an out-of-window value would disable all
        // VR input, including the menu the value came from.
        Assert(mover.moveSpeed > 0f && mover.moveSpeed <= 3f &&
            mover.smoothTurnSpeed >= 15f && mover.smoothTurnSpeed <= 180f &&
            mover.moveSmoothTime >= 0f && mover.moveSmoothTime <= 1f &&
            mover.turnSmoothTime >= 0f && mover.turnSmoothTime <= 1f &&
            mover.moveStopSmoothTime >= 0f && mover.moveStopSmoothTime <= 1f &&
            mover.turnStopSmoothTime >= 0f && mover.turnStopSmoothTime <= 1f &&
            mover.snapAngle > 0f && mover.snapAngle <= 90f &&
            (mover.turnMode == ApartmentTurnMode.Snap || mover.turnMode == ApartmentTurnMode.Smooth),
            row + " stays inside the mover's supported window");
    }

    static void CheckPersistence(ApartmentMenuController menu)
    {
        menu.SelectComfortPreset(0);
        Assert(ApartmentComfortStore.Load().moveSpeed == ApartmentComfortProfile.Comfort.moveSpeed, "choosing a preset is saved immediately");
        Step(menu, "Comfort row 0", +1);
        Assert(ApartmentComfortStore.Load().vignettePercent == menu.Comfort.vignettePercent, "every notch is saved immediately");
        menu.RestoreDefaultComfort();
        Assert(ApartmentComfortStore.Load().moveSpeed == ApartmentComfortProfile.Default.moveSpeed, "restore default is saved immediately");
    }

    // RequiredCharacters() drives the atlas, but a literal typed straight into a page would silently
    // render a blank box. This sweeps what the pages actually ask the font for, on every tab.
    static void CheckPageGlyphs(ApartmentMenuController menu, TMP_FontAsset font)
    {
        for (int page = 0; page < ApartmentMenuController.Tabs.Length; page++)
        {
            menu.BuildPreview(page);
            Assert(MissingGlyphs(menu.PanelRect, font).Count == 0,
                "tab " + page + " renders glyphs the font lacks: " + new string(MissingGlyphs(menu.PanelRect, font).ToArray()));
        }
        menu.BuildComfortPreview();
        Assert(MissingGlyphs(menu.PanelRect, font).Count == 0,
            "the comfort sub page renders glyphs the font lacks: " + new string(MissingGlyphs(menu.PanelRect, font).ToArray()));
    }

    static HashSet<char> MissingGlyphs(RectTransform panel, TMP_FontAsset font)
    {
        var missing = new HashSet<char>();
        if (panel == null) return missing;
        // A Dynamic atlas populates on demand, so a blank-box sweep cannot be judged before first render.
        if (!ApartmentExperienceUpgrade.RequiresPreloadedGlyphs(font)) return missing;
        foreach (var label in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
            foreach (char c in label.text)
                if (!char.IsControl(c) && !font.HasCharacter(c)) missing.Add(c);
        return missing;
    }

    static void Step(ApartmentMenuController menu, string rowName, int direction)
    {
        var rect = menu.PanelRect.Find(rowName).GetComponent<RectTransform>();
        Vector3 point = rect.TransformPoint(rect.rect.center);
        menu.ProcessPointer(new Ray(point - rect.forward, rect.forward), 2f, false, out _, direction);
    }

    static void Click(ApartmentMenuController menu, string name)
    {
        var rect = menu.PanelRect.Find(name).GetComponent<RectTransform>();
        Vector3 point = rect.TransformPoint(rect.rect.center);
        menu.ProcessPointer(new Ray(point - rect.forward, rect.forward), 2f, true, out _);
    }

    // Deliberately a local copy of the M6 preview renderer: ApartmentMenuUpgrade.Render reports text
    // overflow through M6's own private report, so calling it from here would throw.
    static void Render(GameObject go, RectTransform panel, string path)
    {
        panel.position = new Vector3(1000, 1000, 1001.2f); panel.rotation = Quaternion.identity;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        var camera = new GameObject("M9 preview camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(1000, 1000, 1000); camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .16f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(panel.sizeDelta.y, panel.sizeDelta.x / 1.4f) * .001f * .55f;
        panel.GetComponent<Canvas>().worldCamera = camera;
        RenderTexture texture = null; Texture2D png = null; var previous = RenderTexture.active;
        try
        {
            texture = new RenderTexture(1400, 1000, 24); camera.targetTexture = texture;
            Canvas.ForceUpdateCanvases();
            foreach (var label in go.GetComponentsInChildren<TextMeshProUGUI>())
            {
                label.ForceMeshUpdate();
                Assert(!label.isTextOverflowing, "text overflow: " + path + " " + label.name);
            }
            camera.Render(); RenderTexture.active = texture;
            png = new Texture2D(1400, 1000, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); png.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, png.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(camera.gameObject);
            if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
            if (png != null) Object.DestroyImmediate(png);
        }
    }
}
#endif
