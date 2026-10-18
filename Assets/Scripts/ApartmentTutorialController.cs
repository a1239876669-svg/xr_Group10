using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pure session state: no PlayerPrefs, hardware access or scene mutations.
public sealed class ApartmentTutorialProgress
{
    public ApartmentTutorialProgress(bool welcome = false) { Step = welcome ? -2 : 0; }
    public bool Welcome => Step == -2;
    public bool Skipped { get; private set; }
    public bool PracticeSkipped { get; private set; }
    public bool MenuVisited { get; private set; }
    public bool MenuChoiceMade { get; private set; }
    public ApartmentMovementMode Mode { get; private set; } = ApartmentMovementMode.Both;
    readonly bool[] completed = new bool[3];
    public int Step { get; private set; }
    public bool Explaining { get; private set; } = true;
    public bool WaitingForNeutral { get; private set; }
    public int PendingKind { get; private set; } = -1;
    public float Progress { get; private set; }
    readonly bool[] seen = new bool[3];
    public bool AllowMove => !Explaining && !WaitingForNeutral && (Step >= 0 && Step <= 3);
    public bool AllowTurn => !Explaining && !WaitingForNeutral && (Step >= 0 && Step <= 3);
    public bool AllowTeleport => !Explaining && !WaitingForNeutral && (Step >= 0 && Step <= 3);
    public bool AllowInteraction => !Explaining && !WaitingForNeutral && Step == 3;
    public bool HasSeen(ApartmentInteractionKind kind) => Skipped || seen[(int)kind];
    public void ChooseTutorial(bool start)
    {
        if (!Welcome) return;
        Skipped = !start; Step = start ? -1 : 3; Explaining = start; WaitingForNeutral = true;
    }
    public void MenuOpened() { Suspend(); if (Step == -1 && !Explaining) MenuVisited = true; }
    public void ChooseMode(ApartmentMovementMode mode)
    {
        if (!Enum.IsDefined(typeof(ApartmentMovementMode), mode)) return;
        Mode = mode;
        if (Step == -1 && MenuVisited) MenuChoiceMade = true;
        else if (Step >= 0 && Step < 3) SelectNextPractice();
    }
    public void MenuClosed()
    {
        Suspend();
        if (Step == -1 && MenuVisited) SelectNextPractice();
    }
    public void SkipPractice()
    {
        if (Welcome) return;
        PracticeSkipped = true; Step = 3; PendingKind = -1; Explaining = false; WaitingForNeutral = true;
    }
    public void Confirm()
    {
        if (!Explaining || Welcome) return;
        if (PendingKind >= 0) { seen[PendingKind] = true; PendingKind = -1; }
        Explaining = false; WaitingForNeutral = true;
    }
    public void ObserveNeutral(bool neutral) { if (neutral) WaitingForNeutral = false; }
    public void Suspend() { WaitingForNeutral = true; }
    public bool RequestKind(ApartmentInteractionKind kind)
    {
        if (Skipped || !AllowInteraction || HasSeen(kind)) return false;
        PendingKind = (int)kind; Explaining = true; return true;
    }
    public void Walk(float distance) { if (AllowMove && Step == 0) Add(distance, .25f); }
    public void Turn(float degrees) { if (AllowTurn && Step == 1) Add(degrees, 15f); }
    public void Teleport(float distance) { if (AllowTeleport && Step == 2 && distance >= .5f) Advance(); }
    void Add(float amount, float target)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return;
        Progress += amount; if (Progress + .00001f >= target) Advance();
    }
    void Advance() { completed[Step] = true; SelectNextPractice(); }
    void SelectNextPractice()
    {
        int next = !completed[0] && Mode != ApartmentMovementMode.TeleportOnly ? 0 :
            !completed[1] ? 1 : !completed[2] && Mode != ApartmentMovementMode.SmoothOnly ? 2 : 3;
        if (next != Step) { Step = next; Progress = 0; Explaining = Step < 3; }
        WaitingForNeutral = true;
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(9000)]
public sealed class ApartmentTutorialController : MonoBehaviour
{
    public ApartmentVRLocomotion locomotion;
    public ApartmentFurnitureInteractor interactor;
    public ApartmentMenuController menu;
    public ApartmentGuidedJourney guidedJourney;
    public TMP_FontAsset font;
    public Shader panelShader;
    public Shader textOverlayShader;
    public ApartmentTutorialProgress Session { get; private set; } = new ApartmentTutorialProgress();
    public bool InputReady { get; private set; }
    bool MenuActive => menu != null && menu.isActiveAndEnabled && menu.HasOpenPanel;
    public bool ModalVisible => InputReady && !MenuActive && Session.Explaining;
    public bool AllowMove => InputReady && !MenuActive && Session.AllowMove;
    public bool AllowTurn => InputReady && !MenuActive && Session.AllowTurn;
    public bool AllowTeleport => InputReady && !MenuActive && Session.AllowTeleport;
    public bool AllowInteraction => InputReady && !MenuActive && Session.AllowInteraction;
    public bool HasSeen(ApartmentInteractionKind kind) => (guidedJourney != null && guidedJourney.OwnsContextLessons) || Session.HasSeen(kind);
    public static readonly string[] Lessons = {
        "Left stick: walk a short distance.\nTry about 25 cm. Right stick turns; A teleport remains available.\nB > Controls / 按键说明 > Review guide can skip a practice.",
        "Right stick: turn left or right by about 15 degrees.\nYou can still walk or teleport. Choose smooth/snap turning in Settings.",
        "Hold right A, point at a clear floor, then release on green.\nTry a landing at least 50 cm away. B always opens the menu." };
    public static readonly string[] KindLessons = {
        "Room door: point at the door and press right Trigger.\nPress again to close. Grip is for the shopping bag.",
        "Kitchen cabinet: stand nearby, point at its door and press right Trigger.\nOpen, then close it; step clear if the door is blocked.",
        "Balcony sliding door: point at its frame/glass and press right Trigger.\nWhen blocked or paused, step clear and press again." };
    public const string ConfirmHelp = "Point at OK and press right Trigger. B opens the menu for Help / Skip.";
    public const string NeutralHelp = "Release Trigger, A and B, and centre both sticks.";
    public const string PracticeMove = "Try walking 25 cm; other movement remains available. B > Controls > Review guide to skip.";
    public const string PracticeTurn = "Try turning about 15 degrees with the right stick. You may walk or teleport too.";
    public const string PracticeTeleport = "Hold A to aim, release on green. Try 50 cm or skip in Review guide.";
    public const string WelcomeTitle = "Welcome to the apartment review";
    public const string WelcomeHelp = "Review everyday movement and furniture use. Carry a bag, then measure and mark an issue. Structure is fixed; dimensions are simplified assumptions. You may skip, pause or exit safely.";
    public const string MenuLesson = "B opens and closes the menu. Point the right ray and press front Trigger to choose. Both smooth movement and A teleport are available by default. If stuck: Settings / 设置 > 返回安全点.";
    public const string MenuPractice = "Press B, view the controls or settings, then close B. Both movement methods are available by default.";
    public static string AllText => WelcomeTitle + WelcomeHelp + MenuLesson + MenuPractice + "Start guideSkip / explore freely" + ApartmentGuidedJourney.AllText + string.Join("",Lessons) + string.Join("",KindLessons) + ConfirmHelp + NeutralHelp +
        PracticeMove + PracticeTurn + PracticeTeleport + "OK重新练习教程推拉门配置不完整";
    GameObject panel;
    RectTransform panelRect, okRect;
    TextMeshProUGUI label;
    Image okImage;
    Material textMaterial, backgroundMaterial;
    int previousStep = -2, previousKind = -2;
    bool previousModal, wasReady;
    float nextScan;
    ApartmentVRPanel welcomeView;

    void Awake() { Session = new ApartmentTutorialProgress(menu != null); }
    void OnEnable()
    {
        if (locomotion == null) return;
        locomotion.Walked += OnWalked; locomotion.Turned += OnTurned; locomotion.Teleported += OnTeleported;
    }
    void OnDisable()
    {
        if (locomotion != null)
        { locomotion.Walked -= OnWalked; locomotion.Turned -= OnTurned; locomotion.Teleported -= OnTeleported; }
        InputReady = false;
        if (panel != null) DisposeVisual(panel);
        if (textMaterial != null) DisposeVisual(textMaterial);
        if (backgroundMaterial != null) DisposeVisual(backgroundMaterial);
        panel = null;
        welcomeView?.Dispose(); welcomeView = null;
    }
    static void DisposeVisual(UnityEngine.Object value) { if(Application.isPlaying)Destroy(value);else DestroyImmediate(value); }
    void OnWalked(float value) { if (AllowMove) Session.Walk(value); }
    void OnTurned(float value) { if (AllowTurn) Session.Turn(value); }
    void OnTeleported(Vector3 _) { if (AllowTeleport && locomotion.LastRelocationReason == ApartmentRelocationReason.AimedTeleport) Session.Teleport(locomotion.LastTeleportDistance); }
    public void MenuOpened() { Session.MenuOpened(); if(panel!=null)panel.SetActive(false); welcomeView?.SetVisible(false); }
    public void MenuClosed() { Session.MenuClosed(); previousStep = -99; }
    public void MovementChosen(ApartmentMovementMode mode) { Session.ChooseMode(mode); previousStep = -99; }
    void OnApplicationFocus(bool focus) { if (!focus) Suspend(); }
    void OnApplicationPause(bool pause) { if (pause) Suspend(); }
    void Suspend() { InputReady = wasReady = false; Session.Suspend(); if (panel != null) panel.SetActive(false); welcomeView?.SetVisible(false); }

    void LateUpdate()
    {
        bool ready = interactor != null && interactor.RuntimeReady;
        bool neutral = false;
        if (ready)
        {
            try
            {
                bool left = OVRInput.IsControllerConnected(OVRInput.Controller.LTouch) &&
                    OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch) && OVRInput.GetControllerOrientationTracked(OVRInput.Controller.LTouch);
                if (Session.Step >= 0 && Session.Step < 3 && !left) ready = false;
                neutral = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,OVRInput.Controller.RTouch) <= .25f &&
                    !OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.RTouch) &&
                    (menu == null || !OVRInput.Get(OVRInput.Button.Two,OVRInput.Controller.RTouch)) &&
                    OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.LTouch).magnitude <= locomotion.moveDeadZone &&
                    OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.RTouch).magnitude <= locomotion.turnDeadZone;
            }
            catch (Exception) { ready = false; }
        }
        if (!ready) { Suspend(); return; }
        InputReady = true;
        if (!wasReady) { Session.Suspend(); previousStep = -99; }
        wasReady = true; Session.ObserveNeutral(neutral);
        if (MenuActive) { if(panel!=null)panel.SetActive(false); welcomeView?.SetVisible(false); return; }
        if (Session.Welcome)
        {
            if(panel!=null)panel.SetActive(false);
            EnsureWelcome();
            if(previousStep != -2) welcomeView?.Place(locomotion.rig.centerEyeAnchor,locomotion.transform.forward);
            previousStep=-2; welcomeView?.SetVisible(true); return;
        }
        welcomeView?.SetVisible(false);
        if (Session.Skipped) { if(panel!=null)panel.SetActive(false); return; }
        if ((guidedJourney == null || !guidedJourney.OwnsContextLessons) && AllowInteraction && (menu == null || !menu.BlocksWorldInput) && Time.unscaledTime >= nextScan && !locomotion.IsTeleportAiming &&
            !OVRInput.Get(OVRInput.Button.One,OVRInput.Controller.RTouch))
        { nextScan = Time.unscaledTime + .15f; DiscoverNearbyType(); }
        EnsureVisuals();
        if (panel == null) { InputReady = false; return; }
        bool visible = Session.Step < 3 || Session.Explaining || Session.WaitingForNeutral;
        panel.SetActive(visible);
        if (!visible) return;
        bool modal = Session.Explaining;
        bool reposition = previousStep != Session.Step || previousKind != Session.PendingKind || previousModal != modal;
        previousStep = Session.Step; previousKind = Session.PendingKind; previousModal = modal;
        if (reposition || !modal) PlacePanel(modal);
        okRect.gameObject.SetActive(modal);
        panelRect.sizeDelta = new Vector2(900, modal ? 420 : 120);
        label.rectTransform.offsetMin = new Vector2(30,modal ? 116 : 8);
        label.rectTransform.offsetMax = new Vector2(-30,-18);
        label.fontSize = modal ? 38 : 30;
        label.enableAutoSizing = true; label.fontSizeMin = modal ? 28 : 24; label.fontSizeMax = modal ? 38 : 30;
        label.text = modal ? (Session.PendingKind >= 0 ? KindLessons[Session.PendingKind] : Session.Step == -1 ? MenuLesson : Lessons[Session.Step]) + "\n\n" + ConfirmHelp :
            Session.WaitingForNeutral ? (menu != null ? ApartmentMenuController.NeutralText : NeutralHelp) : Session.Step == -1 ? MenuPractice : Session.Step == 0 ? PracticeMove : Session.Step == 1 ? PracticeTurn : PracticeTeleport;
    }
    void DiscoverNearbyType()
    {
        IApartmentInteractable closest = null; float distance = 2.0001f;
        if (interactor.doors != null) foreach (var door in interactor.doors) Consider(door,ref closest,ref distance);
        if (interactor.slidingDoors != null) foreach (var door in interactor.slidingDoors) Consider(door,ref closest,ref distance);
        if (closest != null) Session.RequestKind(closest.HintCategory);
    }
    void Consider(IApartmentInteractable target, ref IApartmentInteractable closest, ref float distance)
    {
        if (target == null || Session.HasSeen(target.HintCategory) || target.HighlightRenderer == null) return;
        if (target is Behaviour b && !b.isActiveAndEnabled) return;
        Vector3 head = interactor.HeadPosition;
        var renderers = target is ApartmentSlidingDoor slider ? slider.GetComponentsInChildren<Renderer>() : new[]{target.HighlightRenderer};
        foreach (var renderer in renderers)
        {
            Vector3 point = renderer.bounds.ClosestPoint(head);
            Vector3 delta = point - head;
            float d = delta.magnitude;
            if (d < .01f || d > distance || Vector3.Dot(locomotion.rig.centerEyeAnchor.forward,delta.normalized) < .15f) continue;
            if (!interactor.TryFirstHit(new Ray(head,delta.normalized),out RaycastHit hit) ||
                !ReferenceEquals(ApartmentFurnitureInteractor.FindInteractable(hit.collider), target)) continue;
            closest = target; distance = d;
        }
    }
    void PlacePanel(bool modal)
    {
        Transform head = locomotion.rig.centerEyeAnchor;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
        if (forward.sqrMagnitude < .5f) forward = locomotion.transform.forward;
        panel.transform.SetPositionAndRotation(head.position + forward * 1.2f - Vector3.up * (modal ? .10f : .38f),Quaternion.LookRotation(forward));
    }
    public void ProcessPointer(Ray ray, float maxDistance, bool pressed, out Vector3 end)
    {
        end = ray.GetPoint(maxDistance);
        if(ModalVisible && Session.Welcome) { welcomeView?.ProcessPointer(ray,maxDistance,pressed,out end); return; }
        if (!ModalVisible || okRect == null || !okRect.gameObject.activeInHierarchy || panel == null || !panel.activeSelf ||
            previousStep != Session.Step || previousKind != Session.PendingKind || !previousModal) return;
        bool hit = HitRectangle(okRect,ray,maxDistance,out Vector3 point);
        okImage.color = hit ? new Color(.12f,.78f,.62f,1) : new Color(.12f,.4f,.75f,1);
        if (hit) end = point;
        if (hit && pressed) { Session.Confirm(); interactor?.ResetUIInput(); okRect.gameObject.SetActive(false); }
    }
    void EnsureWelcome()
    {
        if(welcomeView!=null || font==null || panelShader==null || textOverlayShader==null)return;
        welcomeView=new ApartmentVRPanel(transform,"Tutorial welcome (session only)",font,panelShader,textOverlayShader,900,430);
        welcomeView.Label("Welcome title",WelcomeTitle,42,30,816,70,44,true);
        welcomeView.Label("Welcome explanation",WelcomeHelp,42,120,816,145,30);
        welcomeView.Button("Start tutorial","Start guide",42,314,390,78,()=>ChooseTutorial(true),true);
        welcomeView.Button("Skip tutorial","Skip / explore freely",468,314,390,78,()=>ChooseTutorial(false));
    }
    public void ChooseTutorial(bool start)
    {
        Session.ChooseTutorial(start); interactor?.ResetUIInput(); locomotion?.CancelTeleport();
        welcomeView?.SetVisible(false); previousStep=-99;
    }
    public void SkipBasicPractice()
    {
        Session.SkipPractice(); interactor?.ResetUIInput(); locomotion?.CancelTeleport(); previousStep = -99;
    }
    public RectTransform BuildWelcomePreview() { EnsureWelcome();welcomeView.SetVisible(true);return welcomeView.Root; }
    public static bool HitRectangle(RectTransform rect, Ray ray, float maxDistance, out Vector3 point)
    {
        point = default;
        var plane = new Plane(rect.forward,rect.position);
        if (!plane.Raycast(ray,out float distance) || distance < 0 || distance > maxDistance) return false;
        point = ray.GetPoint(distance); Vector3 local = rect.InverseTransformPoint(point);
        return rect.rect.Contains(new Vector2(local.x,local.y));
    }
    void EnsureVisuals()
    {
        if (panel != null || font == null) return;
        Shader overlay = panelShader != null ? panelShader : Shader.Find("Apartment/TutorialOverlay");
        Shader textShader = textOverlayShader != null ? textOverlayShader : Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlay == null || textShader == null) return;
        backgroundMaterial = new Material(overlay);
        textMaterial = new Material(font.material); textMaterial.shader = textShader;
        panel = new GameObject("VR Tutorial (session only)",typeof(RectTransform),typeof(Canvas));
        panel.transform.SetParent(transform,false); panel.layer = 5;
        var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 100;
        canvas.worldCamera = locomotion.rig.centerEyeAnchor.GetComponent<Camera>();
        panelRect = panel.GetComponent<RectTransform>(); panelRect.localScale = Vector3.one * .001f;
        var bg = MakeRect("Panel",panel.transform,new Vector2(0,0),new Vector2(1,1));
        var image = bg.gameObject.AddComponent<Image>(); image.material = backgroundMaterial; image.color = new Color(.025f,.045f,.065f,.97f); image.raycastTarget = false;
        var content = MakeRect("Chinese instruction",panel.transform,Vector2.zero,Vector2.one);
        label = content.gameObject.AddComponent<TextMeshProUGUI>(); ConfigureText(label);
        okRect = MakeRect("OK",panel.transform,new Vector2(.34f,0),new Vector2(.66f,0));
        okRect.offsetMin = new Vector2(0,24); okRect.offsetMax = new Vector2(0,102);
        okImage = okRect.gameObject.AddComponent<Image>(); okImage.material = backgroundMaterial; okImage.raycastTarget = false;
        var okLabel = MakeRect("Label",okRect,Vector2.zero,Vector2.one).gameObject.AddComponent<TextMeshProUGUI>();
        ConfigureText(okLabel); okLabel.fontSize = 42; okLabel.text = "OK";
    }
    void ConfigureText(TextMeshProUGUI text)
    {
        text.font = font; text.fontSharedMaterial = textMaterial; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
    }
    static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name,typeof(RectTransform)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
    }
}
