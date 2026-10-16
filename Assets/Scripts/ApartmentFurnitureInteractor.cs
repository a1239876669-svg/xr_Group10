using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;

public enum ApartmentInteractionKind { RoomDoor, Cabinet, Balcony }

public interface IApartmentInteractable
{
    ApartmentInteractionKind HintCategory { get; }
    bool NeedsAttention { get; }
    bool CanInteract { get; }
    string InteractionPrompt { get; }
    Transform HintAnchor { get; }
    Renderer HighlightRenderer { get; }
    bool TryInteract(out string reason);
}

// Explicit hysteresis + neutral rearming: held input never activates a newly hovered object.
public sealed class ApartmentInteractionTriggerLatch
{
    bool armed, held;
    public void Reset() { armed = held = false; }
    public bool Sample(float value, bool allowed)
    {
        if (!allowed || float.IsNaN(value) || float.IsInfinity(value)) { Reset(); return false; }
        if (value <= .25f) { armed = true; held = false; return false; }
        if (value < .75f || !armed || held) return false;
        held = true;
        return true;
    }
}

[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed class ApartmentFurnitureInteractor : MonoBehaviour
{
    public ApartmentVRLocomotion locomotion;
    public TMP_FontAsset hintFont;
    [Range(.25f, 3f)] public float maxDistance = 2f;
    public ApartmentCabinetDoor[] doors;
    public ApartmentSlidingDoor[] slidingDoors;
    public ApartmentTutorialController tutorial;
    public ApartmentMenuController menu;
    public bool RayOriginBlocked { get; private set; }
    public string Status { get; private set; } = "Waiting for tracked VR";
    public Vector3 HeadPosition => locomotion.rig.centerEyeAnchor.position;
    public bool RuntimeReady
    {
        get
        {
            if (!Application.isPlaying || !isActiveAndEnabled || locomotion == null || !locomotion.isActiveAndEnabled || locomotion.debugMode ||
                locomotion.ConfigurationError() != null || !Application.isFocused) return false;
            try
            {
                var rig = locomotion.rig;
                var manager = locomotion.manager;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                return rig.isActiveAndEnabled && manager.isActiveAndEnabled && OVRManager.instance == manager &&
                    XRSettings.isDeviceActive && OVRManager.isHmdPresent && OVRManager.hasVrFocus && OVRManager.hasInputFocus &&
                    head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked &&
                    OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) &&
                    OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch) &&
                    OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch) && rig.rightControllerAnchor != null &&
                    Finite(rig.centerEyeAnchor.position) && Finite(rig.rightControllerAnchor.position);
            }
            catch (System.Exception) { return false; }
        }
    }

    readonly ApartmentInteractionTriggerLatch trigger = new ApartmentInteractionTriggerLatch();
    readonly ApartmentMenuStickStepLatch menuStep = new ApartmentMenuStickStepLatch();
    readonly RaycastHit[] hits = new RaycastHit[128];
    readonly Collider[] originOverlaps = new Collider[128];
    IApartmentInteractable hover;
    Renderer highlighted;
    MaterialPropertyBlock originalBlock, highlightBlock;
    GameObject visuals, hint;
    LineRenderer line;
    TextMeshProUGUI label;
    Material lineMaterial;
    int teleportFrame = -1;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void OnEnable()
    {
        trigger.Reset();
        if (locomotion != null) locomotion.Teleported += OnTeleported;
    }
    void OnTeleported(Vector3 _) { teleportFrame = Time.frameCount; trigger.Reset(); ClearHover(); HideVisuals(); }
    void OnDisable()
    {
        if (locomotion != null) locomotion.Teleported -= OnTeleported;
        CancelForTrackingLoss();
        if (visuals != null) { if (Application.isPlaying) Destroy(visuals); else DestroyImmediate(visuals); }
        if (lineMaterial != null) { if (Application.isPlaying) Destroy(lineMaterial); else DestroyImmediate(lineMaterial); }
        visuals = hint = null; line = null; label = null; lineMaterial = null;
    }
    void OnApplicationFocus(bool focused) { if (!focused) CancelForTrackingLoss(); }
    void OnApplicationPause(bool paused) { if (paused) CancelForTrackingLoss(); }

    void LateUpdate()
    {
        if (!RuntimeReady) { CancelForTrackingLoss(); return; }
        if (menu != null && menu.isActiveAndEnabled && menu.Visible)
        {
            ClearHover(); EnsureVisuals(); if(line==null)return;
            if(hint!=null)hint.SetActive(false);
            var hand=locomotion.rig.rightControllerAnchor;
            var menuRay=new Ray(hand.position,hand.forward);
            bool clicked=trigger.Sample(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,OVRInput.Controller.RTouch),true);
            // Right stick up/down steps the hovered comfort row one notch per flick. The menu blocks
            // world input while open, so this axis is free here.
            var menuStick=OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick,OVRInput.Controller.RTouch);
            int menuStepDirection=menuStep.Sample(menuStick.y,locomotion.turnDeadZone);
            // Recovery UI must remain usable even if the tracked hand is inside a virtual wall.
            menu.ProcessPointer(menuRay,maxDistance,clicked,out Vector3 end,menuStepDirection);
            line.enabled=true;line.SetPosition(0,menuRay.origin);line.SetPosition(1,end);
            line.startColor=line.endColor=new Color(.2f,.95f,.85f);
            Status="Menu owns trigger";return;
        }
        if (tutorial != null && tutorial.isActiveAndEnabled && tutorial.ModalVisible)
        {
            ClearHover(); EnsureVisuals(); if (line == null) return;
            if (hint != null) hint.SetActive(false);
            var hand = locomotion.rig.rightControllerAnchor;
            var rayToUI = new Ray(hand.position, hand.forward);
            TryFirstHit(rayToUI, out _);
            bool clicked = trigger.Sample(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch), true);
            // The modal is an overlay; a nearby virtual wall must not trap the player behind an unclickable OK.
            tutorial.ProcessPointer(rayToUI, menu != null ? maxDistance : RayOriginBlocked ? 0 : maxDistance, clicked, out Vector3 end);
            line.enabled = true; line.SetPosition(0,rayToUI.origin); line.SetPosition(1,end);
            line.startColor = line.endColor = new Color(.2f,.95f,.85f);
            Status = "Tutorial confirmation owns trigger"; return;
        }
        if(menu!=null && menu.isActiveAndEnabled && menu.BlocksWorldInput)
        { trigger.Reset(); ClearHover(); HideVisuals(); Status="Waiting for released controls";return; }
        if (tutorial != null && tutorial.isActiveAndEnabled && !tutorial.AllowInteraction)
        { trigger.Reset(); ClearHover(); HideVisuals(); Status = "Tutorial practice"; return; }
        bool teleportOwnsInput = locomotion.IsTeleportAiming || teleportFrame == Time.frameCount ||
            ((menu == null || !menu.isActiveAndEnabled || menu.AllowsTeleport) && OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch));
        if (teleportOwnsInput)
        { trigger.Reset(); ClearHover(); HideVisuals(); Status = "Teleport has priority"; return; }
        EnsureVisuals();
        if (line == null || label == null) { trigger.Reset(); Status = "Interaction visuals/font are not configured"; return; }
        Physics.SyncTransforms();
        var origin = locomotion.rig.rightControllerAnchor;
        Ray ray = new Ray(origin.position, origin.forward);
        bool pressed = trigger.Sample(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch), true);
        if (menu != null && menu.review != null && menu.review.ProcessPointer(ray, pressed, out Vector3 reviewEnd))
        {
            ClearHover();
            if (hint != null) hint.SetActive(false);
            line.enabled = !menu.HasOpenPanel;
            line.SetPosition(0, ray.origin); line.SetPosition(1, reviewEnd);
            line.startColor = line.endColor = Color.cyan;
            Status = "Review tool owns trigger"; return;
        }
        bool found = TryFirstHit(ray, out RaycastHit hit);
        IApartmentInteractable next = found ? FindInteractable(hit.collider) : null;
        if (!ReferenceEquals(next, hover)) { ClearHover(); hover = next; ApplyHighlight(); }
        line.enabled = true;
        line.SetPosition(0, ray.origin);
        line.SetPosition(1, found ? hit.point : ray.GetPoint(maxDistance));
        line.startColor = line.endColor = hover != null ? new Color(.2f, .95f, .85f) : new Color(.75f, .8f, .85f, .5f);
        if (pressed && hover != null && hover.CanInteract) hover.TryInteract(out _);
        hint.SetActive(hover != null && (tutorial == null || !tutorial.isActiveAndEnabled ||
            !tutorial.HasSeen(hover.HintCategory) || hover.NeedsAttention));
        if (hover == null) { Status = "No interactive target"; return; }
        label.text = hover.InteractionPrompt;
        Vector3 anchor = hover.HintAnchor.position;
        if (hover.HighlightRenderer != null) anchor = hover.HighlightRenderer.bounds.center +
            Vector3.up * (hover.HighlightRenderer.bounds.extents.y + .13f);
        hint.transform.position = anchor;
        Vector3 facing = anchor - HeadPosition;
        if (facing.sqrMagnitude > .0001f) hint.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        Status = label.text;
    }

    public void ResetUIInput() { trigger.Reset(); menuStep.Reset(); ClearHover(); HideVisuals(); }

    // The two-argument form keeps the authored interaction reach; the overload exists for callers that
    // need a longer ray (review measuring reaches 30 m) without changing any existing caller's behaviour.
    public bool TryFirstHit(Ray ray, out RaycastHit hit) => TryFirstHit(ray, maxDistance, out hit);
    public bool TryFirstHit(Ray ray, float distance, out RaycastHit hit)
    {
        hit = default;
        RayOriginBlocked = false;
        if (locomotion == null || !Finite(ray.origin) || !Finite(ray.direction) ||
            float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f) return false;
        // Raycasts don't report a wall containing the origin. Fail closed if the hand is inside a solid.
        int near = Physics.OverlapSphereNonAlloc(ray.origin, .002f, originOverlaps, ~0, QueryTriggerInteraction.Collide);
        if (near == originOverlaps.Length) { RayOriginBlocked = true; return false; }
        for (int i = 0; i < near; i++)
        {
            var c = originOverlaps[i];
            if (!Eligible(c)) continue;
            if ((c.ClosestPoint(ray.origin) - ray.origin).sqrMagnitude < .00000001f) { RayOriginBlocked = true; return false; }
        }
        int count = Physics.RaycastNonAlloc(ray, hits, distance, ~0, QueryTriggerInteraction.Collide);
        if (count == hits.Length) { RayOriginBlocked = true; return false; }
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (Eligible(hits[i].collider) && hits[i].distance < nearest)
            { nearest = hits[i].distance; hit = hits[i]; }
        return hit.collider != null;
    }
    bool Eligible(Collider c) => ApartmentDoorAimSurface.IsAimSolid(c) && c.gameObject.scene == gameObject.scene &&
        !c.transform.IsChildOf(locomotion.transform);
    public static IApartmentInteractable FindInteractable(Collider collider)
    {
        if (collider == null) return null;
        foreach (var component in collider.GetComponentsInParent<MonoBehaviour>())
            if (component is IApartmentInteractable target && component.isActiveAndEnabled) return target;
        return null;
    }

    void CancelForTrackingLoss()
    {
        trigger.Reset(); menuStep.Reset(); ClearHover(); HideVisuals(); Status = "Paused: tracking/input focus unavailable";
        if (doors != null) foreach (var door in doors) if (door != null) door.PauseMotion();
        if (slidingDoors != null) foreach (var door in slidingDoors) if (door != null) door.PauseMotion();
    }
    void ClearHover()
    {
        if (highlighted != null) highlighted.SetPropertyBlock(originalBlock);
        highlighted = null; hover = null;
    }
    void ApplyHighlight()
    {
        var candidate = hover?.HighlightRenderer;
        if (candidate == null || candidate.sharedMaterial == null || !candidate.sharedMaterial.HasProperty(ColorId)) return;
        highlighted = candidate;
        originalBlock = new MaterialPropertyBlock(); highlighted.GetPropertyBlock(originalBlock);
        highlightBlock = new MaterialPropertyBlock(); highlighted.GetPropertyBlock(highlightBlock);
        highlightBlock.SetColor(ColorId, Color.Lerp(highlighted.sharedMaterial.GetColor(ColorId), new Color(.2f, 1f, .85f), .45f));
        highlighted.SetPropertyBlock(highlightBlock);
    }
    void HideVisuals() { if (line != null) line.enabled = false; if (hint != null) hint.SetActive(false); }
    void EnsureVisuals()
    {
        if (visuals != null || hintFont == null) return;
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        visuals = new GameObject("Furniture Interaction Visuals"); visuals.transform.SetParent(transform, false);
        var rayObject = new GameObject("Right Trigger Ray"); rayObject.transform.SetParent(visuals.transform, false);
        line = rayObject.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
        line.startWidth = line.endWidth = .003f; lineMaterial = new Material(shader); line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        hint = new GameObject("Cabinet Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        hint.transform.SetParent(visuals.transform, false); hint.layer = 5;
        var canvas = hint.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = locomotion.rig.centerEyeAnchor.GetComponent<Camera>();
        var rect = hint.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(800, 180); rect.localScale = Vector3.one * .001f;
        var group = hint.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
        background.layer = 5;
        background.transform.SetParent(hint.transform, false); Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = new Color(.025f, .04f, .055f, .85f); background.GetComponent<Image>().raycastTarget = false;
        var text = new GameObject("Instruction", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.layer = 5;
        text.transform.SetParent(hint.transform, false); Stretch(text.GetComponent<RectTransform>());
        label = text.GetComponent<TextMeshProUGUI>(); label.font = hintFont; label.fontSize = 36;
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
        label.margin = new Vector4(20, 12, 20, 12); label.text = ApartmentCabinetDoor.OpenPrompt;
        HideVisuals();
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static bool Finite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
        float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));
}
