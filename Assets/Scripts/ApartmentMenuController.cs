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

[DisallowMultipleComponent, DefaultExecutionOrder(8500)]
public sealed class ApartmentMenuController : MonoBehaviour
{
    public ApartmentVRLocomotion locomotion;
    public ApartmentFurnitureInteractor interactor;
    public ApartmentTutorialController tutorial;
    public ApartmentShoppingBagBridge shoppingBag;
    public Transform safeResetPoint;
    public ApartmentTourTracker tour;
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
    public static string AllText => MenuTitle + string.Join("", Tabs) + string.Join("", Modes) + string.Join("", ModeDetails) +
        Footer + NeutralText + ChooseHelp + ResetQuestion + ResetHelp + ExitQuestion + TourTitle + "继续探索取消确认退出" + ApartmentShoppingBagBridge.PageText + KeyText(ApartmentMovementMode.Both) +
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

    void Awake() { MovementMode = ApartmentMovementMode.Both; ActivePanel = ApartmentPanelState.Closed; waitingNeutral = true; }
    void OnDisable() { Suspend(); view?.Dispose(); completionView?.Dispose(); quitView?.Dispose(); view = completionView = quitView = null; }
    void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }
    void OnApplicationPause(bool paused) { if (paused) Suspend(); }
    void Suspend()
    {
        InputReady = wasReady = false; waitingNeutral = true; bLatch.Reset();
        ConfirmingReset = false; status = ""; UIRevision++;
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
        ActivePanel = next; ConfirmingReset = false; status = ""; HidePanels();
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
    void SetPage(int page)
    {
        if (page < 0 || page >= Tabs.Length) return;
        if (page == 5) { RequestQuit(); return; }
        Page = page; ConfirmingReset = false; status = ""; UIRevision++;
        interactor?.ResetUIInput(); Rebuild(false);
    }
    public void RefreshMyItems()
    {
        if (!isActiveAndEnabled || !IsOpen || Page != 3) return;
        UIRevision++; interactor?.ResetUIInput(); Rebuild(false);
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
    public void ProcessPointer(Ray ray, float maxDistance, bool pressed, out Vector3 end)
    {
        end = ray.GetPoint(maxDistance);
        if (Visible) CurrentView?.ProcessPointer(ray, maxDistance, pressed, out end);
    }
    // Also used by the explicit editor-only preview in a scratch scene; not an input bypass.
    public void BuildPreview(int page)
    {
        Page = page == 5 ? 2 : page; quitSource = ApartmentPanelState.Menu;
        ActivePanel = page == 5 ? ApartmentPanelState.QuitConfirmation : ApartmentPanelState.Menu;
        Rebuild(false); CurrentView.SetVisible(true);
    }
    public void BuildTourPreview() { ActivePanel = ApartmentPanelState.TourComplete; Rebuild(false); CurrentView.SetVisible(true); }
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
            int index = i; bool placeholder = i == 0 || i == 1 || (i == 3 && (shoppingBag == null || !shoppingBag.IsConfigured));
            view.Button("Tab " + i, Tabs[i] + (placeholder ? "\n<size=70%>暂未开放</size>" : ""), 28, 132+i*91, 248, 79,
                () => SetPage(index), Page == i, 33);
        }
        view.Label("Page title", Tabs[Page], 320, 117, 710, 68, 39, true);
        if (ConfirmingReset)
        {
            view.Label("Reset title", ResetQuestion, 330, 230, 700, 60, 39, true);
            view.Label("Reset explanation", ResetHelp, 330, 310, 700, 130, 30);
            view.Button("Confirm reset", "返回", 340, 490, 300, 80, ConfirmReset, true);
            view.Button("Cancel reset", "取消", 680, 490, 300, 80, () => { ConfirmingReset=false; interactor.ResetUIInput(); Rebuild(false); });
        }
        else if (Page == 4)
        {
            view.Label("Mode title", "移动方式", 330, 184, 650, 54, 31, true);
            for (int i = 0; i < 3; i++)
            {
                var mode=(ApartmentMovementMode)i; bool selected=MovementMode==mode;
                view.Button("Mode " + i, (selected ? "●  " : "○  ")+Modes[i]+"\n<size=75%>"+ModeDetails[i]+"</size>",
                    330, 243+i*94, 730, 80, () => SelectMovementMode(mode), selected, 32);
            }
            view.Label("Safety title", "安全重置", 330, 522, 700, 54, 31, true);
            view.Label("Safety description", string.IsNullOrEmpty(status) ? "卡住或迷路时，返回出生安全位置" : status, 330, 576, 730, 42, 25);
            view.Button("Safety reset", "返回安全点", 330, 624, 730, 62, RequestReset, false, 30);
        }
        else if (Page == 3)
        {
            if (shoppingBag != null) shoppingBag.BuildMyItemsPage(view);
            else view.Label("Bag unavailable", "Shopping Bag: Not configured", 330, 220, 730, 100, 30);
        }
        else if (Page == 2)
            view.Label("Controls", KeyText(MovementMode), 330, 208, 730, 455, 30);
        else view.Label("Placeholder", "暂未开放", 330, 300, 700, 120, 43);
        string help = tutorial != null && tutorial.Session.Step == -1 ? ChooseHelp : Footer;
        view.Label("Footer", help, 24, 690, 1052, 44, 23);
        if (place && locomotion != null) view.Place(locomotion.rig.centerEyeAnchor, locomotion.transform.forward);
        view.SetVisible(Visible);
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
    sealed class Control { public RectTransform rect; public Image image; public Color color; public Action action; }
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
    public void ProcessPointer(Ray ray,float distance,bool pressed,out Vector3 end)
    {
        end=ray.GetPoint(distance);Control hit=null;
        foreach(var c in controls)
        {
            bool hover=ApartmentTutorialController.HitRectangle(c.rect,ray,distance,out var point);
            c.image.color=hover?new Color(.12f,.48f,.50f,1):c.color;
            if(hover){hit=c;end=point;}
        }
        // Invoke only after iteration: callbacks may replace the whole page.
        if(pressed&&hit!=null)hit.action?.Invoke();
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
