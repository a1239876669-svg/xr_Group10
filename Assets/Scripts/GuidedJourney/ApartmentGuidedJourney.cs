using System;
using System.Collections.Generic;
using Group10.GuidedJourney;
using QuestGuidance.Review;
using TMPro;
using UnityEngine;
using XRGroup10.ShoppingBag;

// Observes explicitly wired existing components. No input binding, physics override or scene search.
[DisallowMultipleComponent, DefaultExecutionOrder(12000)]
public sealed class ApartmentGuidedJourney : MonoBehaviour
{
    public ApartmentTutorialController tutorial;
    public ApartmentMenuController menu;
    public ApartmentVRLocomotion locomotion;
    public ApartmentFurnitureInteractor interactor;
    public ReviewRoomMap roomMap;
    public ReviewTools review;
    public ApartmentShoppingBagBridge bagBridge;
    public ApartmentRoomDoor entranceDoor;
    public ApartmentSlidingDoor balconyDoor;
    public ApartmentCabinetDoor practiceCabinet;
    public Transform kitchenDropPoint;
    public Vector3 dropAreaSize = new Vector3(1.30f, 1f, .90f);
    public float dropFloorY = .012f;
    public float livingExplorationMetres = 2f;
    public float nearBalconyDistance = 1.65f;
    public ShoppingBagHint supersededBagHint;
    public bool showTaskCard = true;
    public JourneyStateMachine Journey { get; private set; }
    public bool IsFinished => Journey != null && Journey.IsFinished;
    public bool OwnsContextLessons => isActiveAndEnabled && IsConfigured;
    public bool IsConfigured => tutorial != null && menu != null && locomotion != null && interactor != null &&
        roomMap != null && review != null && bagBridge != null && bagBridge.bag != null && entranceDoor != null &&
        balconyDoor != null && practiceCabinet != null && kitchenDropPoint != null;
    public bool FeedbackActuallySaved { get; private set; }
    public string LastSavedMarkerId { get; private set; }
    public string StageTitle => ApartmentLanguage.Display(Titles[(int)(Journey?.CurrentStage ?? JourneyStage.WelcomeMovement)]);
    public static readonly string[] Titles = {
        "0 / 11 - Welcome and movement", "1 / 11 - Enter the apartment", "2 / 11 - Explore the living room",
        "3 / 11 - Try the balcony door", "4 / 11 - Try a kitchen cabinet", "5 / 11 - Return to the entrance",
        "6 / 11 - Choose and pick up a bag", "7 / 11 - Carry shopping to the kitchen", "8 / 11 - Put the bag down",
        "9 / 11 - Measure an opening", "10 / 11 - Mark an issue and save", "11 / 11 - Free exploration" };
    public const string ModelHelp = "Structure/walls are fixed. Selected doors and cabinets open. Furniture and dimensions are simplified assumptions, not certified clearance.";
    public const string SafetyHelp = "Stop if uncomfortable. B > Settings / 设置 > 返回安全点 returns to a checked entrance landing. Exit through B > 退出游戏.";
    public const string GuideFooter = "B > Controls / 按键说明 > Review guide: help, skip a task, or explore freely.";
    public static string AllText => string.Join("", Titles) + ModelHelp + SafetyHelp + GuideFooter +
        "Review guideSkip this taskSkip all / exploreShow task cardHide task cardBack to controlsSkip basic practice" +
        "Welcome! You are reviewing this apartment to identify possible problems with everyday movement and furniture use.";
    ApartmentVRPanel card, targetTag, toolHint;
    readonly GuidancePanelPlacement toolPlacement = new GuidancePanelPlacement { VerticalOffset = -.26f };
    TextMeshProUGUI toolTitle, toolTask, toolHelp;
    bool toolWasVisible;
    readonly GuidancePanelPlacement cardPlacement = new GuidancePanelPlacement();
    bool cardWasVisible, recoveryHelp;
    TextMeshProUGUI cardTitle, cardTask;
    GameObject dropVisual;
    Material dropMaterial;
    long sizeRevision, measurementRevision, savedRevision;
    bool discontinuous, aimedTeleport, subscribed, skippedFromWelcome, paused;
    float settled;
    double walkedThisFrame;
    readonly HashSet<string> freshFeedbackMarkers = new HashSet<string>();
    JourneyObservation lastObservation;
    JourneyStage previousStage;
    string lastRenderedText;

    void Awake()
    {
        Journey = new JourneyStateMachine(new JourneyConfig { LivingExplorationMetres = Mathf.Clamp(livingExplorationMetres, .5f, 3f) });
        previousStage = Journey.CurrentStage;
    }
    void OnEnable() { ApartmentLanguage.Changed += LanguageChanged; Subscribe(); }
    void LanguageChanged() { lastRenderedText = null; if(card!=null) { card.SetLabelText(cardTitle,StageTitle); card.SetLabelText(cardTask,CurrentInstruction); } RefreshToolHintText(); }
    void Start()
    {
        if (!IsConfigured) { Debug.LogError("Guided journey: missing explicit scene references; existing apartment controls remain available.", this); enabled = false; return; }
        if (supersededBagHint != null) { supersededBagHint.Dismiss(); supersededBagHint.enabled = false; }
        EnsureCard();
    }
    void Subscribe()
    {
        if (subscribed || !IsConfigured) return;
        locomotion.Teleported += Relocated;
        locomotion.Walked += Walked;
        bagBridge.SizeSelected += SizeSelected;
        review.RecordCreated += RecordCreated;
        review.FeedbackSaved += FeedbackSaved;
        subscribed = true;
    }
    void OnDisable()
    {
        ApartmentLanguage.Changed -= LanguageChanged; cardPlacement.Reset(); cardWasVisible=false; toolPlacement.Reset(); toolWasVisible=false; toolHint?.SetVisible(false);
        if (subscribed)
        {
            if (locomotion != null) { locomotion.Teleported -= Relocated; locomotion.Walked -= Walked; }
            if (bagBridge != null) bagBridge.SizeSelected -= SizeSelected;
            if (review != null) { review.RecordCreated -= RecordCreated; review.FeedbackSaved -= FeedbackSaved; }
        }
        subscribed = false; card?.SetVisible(false); if (dropVisual != null) dropVisual.SetActive(false);
        InvalidateObservation(); targetTag?.SetVisible(false);
    }
    void OnDestroy()
    {
        card?.Dispose(); card = null; targetTag?.Dispose(); targetTag = null; toolHint?.Dispose(); toolHint = null;
        if (dropVisual != null) Destroy(dropVisual);
        if (dropMaterial != null) Destroy(dropMaterial);
    }
    void OnApplicationFocus(bool value) { if (!value) InvalidateObservation(); }
    void OnApplicationPause(bool value) { paused = value; if (value) InvalidateObservation(); }
    void InvalidateObservation()
    {
        discontinuous = true; aimedTeleport = false; walkedThisFrame = 0; settled = 0;
        var unavailable = lastObservation; unavailable.Focused = unavailable.TrackingValid = false;
        Journey?.Tick(unavailable);
        card?.SetVisible(false); targetTag?.SetVisible(false); toolHint?.SetVisible(false); toolWasVisible=false;
        if (dropVisual != null) dropVisual.SetActive(false);
    }
    bool Ready => !paused && Application.isFocused && IsConfigured && interactor.RuntimeReady;
    void Walked(float distance) { if (Ready && float.IsFinite(distance) && distance > 0) walkedThisFrame += distance; }
    void Relocated(Vector3 _) { discontinuous = true; aimedTeleport = locomotion.LastRelocationReason == ApartmentRelocationReason.AimedTeleport; }
    void SizeSelected(BagSize _) { if (Ready) sizeRevision++; }
    void RecordCreated(ReviewRecord record)
    {
        if (!Ready || record == null) return;
        if (record.kind == "measurement" && record.distanceMetres > .001f && float.IsFinite(record.distanceMetres)) measurementRevision++;
        if (record.kind == "marker" && Journey.CurrentStage == JourneyStage.Feedback) freshFeedbackMarkers.Add(record.id);
    }
    void FeedbackSaved(ReviewRecord record)
    {
        if (!Ready || record == null || Journey.CurrentStage != JourneyStage.Feedback || !freshFeedbackMarkers.Contains(record.id) ||
            record.kind != "marker" || record.state != FeedbackState.Submitted || !string.IsNullOrEmpty(review.LastError)) return;
        savedRevision++; FeedbackActuallySaved = true; LastSavedMarkerId = record.id;
    }
    JourneyRoom RoomAt(Vector3 point)
    {
        string room = roomMap.Resolve(point);
        if (room == "Entrance") return JourneyRoom.Entrance;
        if (room == "LivingRoom" || room == "Living Room") return JourneyRoom.Living;
        if (room == "Kitchen") return JourneyRoom.Kitchen;
        if (room == "Balcony") return JourneyRoom.Balcony;
        return JourneyRoom.Unknown;
    }
    JourneyObservation Observe()
    {
        bool ready = Ready;
        Vector3 root = locomotion.transform.position;
        Vector3 tracked = root;
        if (locomotion.rig != null && locomotion.rig.centerEyeAnchor != null) tracked = locomotion.rig.centerEyeAnchor.position;
        tracked.y = .8f; // Room classification only: stable floor-local sampling, never moves the camera.
        var o = new JourneyObservation {
            Focused = ready, TrackingValid = ready, DeltaTime = Time.unscaledDeltaTime,
            PositionX = tracked.x, PositionZ = tracked.z, DiscontinuousMove = discontinuous, AimedTeleport = aimedTeleport,
            UseReportedWalkDistance = true, ReportedWalkDistance = walkedThisFrame,
            Room = RoomAt(tracked), BasicsFinished = tutorial.Session.Step == 3 && !tutorial.Session.Explaining && !tutorial.Session.WaitingForNeutral,
            EntranceDoorOpen = entranceDoor.State == ApartmentCabinetState.Open,
            BalconyDoorOpen = balconyDoor.State == ApartmentCabinetState.Open,
            CabinetOpen = practiceCabinet.State == ApartmentCabinetState.Open,
            CabinetClosed = practiceCabinet.State == ApartmentCabinetState.Closed,
            BagHeld = bagBridge.bag.IsHeld, SizeSelectionRevision = sizeRevision,
            MeasurementRevision = measurementRevision, FeedbackSavedRevision = savedRevision
        };
        Vector3 balconyPosition = balconyDoor.transform.position;
        o.NearBalcony = o.Room == JourneyRoom.Living && Vector2.Distance(new Vector2(tracked.x, tracked.z), new Vector2(balconyPosition.x, balconyPosition.z)) <= nearBalconyDistance;
        bool inside = BagReleasedInsideDropArea();
        settled = ready && inside ? settled + Mathf.Min(Time.unscaledDeltaTime, .1f) : 0;
        o.BagInsideDropZone = settled >= .35f;
        return o;
    }
    // Whole real body bounds must fit; the user must release and settle near the actual floor.
    public bool BagReleasedInsideDropArea()
    {
        if (!IsConfigured || bagBridge.bag.IsHeld || bagBridge.bag.BodyCollider == null || !bagBridge.bag.BodyCollider.enabled) return false;
        Bounds bag = bagBridge.bag.BodyCollider.bounds;
        Bounds zone = new Bounds(kitchenDropPoint.position + Vector3.up * dropAreaSize.y * .5f, dropAreaSize);
        return bag.min.x >= zone.min.x && bag.max.x <= zone.max.x && bag.min.z >= zone.min.z && bag.max.z <= zone.max.z &&
            bag.max.y <= zone.max.y && bag.min.y >= dropFloorY - .04f && bag.min.y <= dropFloorY + .12f;
    }
    void LateUpdate()
    {
        if (!IsConfigured) { card?.SetVisible(false); toolHint?.SetVisible(false); return; }
        Subscribe();
        lastObservation = Observe();
        if (tutorial.Session.Skipped && !skippedFromWelcome) { Journey.SkipAll(lastObservation); skippedFromWelcome = true; showTaskCard = false; }
        else Journey.Tick(lastObservation);
        discontinuous = aimedTeleport = false; walkedThisFrame = 0;
        if (Journey.CurrentStage != previousStage)
        {
            if (Journey.CurrentStage == JourneyStage.Feedback) freshFeedbackMarkers.Clear();
            previousStage = Journey.CurrentStage; settled = 0; cardPlacement.Reset(); lastRenderedText = null;
        }
        EnsureDropVisual();
        UpdateCabinetTag();
        UpdateToolHint(Ready && !menu.HasOpenPanel && !tutorial.ModalVisible && !locomotion.IsTeleportAiming);
        bool visible = Ready && showTaskCard && !menu.HasOpenPanel && !tutorial.ModalVisible && !locomotion.IsTeleportAiming && Journey.CurrentStage != JourneyStage.WelcomeMovement && !interactor.HasWorldHover && review.Mode == ReviewMode.None;
        card?.SetVisible(visible);
        if (!visible || card == null) { cardWasVisible = false; return; }
        string task = CurrentInstruction;
        if (task != lastRenderedText) { card.SetLabelText(cardTitle,StageTitle); card.SetLabelText(cardTask,task); lastRenderedText = task; }
        cardPlacement.Place(locomotion.rig.centerEyeAnchor,card.Root,locomotion.transform.forward,Time.unscaledTime,!cardWasVisible);
        cardWasVisible=true;
    }
    // One small view owned by this guide; no new tutorial state or input owner.
    void UpdateToolHint(bool ready)
    {
        bool visible = ready && review != null && review.Mode != ReviewMode.None;
        if (visible && toolHint == null && menu != null && menu.font != null)
        {
            toolHint = new ApartmentVRPanel(transform, "Current review tool (no collider)", menu.font, menu.panelShader, menu.textOverlayShader, 700, 172);
            toolTitle = toolHint.Label("Tool", "", 18, 6, 664, 32, 25, true);
            toolTask = toolHint.Label("Current tool task", "", 18, 42, 664, 74, 25);
            toolHelp = toolHint.Label("Tool recovery", "", 18, 122, 664, 40, 19);
        }
        toolHint?.SetVisible(visible);
        if (!visible || toolHint == null) { toolWasVisible = false; return; }
        RefreshToolHintText();
        if (locomotion != null && locomotion.rig != null)
            toolPlacement.Place(locomotion.rig.centerEyeAnchor, toolHint.Root, locomotion.transform.forward, Time.unscaledTime, !toolWasVisible);
        toolWasVisible = true;
    }
    void RefreshToolHintText()
    {
        if (toolHint == null || review == null) return;
        bool measure = review.Mode == ReviewMode.Measure;
        toolHint.SetLabelText(toolTitle, ApartmentLanguage.Text(measure ? "tool.measure.title" : "tool.marker.title"));
        toolHint.SetLabelText(toolTask, ApartmentLanguage.Text(measure ? "tool.measure.task" : "tool.marker.task") + "\n" + review.CurrentToolReadout);
        toolHint.SetLabelText(toolHelp, ApartmentLanguage.Text(measure ? "tool.measure.help" : "tool.marker.help"));
    }
    public string CurrentInstruction => ApartmentLanguage.Display(CurrentInstructionSource);

    string CurrentInstructionSource
    {
        get
        {
            if (!IsConfigured || Journey == null) return "Guide is unavailable. Existing menu and controls remain available.";
            if ((Journey.CurrentStage == JourneyStage.EntranceDoor || Journey.CurrentStage == JourneyStage.BalconyDoor ||
                Journey.CurrentStage == JourneyStage.CabinetPractice) && review.Mode != ReviewMode.None)
                return "A measurement/marker tool currently owns Trigger. Open B > 测距 and choose 停止工具 (Stop tool), then try the door.";
            if (Journey.CurrentStage == JourneyStage.Feedback)
                foreach (var record in review.ActiveRecords)
                    if (record.kind == "marker" && record.state == FeedbackState.Draft && !freshFeedbackMarkers.Contains(record.id))
                        return "An earlier draft is open. Save it then choose 继续标记 (Continue marking), or cancel that draft. Place a new marker for this task.";
            switch (Journey.CurrentStage)
            {
                case JourneyStage.WelcomeMovement: return "Practise the basic controls, or use B > Controls > Review guide to skip a practice.";
                case JourneyStage.EntranceDoor: return "Let's enter the apartment. Point at the entrance door and press right Trigger, then move inside.";
                case JourneyStage.LivingExplore: return "Great! Explore the living room at your own pace. Look around, then approach the balcony.";
                case JourneyStage.BalconyDoor: return balconyDoor.NeedsAttention ? "The sliding door is paused or blocked. Step clear and press right Trigger to resume, or skip this practice in Help." : "Would you like to explore the balcony? Point at its sliding door and press right Trigger to open it.";
                case JourneyStage.CabinetPractice: return Journey.CabinetOpenedThisStage ? "Great! Close this same cabinet with right Trigger. If blocked, step clear and press again." : "Well done! Visit the kitchen and find the Cabinet practice label. Close it first if open, then open and close it with right Trigger. No bag yet.";
                case JourneyStage.ReturnEntrance: return "Great! Return to the entrance for a daily activity. Walk or use A to teleport; choose any safe route.";
                case JourneyStage.BagSelectPickup: return !Journey.SizeSelectedThisStage ? "At the entrance, open B > My Items / 我的物品. Put the bag down and choose Small, Medium or Large." : "Close the menu, release Grip, move either hand near the handle, then hold Grip to pick up the bag.";
                case JourneyStage.CarryKitchen: return !bagBridge.bag.IsHeld ? "Your progress is kept. Return to your bag and hold Grip near the handle to carry on." : "Carry your bag through the living room into the kitchen. Red contact warns of a possible spatial issue; move clear to restore it.";
                case JourneyStage.KitchenDrop: return "Kitchen drop task (not cabinet storage): move the bag into the cyan floor area, release Grip, and let it settle.";
                case JourneyStage.Measurement: return "Good! Open B > 测距 > 开始测距. Use right Trigger to choose two real surface points and read the distance. Cancel/retry or skip in Help.";
                case JourneyStage.Feedback: return !string.IsNullOrEmpty(review.LastError) ? "Feedback was not confirmed saved. Keep the record, check the error in the menu and retry Save feedback. You may skip in Help." : "Open B > 标记注释 > 放置标记. Place a new marker, choose aspect, rating and importance, then 保存反馈. Wait for a successful save.";
                default: return Journey.WasSkipped ? "Guide finished with skipped tasks. Explore freely; only a successful Save confirms feedback storage. More measurements, markers and menu Exit remain available." : "Review complete! Explore freely, take more measurements, leave more feedback, or exit through the menu whenever you are ready.";
            }
        }
    }
    void EnsureCard()
    {
        if (card != null || menu == null || menu.font == null || menu.panelShader == null || menu.textOverlayShader == null) return;
        card = new ApartmentVRPanel(transform, "Guided review task card (no collider)", menu.font, menu.panelShader, menu.textOverlayShader, 870, 270);
        cardTitle = card.Label("Stage", StageTitle, 22, 12, 826, 44, 29, true);
        cardTask = card.Label("Current task", CurrentInstruction, 22, 60, 826, 142, 27);
        card.Label("Help", "B > 按键说明 > Review guide | Help / Skip", 22, 218, 826, 38, 21);
        card.SetVisible(false);
    }
    void EnsureDropVisual()
    {
        bool visible = Ready && Journey.CurrentStage == JourneyStage.KitchenDrop && !menu.HasOpenPanel;
        if (visible && dropVisual == null)
        {
            // Visual only: a flat line rectangle cannot obstruct doors, rays, locomotion or bag physics.
            dropVisual = new GameObject("Kitchen drop outline (visual only)");
            dropVisual.transform.SetParent(kitchenDropPoint, false);
            var line = dropVisual.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.positionCount = 4; line.startWidth = line.endWidth = .025f;
            dropMaterial = new Material(menu.panelShader); dropMaterial.color = new Color(.1f, .85f, .9f, .85f); line.sharedMaterial = dropMaterial;
            float x = dropAreaSize.x * .5f, z = dropAreaSize.z * .5f;
            line.SetPositions(new[] { new Vector3(-x, .025f, -z), new Vector3(x, .025f, -z), new Vector3(x, .025f, z), new Vector3(-x, .025f, z) });
            line.startColor = line.endColor = Color.cyan;
        }
        if (dropVisual != null) dropVisual.SetActive(visible);
    }
    void UpdateCabinetTag()
    {
        bool visible = Ready && !menu.HasOpenPanel && !tutorial.ModalVisible && Journey.CurrentStage == JourneyStage.CabinetPractice;
        if (visible && targetTag == null)
        {
            targetTag = new ApartmentVRPanel(transform, "Cabinet practice label (no collider)", menu.font, menu.panelShader, menu.textOverlayShader, 340, 66);
            targetTag.Label("Target", "Cabinet practice", 10, 7, 320, 50, 28, true);
        }
        targetTag?.SetVisible(visible);
        if (!visible || targetTag == null) return;
        var renderer = practiceCabinet.HighlightRenderer;
        if (renderer == null) return;
        Vector3 point = renderer.bounds.center + Vector3.up * (renderer.bounds.extents.y + .12f);
        Vector3 facing = point - locomotion.rig.centerEyeAnchor.position;
        if (facing.sqrMagnitude < .001f) return;
        targetTag.Root.SetPositionAndRotation(point, Quaternion.LookRotation(facing));
        targetTag.Root.GetComponent<Canvas>().worldCamera = locomotion.rig.centerEyeAnchor.GetComponent<Camera>();
    }
    public void SkipCurrent()
    {
        if (!Ready || !menu.IsOpen) return;
        if (Journey.CurrentStage == JourneyStage.WelcomeMovement) tutorial.SkipBasicPractice();
        Journey.SkipCurrentStage(Observe()); lastRenderedText = null;
    }
    public void SkipAll()
    {
        if (!Ready || !menu.IsOpen) return;
        tutorial.SkipBasicPractice(); Journey.SkipAll(Observe()); lastRenderedText = null;
    }
    public void BuildHelp(ApartmentVRPanel panel, Action back)
    {
        if (panel == null) return;
        if(recoveryHelp)
        {
            panel.Label("Recovery title",ApartmentLanguage.Text("recovery.title"),330,190,730,48,30,true);
            panel.Label("Recovery text",ApartmentLanguage.Text("recovery.help"),330,246,730,310,26);
            panel.Button("Back guide",ApartmentLanguage.Text("recovery.back"),330,580,730,54,()=>{ recoveryHelp=false; menu.RefreshGuideHelp(); },false,25);
            return;
        }
        panel.Label("Guide stage", StageTitle, 330, 190, 730, 48, 30, true);
        panel.Label("Guide instruction", CurrentInstruction, 330, 246, 730, 128, 27);
        panel.Label("Model and safety", ModelHelp + "\n" + SafetyHelp, 330, 384, 730, 112, 22);
        panel.Button("Skip task", "Skip this task (not completed)", 330, 510, 350, 56, () => { SkipCurrent(); menu.RefreshGuideHelp(); }, false, 23);
        panel.Button("Skip guide", "Skip all / explore", 710, 510, 350, 56, () => { SkipAll(); menu.RefreshGuideHelp(); }, false, 25);
        panel.Button("Toggle task card", showTaskCard ? "Hide task card" : "Show task card", 330, 580, 350, 54, () => { showTaskCard = !showTaskCard; menu.RefreshGuideHelp(); }, false, 25);
        panel.Button("Back controls", "Back to controls", 710, 580, 350, 54, back, false, 25);
        panel.Button("Recovery help",ApartmentLanguage.Text("recovery.title"),330,635,730,42,()=>{ recoveryHelp=true;menu.RefreshGuideHelp(); },false,24);
    }
}
