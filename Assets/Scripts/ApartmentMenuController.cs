using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ApartmentMovementMode { SmoothOnly, TeleportOnly, Both }
public enum ApartmentRelocationReason { AimedTeleport, SafetyReset }
public enum ApartmentPanelState { Closed, Menu, TourComplete, QuitConfirmation }

// Explicit release-to-arm: focus recovery and held B never toggle a newly opened view.
public sealed class ApartmentMenuButtonLatch
{
    bool armed;
    public void Reset() { armed = false; }
    public bool Sample(bool held, bool ready)
    {
        if (!ready) { Reset(); return false; }
        if (!held) { armed = true; return false; }
        if (!armed) return false;
        armed = false; return true;
    }
}

// One value step per stick flick. The stick must return inside the dead zone before the next step,
// so holding a direction cannot run a setting away.
public sealed class ApartmentMenuStickStepLatch
{
    bool armed;
    public void Reset() { armed = false; }
    public int Sample(float vertical, float deadZone)
    {
        if (float.IsNaN(vertical) || float.IsInfinity(vertical)) { Reset(); return 0; }
        if (Mathf.Abs(vertical) <= deadZone) { armed = true; return 0; }
        if (!armed) return 0;
        armed = false;
        return vertical > 0f ? 1 : -1;
    }
}

[DisallowMultipleComponent, DefaultExecutionOrder(8500)]
public sealed class ApartmentMenuController : MonoBehaviour
{
    public ApartmentVRLocomotion locomotion;
    public ApartmentFurnitureInteractor interactor;
    public ApartmentTutorialController tutorial;
    public Transform safeResetPoint;
    public ApartmentTourTracker tour;
    public ApartmentComfortVignette comfortVignette;
    public TMP_FontAsset font;
    public Shader panelShader, textOverlayShader;
    public ApartmentMovementMode MovementMode { get; private set; } = ApartmentMovementMode.Both;
    public ApartmentPanelState ActivePanel { get; private set; }
    public bool IsOpen => ActivePanel == ApartmentPanelState.Menu;
    public bool HasOpenPanel => ActivePanel != ApartmentPanelState.Closed;
    public bool InputReady { get; private set; }
    public bool Visible => HasOpenPanel && InputReady;
    public bool BlocksWorldInput => !InputReady || HasOpenPanel || waitingNeutral;
    public bool AllowsSmoothMove => MovementMode != ApartmentMovementMode.TeleportOnly;
    public bool AllowsTeleport => MovementMode != ApartmentMovementMode.SmoothOnly;
    public int UIRevision { get; private set; }
    public bool ConfirmingReset { get; private set; }
    public bool ShowingTourCompletion => ActivePanel == ApartmentPanelState.TourComplete;
    public int Page { get; private set; } = 2;
    // Comfort values live here and are pushed to the mover/vignette from one clamped entry point.
    public ApartmentComfortProfile Comfort { get; private set; } = ApartmentComfortProfile.Default;
    // 0 = the settings page itself, 1 = the comfort sub page. Deliberately not a Page value: the tab
    // array and SetPage treat index 5 as "quit" and page bounds are enforced against Tabs.Length.
    public int ComfortSection { get; private set; }
    public const int ComfortRowVignette = 0, ComfortRowSpeed = 1, ComfortRowMoveStart = 2, ComfortRowMoveStop = 3,
        ComfortRowTurnSpeed = 4, ComfortRowTurnStart = 5, ComfortRowTurnStop = 6, ComfortRowSnapAngle = 7;
    // Sub-page grid: eight rows fit between the page title and the fixed footer inside the unchanged
    // 1100x740 panel (190 + 7 * 62 + 54 = 678 < 690).
    public const float ComfortX = 330f, ComfortW = 720f, ComfortTop = 190f, ComfortStep = 62f, ComfortRowHeight = 54f;
    public const string MenuTitle = "公寓菜单";
    public static readonly string[] Tabs = { "测距", "标记注释", "按键说明", "我的物品", "设置", "退出游戏" };
    public const string ExitQuestion = "退出游戏";
    public const string TourTitle = "您已看完整个公寓，您可以继续探索和标记注释或者退出游戏";
    public static readonly string[] Modes = { "平滑移动", "A 键瞬移", "两种都使用" };
    public static readonly string[] ModeDetails = { "使用左摇杆行走", "按住 A 瞄准，松开后传送", "左摇杆行走 + A 键瞬移" };
    public const string Footer = "右手指向按钮 · 扣食指扳机确认";
    public const string NeutralText = "请松开扳机、A、B，并让摇杆回中";
    public const string ChooseHelp = "教学：进入设置，点击一种移动方式，再按 B 关闭";
    public const string ResetQuestion = "返回出生安全点？";
    public const string ResetHelp = "保持当前朝向，返回初始安全位置。\n不会重置门或当前教学进度。";
    public const string ComfortTitle = "舒适度调节";
    public const string ComfortPresetTitle = "舒适度预设";
    public const string TurnModeTitle = "转向方式";
    public const string TurnSmoothLabel = "平滑";
    public const string TurnSnapLabel = "快照";
    public const string SnapAngleTitle = "角度";
    // Start-of-motion ease and brake-to-stop are separate, independently adjustable values per axis, so
    // the two cells share one row and are labelled with these.
    public const string SmoothStartLabel = "起步";
    public const string SmoothStopLabel = "停止";
    public const string RestoreDefault = "恢复默认";
    public const string ComfortBack = "返回";
    public const string ComfortFooter = "右摇杆上下  调节数值 · 扣食指扳机确认";
    public static readonly string[] PresetNames = { "舒适", "标准", "灵敏" };
    // Titles of the plain full-width value rows. The two paired rows carry their own titles plus the
    // 起步 / 停止 cell labels.
    public static readonly string[] ComfortRowTitles = { "移动暗角", "最大移动速度", "移动平滑", "转向速度", "转向平滑" };
    // Every comfort label and unit in one place. RequiredCharacters() is derived from this text, so a
    // new label that is missing here fails menu font validation instead of silently rendering blank.
    public static string ComfortText => ComfortTitle + ComfortPresetTitle + string.Join("", PresetNames) +
        string.Join("", ComfortRowTitles) + TurnModeTitle + TurnSmoothLabel + TurnSnapLabel + SnapAngleTitle +
        SmoothStartLabel + SmoothStopLabel +
        RestoreDefault + ComfortBack + ComfortFooter + "%m/sdeg/s当前未启用";
    public static string AllText => MenuTitle + string.Join("", Tabs) + string.Join("", Modes) + string.Join("", ModeDetails) +
        Footer + NeutralText + ChooseHelp + ResetQuestion + ResetHelp + ExitQuestion + TourTitle + "继续探索取消确认退出" + KeyText(ApartmentMovementMode.Both) +
        ComfortText +
        "关闭菜单暂未开放当前未启用移动方式选择适合你的移动方式安全重置卡住或迷路时，返回出生安全位置返回安全点返回取消安全点未配置安全点被物体占用，请稍后重试安全点下方没有可站立的地板头显高度无效，请恢复正常追踪等待头显、右手柄追踪和输入焦点安全点配置无效○●";
    public static string KeyText(ApartmentMovementMode mode) =>
        "左摇杆   平滑行走" + (mode == ApartmentMovementMode.TeleportOnly ? "（当前未启用）" : "") +
        "\n右摇杆左右   连续转向\n右手 A   按住瞄准，松开瞬移" + (mode == ApartmentMovementMode.SmoothOnly ? "（当前未启用）" : "") +
        "\n右手 B   打开 / 关闭菜单\n右食指扳机   确认按钮 / 开关门";
    readonly ApartmentMenuButtonLatch bLatch = new ApartmentMenuButtonLatch();
    ApartmentVRPanel view, completionView, quitView;
    ApartmentPanelState quitSource = ApartmentPanelState.Menu;
    ApartmentVRPanel CurrentView => ActivePanel == ApartmentPanelState.Menu ? view :
        ActivePanel == ApartmentPanelState.TourComplete ? completionView :
        ActivePanel == ApartmentPanelState.QuitConfirmation ? quitView : null;
    bool waitingNeutral = true, wasReady;
    string status = "";

    void Awake()
    {
        MovementMode = ApartmentMovementMode.Both; ActivePanel = ApartmentPanelState.Closed; waitingNeutral = true;
        // The saved profile is pushed through the mover's clamped entry point before anything is drawn.
        LoadComfort();
    }
    void OnDisable() { Suspend(); view?.Dispose(); completionView?.Dispose(); quitView?.Dispose(); view = completionView = quitView = null; }
    void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }
    void OnApplicationPause(bool paused) { if (paused) Suspend(); }
    void Suspend()
    {
        InputReady = wasReady = false; waitingNeutral = true; bLatch.Reset();
        ConfirmingReset = false; status = ""; ComfortSection = 0; UIRevision++;
        if (ActivePanel == ApartmentPanelState.QuitConfirmation) ActivePanel = quitSource;
        HidePanels(); locomotion?.CancelTeleport(); interactor?.ResetUIInput();
    }
    public static bool ControlsNeutral(ApartmentVRLocomotion mover)
    {
        return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) <= .25f &&
            !OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch) &&
            !OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) &&
            OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch).magnitude <= mover.moveDeadZone &&
            OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).magnitude <= mover.turnDeadZone;
    }
    void LateUpdate()
    {
        bool ready = interactor != null && interactor.RuntimeReady;
        bool neutral = false, b = false;
        if (ready) try { neutral = ControlsNeutral(locomotion); b = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch); }
        catch (Exception) { ready = false; }
        if (!ready) { if (wasReady || InputReady) Suspend(); else bLatch.Reset(); return; }
        InputReady = true;
        if (!wasReady) { waitingNeutral = true; bLatch.Reset(); interactor.ResetUIInput(); if (HasOpenPanel) Rebuild(true); }
        wasReady = true;
        if (neutral) waitingNeutral = false;
        // The welcome question is a distinct screen. B becomes available after an explicit choice.
        bool welcome = tutorial != null && tutorial.isActiveAndEnabled && tutorial.Session.Welcome;
        if (bLatch.Sample(b, !welcome)) Toggle();
        if (tour != null && tour.Complete && !tour.NotificationShown && !HasOpenPanel && !waitingNeutral && neutral &&
            (tutorial == null || (tutorial.Session.Step == 3 && !tutorial.Session.Explaining)) && !locomotion.IsTeleportAiming)
            ShowTourCompletion();
        CurrentView?.SetVisible(Visible);
    }
    public void Toggle()
    {
        if (!InputReady || (tutorial != null && tutorial.isActiveAndEnabled && tutorial.Session.Welcome)) return;
        if (ActivePanel == ApartmentPanelState.QuitConfirmation) { CancelQuit(); return; }
        if (ShowingTourCompletion) return; // Completion has exactly two explicit choices; B cannot dismiss it.
        ChangePanel(IsOpen ? ApartmentPanelState.Closed : ApartmentPanelState.Menu);
    }
    void HidePanels() { view?.SetVisible(false); completionView?.SetVisible(false); quitView?.SetVisible(false); }
    void ChangePanel(ApartmentPanelState next)
    {
        bool wasOpen = HasOpenPanel;
        locomotion?.CancelTeleport(); interactor?.ResetUIInput(); bLatch.Reset(); waitingNeutral = true; UIRevision++;
        ActivePanel = next; ConfirmingReset = false; status = ""; ComfortSection = 0; HidePanels();
        if (!wasOpen && HasOpenPanel) tutorial?.MenuOpened();
        if (wasOpen && !HasOpenPanel) tutorial?.MenuClosed();
        if (HasOpenPanel) Rebuild(true);
    }
    void ContinueExploring() { if (Visible && ShowingTourCompletion) ChangePanel(ApartmentPanelState.Closed); }
    void RequestQuit()
    {
        if (!Visible || (ActivePanel != ApartmentPanelState.Menu && !ShowingTourCompletion)) return;
        quitSource = ActivePanel;
        ChangePanel(ApartmentPanelState.QuitConfirmation);
    }
    void CancelQuit()
    {
        if (Visible && ActivePanel == ApartmentPanelState.QuitConfirmation) ChangePanel(quitSource);
    }
    public void SelectMovementMode(ApartmentMovementMode mode)
    {
        if (!Enum.IsDefined(typeof(ApartmentMovementMode), mode)) return;
        MovementMode = mode; locomotion?.CancelTeleport(); waitingNeutral = true;
        tutorial?.MovementChosen(mode); interactor?.ResetUIInput(); UIRevision++;
        Rebuild(false);
    }

    // ---- Comfort settings ------------------------------------------------------------------------
    public void OpenComfort() { SetComfortSection(1); }
    public void CloseComfort() { SetComfortSection(0); }
    void SetComfortSection(int section)
    {
        section = section == 1 ? 1 : 0;
        if (section == ComfortSection) return;
        ComfortSection = section; status = ""; UIRevision++;
        interactor?.ResetUIInput(); Rebuild(false);
    }
    void LoadComfort()
    {
        Comfort = ApartmentComfortStore.Load();
        if (locomotion != null && !locomotion.ApplyComfort(Comfort))
        {
            // The record was rejected by the movement configuration check, so it is dropped instead of
            // being carried into the session (or re-saved).
            Comfort = ApartmentComfortProfile.Default;
            locomotion.ApplyComfort(Comfort);
            ApartmentComfortStore.Clear();
        }
        if (comfortVignette != null) comfortVignette.strengthPercent = Comfort.vignettePercent;
    }
    void ApplyComfort(bool save)
    {
        if (Comfort == null) Comfort = ApartmentComfortProfile.Default;
        Comfort.Clamp();
        locomotion?.ApplyComfort(Comfort);
        if (comfortVignette != null) comfortVignette.strengthPercent = Comfort.vignettePercent;
        if (save) ApartmentComfortStore.Save(Comfort);
    }
    // One notch = one step. Out-of-range steps are absorbed by Clamp() inside ApplyComfort, so the
    // value can never leave the window the mover validates.
    public void StepComfort(int row, int direction)
    {
        if (Comfort == null || direction == 0) return;
        switch (row)
        {
            case ComfortRowVignette: Comfort.vignettePercent += direction * ApartmentComfortProfile.VignetteStep; break;
            case ComfortRowSpeed: Comfort.moveSpeed += direction * ApartmentComfortProfile.SpeedStep; break;
            case ComfortRowMoveStart: Comfort.moveSmoothTime += direction * ApartmentComfortProfile.SpeedSmoothStep; break;
            case ComfortRowMoveStop: Comfort.moveStopSmoothTime += direction * ApartmentComfortProfile.MoveStopStep; break;
            case ComfortRowTurnSpeed: Comfort.turnSpeed += direction * ApartmentComfortProfile.TurnSpeedStep; break;
            case ComfortRowTurnStart: Comfort.turnSmoothTime += direction * ApartmentComfortProfile.TurnSmoothStep; break;
            case ComfortRowTurnStop: Comfort.turnStopSmoothTime += direction * ApartmentComfortProfile.TurnStopStep; break;
            case ComfortRowSnapAngle: Comfort.snapAngle += direction * ApartmentComfortProfile.SnapAngleStep; break;
            default: return;
        }
        ChangeComfort();
    }
    public void SelectComfortPreset(int index) { Comfort = ApartmentComfortProfile.Preset(index); ChangeComfort(); }
    public void SelectSnapTurn(bool snap) { if (Comfort == null) return; Comfort.snapTurn = snap; ChangeComfort(); }
    public void RestoreDefaultComfort() { Comfort = ApartmentComfortProfile.Default; ChangeComfort(); }
    void ChangeComfort()
    {
        ApplyComfort(true);
        UIRevision++;
        // The page is rebuilt in place: the pointer and stick latches are re-armed so a held input
        // cannot step or click twice.
        interactor?.ResetUIInput();
        Rebuild(false);
    }
    void SetPage(int page)
    {
        if (page < 0 || page >= Tabs.Length) return;
        if (page == 5) { RequestQuit(); return; }
        Page = page; ConfirmingReset = false; status = ""; ComfortSection = 0; UIRevision++;
        interactor?.ResetUIInput(); Rebuild(false);
    }
    void RequestReset() { ConfirmingReset = true; status = ""; UIRevision++; interactor?.ResetUIInput(); Rebuild(false); }
    void ConfirmQuit()
    {
        if (!Visible || ActivePanel != ApartmentPanelState.QuitConfirmation) return;
        interactor?.ResetUIInput(); locomotion?.CancelTeleport();
        ChangePanel(ApartmentPanelState.Closed);
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying) UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    public void ShowTourCompletion()
    {
        if (!InputReady || HasOpenPanel || tour == null || !tour.Complete || tour.NotificationShown) return;
        tour.NotificationShown = true;
        ChangePanel(ApartmentPanelState.TourComplete);
    }
    void ConfirmReset()
    {
        if (!Visible || !ConfirmingReset) return;
        if (safeResetPoint == null) status = "安全点未配置";
        else if (locomotion.TrySafetyReset(safeResetPoint.position, out string reason))
        { Toggle(); return; }
        else status = reason;
        ConfirmingReset = false; UIRevision++; interactor.ResetUIInput(); Rebuild(false);
    }
    public void ProcessPointer(Ray ray, float maxDistance, bool pressed, out Vector3 end, int stickStep = 0)
    {
        end = ray.GetPoint(maxDistance);
        if (Visible) CurrentView?.ProcessPointer(ray, maxDistance, pressed, out end, stickStep);
    }
    // Also used by the explicit editor-only preview in a scratch scene; not an input bypass.
    public void BuildPreview(int page)
    {
        Page = page == 5 ? 2 : page; quitSource = ApartmentPanelState.Menu; ComfortSection = 0;
        ActivePanel = page == 5 ? ApartmentPanelState.QuitConfirmation : ApartmentPanelState.Menu;
        Rebuild(false); CurrentView.SetVisible(true);
    }
    public void BuildTourPreview() { ActivePanel = ApartmentPanelState.TourComplete; Rebuild(false); CurrentView.SetVisible(true); }
    public void BuildComfortPreview() { Page = 4; ComfortSection = 1; ActivePanel = ApartmentPanelState.Menu; Rebuild(false); CurrentView.SetVisible(true); }
    public RectTransform PanelRect => CurrentView?.Root;
    void Rebuild(bool place)
    {
        if (font == null || panelShader == null || textOverlayShader == null) return;
        HidePanels();
        if (ShowingTourCompletion || ActivePanel == ApartmentPanelState.QuitConfirmation)
        {
            bool complete = ShowingTourCompletion;
            if (complete && completionView == null)
                completionView = new ApartmentVRPanel(transform,"Tour completion (session only)",font,panelShader,textOverlayShader,900,440);
            if (!complete && quitView == null)
                quitView = new ApartmentVRPanel(transform,"Quit confirmation (session only)",font,panelShader,textOverlayShader,900,340);
            var modal = CurrentView; modal.Clear();
            var title = modal.Label(complete ? "Tour title" : "Exit title", complete ? TourTitle : ExitQuestion,
                50,30,800,complete ? 240 : 145,complete ? 42 : 48,true);
            title.alignment = TextAlignmentOptions.Center;
            float y = complete ? 315 : 215;
            if (complete)
            {
                modal.Button("Continue exploring","继续探索",50,y,375,80,ContinueExploring,true);
                modal.Button("Tour quit","退出游戏",475,y,375,80,RequestQuit);
            }
            else
            {
                modal.Button("Confirm quit","确认退出",50,y,375,80,ConfirmQuit);
                modal.Button("Cancel quit","取消",475,y,375,80,CancelQuit,true);
            }
            if (place && locomotion != null) modal.Place(locomotion.rig.centerEyeAnchor,locomotion.transform.forward);
            modal.SetVisible(Visible); return;
        }
        if (!IsOpen) return;
        if (view == null) view = new ApartmentVRPanel(transform, "Apartment menu (session only)", font, panelShader, textOverlayShader, 1100, 740);
        view.Clear(); view.Label("Menu title", MenuTitle, 35, 10, 600, 84, 48, true);
        view.Button("Close", "B  关闭菜单", 840, 26, 225, 62, () => Toggle(), false, 29);
        view.Box("Divider", 28, 107, 1044, 2, new Color(.2f,.3f,.36f,1));
        for (int i = 0; i < Tabs.Length; i++)
        {
            int index = i; bool placeholder = i == 0 || i == 1 || i == 3;
            view.Button("Tab " + i, Tabs[i] + (placeholder ? "\n<size=70%>暂未开放</size>" : ""), 28, 132+i*91, 248, 79,
                () => SetPage(index), Page == i, 33);
        }
        view.Label("Page title", ComfortSection == 1 ? Tabs[Page] + " / " + ComfortTitle : Tabs[Page], 320, 117, 710, 68, 39, true);
        if (ConfirmingReset)
        {
            view.Label("Reset title", ResetQuestion, 330, 230, 700, 60, 39, true);
            view.Label("Reset explanation", ResetHelp, 330, 310, 700, 130, 30);
            view.Button("Confirm reset", "返回", 340, 490, 300, 80, ConfirmReset, true);
            view.Button("Cancel reset", "取消", 680, 490, 300, 80, () => { ConfirmingReset=false; interactor.ResetUIInput(); Rebuild(false); });
        }
        else if (Page == 4 && ComfortSection == 1) RebuildComfort(view);
        else if (Page == 4)
        {
            view.Label("Mode title", "移动方式", 330, 190, 650, 46, 30, true);
            for (int i = 0; i < 3; i++)
            {
                var mode=(ApartmentMovementMode)i; bool selected=MovementMode==mode;
                view.Button("Mode " + i, (selected ? "●  " : "○  ")+Modes[i]+"\n<size=75%>"+ModeDetails[i]+"</size>",
                    330, 240+i*86, 730, 80, () => SelectMovementMode(mode), selected, 32);
            }
            view.Button("Comfort entry", ComfortTitle, 330, 500, 730, 56, OpenComfort, false, 30);
            view.Label("Safety text", string.IsNullOrEmpty(status) ? "安全重置：卡住或迷路时，返回出生安全位置" : status, 330, 562, 730, 60, 25);
            view.Button("Safety reset", "返回安全点", 330, 626, 730, 56, RequestReset, false, 30);
        }
        else if (Page == 2)
            view.Label("Controls", KeyText(MovementMode), 330, 208, 730, 455, 30);
        else view.Label("Placeholder", "暂未开放", 330, 300, 700, 120, 43);
        string help = tutorial != null && tutorial.Session.Step == -1 ? ChooseHelp :
            (Page == 4 && ComfortSection == 1 ? ComfortFooter : Footer);
        view.Label("Footer", help, 24, 690, 1052, 44, 23);
        if (place && locomotion != null) view.Place(locomotion.rig.centerEyeAnchor, locomotion.transform.forward);
        view.SetVisible(Visible);
    }

    // The comfort sub page. It is deliberately not a seventh tab: Page stays 4, so the tab column, the
    // footer contract and the existing "Tab 4 is the settings page" assertions keep their meaning.
    static float ComfortRowY(int index) => ComfortTop + index * ComfortStep;
    // Invariant formatting: a decimal comma would otherwise appear on a non-English system culture.
    static string Num(float value, string format) => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    public static bool MatchesPreset(ApartmentComfortProfile profile, int index)
    {
        var preset = ApartmentComfortProfile.Preset(index);
        return Mathf.Abs(profile.vignettePercent - preset.vignettePercent) < .001f &&
            Mathf.Abs(profile.moveSpeed - preset.moveSpeed) < .001f &&
            Mathf.Abs(profile.moveSmoothTime - preset.moveSmoothTime) < .001f &&
            Mathf.Abs(profile.moveStopSmoothTime - preset.moveStopSmoothTime) < .001f &&
            Mathf.Abs(profile.turnSpeed - preset.turnSpeed) < .001f &&
            Mathf.Abs(profile.turnSmoothTime - preset.turnSmoothTime) < .001f &&
            Mathf.Abs(profile.turnStopSmoothTime - preset.turnStopSmoothTime) < .001f;
    }
    void RebuildComfort(ApartmentVRPanel view)
    {
        // No title of its own: the page title line above already reads "设置 / 舒适度调节".
        // Row 0: presets. Applying one overwrites every value below; all of them stay editable after.
        view.Label("Preset title", ComfortPresetTitle, ComfortX, ComfortRowY(0), 200, ComfortRowHeight, 28);
        for (int i = 0; i < PresetNames.Length; i++)
        {
            int index = i;
            view.Button("Preset " + i, PresetNames[i], 545 + i * 170, ComfortRowY(0), 160, ComfortRowHeight,
                () => SelectComfortPreset(index), MatchesPreset(Comfort, index), 28);
        }
        bool smoothTurn = !Comfort.snapTurn;
        // Rows 1 / 2 / 4: one full-width ray target each, stepped with the right stick while hovered.
        view.Stepper("Comfort vignette", ComfortRowTitles[0], Num(Comfort.vignettePercent,"0") + " %",
            ComfortX, ComfortRowY(1), ComfortW, ComfortRowHeight, 290, d => StepComfort(ComfortRowVignette, d), true);
        view.Stepper("Comfort speed", ComfortRowTitles[1], Num(Comfort.moveSpeed,"0.0") + " m/s",
            ComfortX, ComfortRowY(2), ComfortW, ComfortRowHeight, 290, d => StepComfort(ComfortRowSpeed, d), true);
        // Row 3: start-of-motion ease and brake-to-stop are independently adjustable, side by side.
        PairedRow(view, "移动平滑", "Comfort move start", ComfortRowMoveStart, Num(Comfort.moveSmoothTime,"0.00") + " s",
            "Comfort move stop", ComfortRowMoveStop, Num(Comfort.moveStopSmoothTime,"0.00") + " s", ComfortRowY(3));
        view.Stepper("Comfort turn speed", ComfortRowTitles[3], Num(Comfort.turnSpeed,"0") + " deg/s",
            ComfortX, ComfortRowY(4), ComfortW, ComfortRowHeight, 290, d => StepComfort(ComfortRowTurnSpeed, d), smoothTurn);
        // Row 5: the same split for the view. Snap turn has no continuous rate to ease or brake, so both
        // cells render flat while it is selected.
        PairedRow(view, "转向平滑", "Comfort turn start", ComfortRowTurnStart, Num(Comfort.turnSmoothTime,"0.00") + " s",
            "Comfort turn stop", ComfortRowTurnStop, Num(Comfort.turnStopSmoothTime,"0.00") + " s", ComfortRowY(5), smoothTurn);
        // Row 6: turn mode and the snap angle on one line. The angle is only live while snap turn is on.
        view.Label("Turn mode title", TurnModeTitle, ComfortX, ComfortRowY(6), 140, ComfortRowHeight, 28);
        view.Button("Turn smooth", TurnSmoothLabel, 478, ComfortRowY(6), 110, ComfortRowHeight, () => SelectSnapTurn(false), smoothTurn, 26);
        view.Button("Turn snap", TurnSnapLabel, 596, ComfortRowY(6), 110, ComfortRowHeight, () => SelectSnapTurn(true), !smoothTurn, 26);
        view.Stepper("Comfort snap angle", null, SnapAngleTitle + " " + Num(Comfort.snapAngle,"0"),
            706, ComfortRowY(6), 344, ComfortRowHeight, 0, d => StepComfort(ComfortRowSnapAngle, d), !smoothTurn);
        // Row 7: actions.
        view.Button("Comfort default", RestoreDefault, ComfortX, ComfortRowY(7), 200, ComfortRowHeight, RestoreDefaultComfort, false, 28);
        view.Button("Comfort back", ComfortBack, 860, ComfortRowY(7), 190, ComfortRowHeight, CloseComfort, false, 28);
    }

    // One row title plus two independent stepper cells: start-of-motion ease on the left, brake-to-stop on
    // the right. They share a grid row so the sub page keeps its eight-row layout.
    void PairedRow(ApartmentVRPanel view, string title, string startName, int startRow, string startValue,
        string stopName, int stopRow, string stopValue, float y, bool active = true)
    {
        view.Label(title + " title", title, ApartmentVRPanel.PairedTitleX, y, ApartmentVRPanel.PairedTitleWidth, ComfortRowHeight, 28)
            .color = active ? Color.white : ApartmentVRPanel.Dim;
        view.StepperCell(startName, SmoothStartLabel, startValue, ApartmentVRPanel.CellOneX, y, ApartmentVRPanel.CellWidth, ComfortRowHeight,
            d => StepComfort(startRow, d), active);
        view.StepperCell(stopName, SmoothStopLabel, stopValue, ApartmentVRPanel.CellTwoX, y, ApartmentVRPanel.CellWidth, ComfortRowHeight,
            d => StepComfort(stopRow, d), active);
    }
}

// Small world-space UI renderer. Hit testing shares the existing right-hand trigger, no EventSystem input owner.
public sealed class ApartmentVRPanel : IDisposable
{
    public RectTransform Root { get; private set; }
    readonly TMP_FontAsset font;
    readonly Material background, textMaterial;
    readonly Sprite rounded;
    readonly Texture2D roundTexture;
    readonly List<Control> controls = new List<Control>();
    readonly List<GameObject> content = new List<GameObject>();
    sealed class Control { public RectTransform rect; public Image image; public Color color; public Action action; public Action<int> step; }
    // Fixed comfort-row geometry so every row lines up and the ray target stays predictable.
    public const float StepperButton = 90f, StepperGap = 10f;
    // Paired-row cells: two of these share one 720 wide row (title 140 + 2 * 276 + gaps).
    public const float CellLabelWidth = 60f, CellButton = 62f, CellGap = 4f;
    public const float PairedTitleX = 348f, PairedTitleWidth = 140f;
    public const float CellOneX = 492f, CellTwoX = 776f, CellWidth = 274f;
    public static readonly Color RowColor = new Color(.055f,.11f,.15f,1), RowIdleColor = new Color(.05f,.06f,.08f,1), Dim = new Color(.42f,.46f,.5f,1);
    public ApartmentVRPanel(Transform parent, string name, TMP_FontAsset font, Shader panelShader, Shader textShader, float width, float height)
    {
        this.font=font; background=new Material(panelShader); textMaterial=new Material(font.material); textMaterial.shader=textShader;
        roundTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);roundTexture.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {
            float dx=Mathf.Max(7-x, x-24),dy=Mathf.Max(7-y,y-24);
            float d=new Vector2(Mathf.Max(0,dx),Mathf.Max(0,dy)).magnitude;
            roundTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(7.5f-d)));
        }
        roundTexture.Apply();rounded=Sprite.Create(roundTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(9,9,9,9));
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas));go.layer=5;
        Root=go.GetComponent<RectTransform>();Root.SetParent(parent,false);Root.localScale=Vector3.one*.001f;Root.sizeDelta=new Vector2(width,height);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=110;
        var image=go.AddComponent<Image>();image.material=background;image.sprite=rounded;image.type=Image.Type.Sliced;image.color=new Color(.025f,.045f,.067f,.98f);image.raycastTarget=false;
    }
    RectTransform Rect(string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=Root.gameObject.layer;content.Add(go);
        var r=go.GetComponent<RectTransform>();r.SetParent(Root,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    public void Box(string name,float x,float y,float w,float h,Color color)
    { var image=Rect(name,x,y,w,h).gameObject.AddComponent<Image>();image.material=background;image.color=color;image.raycastTarget=false; }
    public TextMeshProUGUI Label(string name,string text,float x,float y,float w,float h,float size,bool bold=false)
    {
        var label=Rect(name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.fontSharedMaterial=textMaterial;
        label.text=text;label.color=Color.white;label.fontSize=size;label.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        label.enableAutoSizing=true;label.fontSizeMin=size*.82f;label.fontSizeMax=size;
        label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;return label;
    }
    public void Button(string name,string text,float x,float y,float w,float h,Action action,bool selected=false,float size=33)
    {
        var rect=Rect(name,x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.material=background;image.sprite=rounded;image.type=Image.Type.Sliced;image.raycastTarget=false;
        Color color=selected?new Color(.06f,.36f,.39f,1):new Color(.10f,.15f,.21f,1);image.color=color;
        if(selected)Box(name+" accent",x,y+12,4,h-24,new Color(.18f,.94f,.88f,1));
        Label(name+" label",text,x+19,y+4,w-38,h-8,size);
        controls.Add(new Control{rect=rect,image=image,color=color,action=action});
    }
    // A comfort row: one ray target spanning the whole row, plus -/+ buttons that step the same value.
    // The row is stepped with the right stick while it is hovered; an inactive row renders flat and
    // registers nothing, so it cannot be aimed at or stepped. titleWidth <= 0 renders no title, which
    // is what the combined turn-mode row uses.
    public void Stepper(string name,string title,string value,float x,float y,float w,float h,float titleWidth,Action<int> step,bool active)
    {
        float minusX=x+w-StepperButton*2-StepperGap;
        float valueX=x+Mathf.Max(0f,titleWidth);
        var rect=Rect(name,x,y,w,h);
        var image=rect.gameObject.AddComponent<Image>();image.material=background;image.sprite=rounded;image.type=Image.Type.Sliced;image.raycastTarget=false;
        image.color=active?RowColor:RowIdleColor;
        if(!string.IsNullOrEmpty(title))Label(name+" title",title,x+18,y+4,Mathf.Max(40f,titleWidth-24f),h-8,30).color=active?Color.white:Dim;
        var valueLabel=Label(name+" value",value,valueX,y+4,Mathf.Max(40f,minusX-StepperGap-valueX),h-8,30,true);
        valueLabel.alignment=TextAlignmentOptions.MidlineRight;
        if(!active)valueLabel.color=Dim;
        if(!active){Flat(name+" minus","-",minusX,y,StepperButton,h);Flat(name+" plus","+",minusX+StepperButton+StepperGap,y,StepperButton,h);return;}
        controls.Add(new Control{rect=rect,image=image,color=RowColor,step=step});
        Button(name+" minus","-",minusX,y,StepperButton,h,()=>step?.Invoke(-1),false,34);
        Button(name+" plus","+",minusX+StepperButton+StepperGap,y,StepperButton,h,()=>step?.Invoke(1),false,34);
    }
    // A compact stepper cell: its own ray target plus a short label, a value and -/+ buttons. Two of these
    // share one grid row so that start-of-motion ease and brake-to-stop stay independently adjustable
    // without growing the sub page.
    public void StepperCell(string name,string label,string value,float x,float y,float w,float h,Action<int> step,bool active)
    {
        float minusX=x+w-CellButton*2-CellGap;
        float valueX=x+CellLabelWidth;
        var rect=Rect(name,x,y,w,h);
        var image=rect.gameObject.AddComponent<Image>();image.material=background;image.sprite=rounded;image.type=Image.Type.Sliced;image.raycastTarget=false;
        image.color=active?RowColor:RowIdleColor;
        Label(name+" label",label,x+8,y+4,CellLabelWidth-12,h-8,25).color=active?Color.white:Dim;
        var valueLabel=Label(name+" value",value,valueX,y+4,Mathf.Max(38f,minusX-6f-valueX),h-8,29,true);
        valueLabel.alignment=TextAlignmentOptions.MidlineRight;
        if(!active)valueLabel.color=Dim;
        if(!active){Flat(name+" minus","-",minusX,y,CellButton,h);Flat(name+" plus","+",minusX+CellButton+CellGap,y,CellButton,h);return;}
        controls.Add(new Control{rect=rect,image=image,color=RowColor,step=step});
        Button(name+" minus","-",minusX,y,CellButton,h,()=>step?.Invoke(-1),false,26);
        Button(name+" plus","+",minusX+CellButton+CellGap,y,CellButton,h,()=>step?.Invoke(1),false,26);
    }
    void Flat(string name,string text,float x,float y,float w,float h)
    {
        Box(name,x,y,w,h,RowIdleColor);
        Label(name+" label",text,x+19,y+4,w-38,h-8,34).color=Dim;
    }
    public void ProcessPointer(Ray ray,float distance,bool pressed,out Vector3 end,int stickStep=0)
    {
        end=ray.GetPoint(distance);Control hit=null,stepHit=null;
        foreach(var c in controls)
        {
            bool hover=ApartmentTutorialController.HitRectangle(c.rect,ray,distance,out var point);
            c.image.color=hover?new Color(.12f,.48f,.50f,1):c.color;
            if(hover){hit=c;end=point;if(c.step!=null)stepHit=c;}
        }
        // Invoke only after iteration: callbacks may replace the whole page. A pressed trigger wins over
        // a stick flick so one frame can never do both.
        if(pressed&&hit!=null)hit.action?.Invoke();
        else if(stickStep!=0)stepHit?.step?.Invoke(stickStep);
    }
    public void Place(Transform head,Vector3 fallback)
    {
        Vector3 forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
        if(forward.sqrMagnitude<.5f)forward=fallback;
        Root.SetPositionAndRotation(head.position+forward*1.2f-Vector3.up*.12f,Quaternion.LookRotation(forward));
        Root.GetComponent<Canvas>().worldCamera=head.GetComponent<Camera>();
    }
    public void SetVisible(bool visible) { if(Root!=null)Root.gameObject.SetActive(visible); }
    public void Clear() { controls.Clear();foreach(var go in content){go.SetActive(false);Destroy(go);}content.Clear(); }
    static void Destroy(UnityEngine.Object obj) { if(Application.isPlaying)UnityEngine.Object.Destroy(obj);else UnityEngine.Object.DestroyImmediate(obj); }
    public void Dispose() { if(Root!=null)Destroy(Root.gameObject);Destroy(background);Destroy(textMaterial);Destroy(rounded);Destroy(roundTexture);Root=null; }
}
