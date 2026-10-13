using System;
using UnityEngine;

public enum ApartmentCabinetState { Closed, Opening, Open, Closing, Blocked, Paused }

// The pivot is unscaled; the original scaled door mesh/collider is its child.
// Kinematic bodies do not stop themselves: every angular step is checked before committing.
[DisallowMultipleComponent]
public class ApartmentCabinetDoor : MonoBehaviour, IApartmentInteractable
{
    public BoxCollider doorCollider;
    public Renderer doorRenderer;
    public Rigidbody hingeBody;
    public ApartmentFurnitureInteractor inputOwner;
    public float openAngle = 90f;
    [Range(15f, 180f)] public float degreesPerSecond = 90f;
    public Quaternion closedLocalRotation = Quaternion.identity;

    public ApartmentCabinetState State { get; private set; }
    public float CurrentAngle { get; private set; }
    public string BlockedReason { get; private set; } = "";
    public bool IsMoving => State == ApartmentCabinetState.Opening || State == ApartmentCabinetState.Closing;
    public bool CanInteract => isActiveAndEnabled && ConfigurationError() == null && !IsMoving;
    public Transform HintAnchor => doorCollider != null ? doorCollider.transform : transform;
    public Renderer HighlightRenderer => doorRenderer;
    public virtual ApartmentInteractionKind HintCategory => ApartmentInteractionKind.Cabinet;
    public bool NeedsAttention => State == ApartmentCabinetState.Blocked || State == ApartmentCabinetState.Paused || ConfigurationError() != null;
    public event Action<ApartmentCabinetState, string> StateChanged;

    public const string OpenPrompt = "按右扳机打开";
    public const string ClosePrompt = "按右扳机关闭";
    public const string OpeningPrompt = "正在打开……";
    public const string ClosingPrompt = "正在关闭……";
    public const string BlockedPrompt = "被挡住，请让开后再按右扳机";
    public const string PausedPrompt = "已暂停，按右扳机继续";
    public const string InvalidPrompt = "柜门配置不完整";
    public static string FontCharacters => OpenPrompt + ClosePrompt + OpeningPrompt + ClosingPrompt +
        BlockedPrompt + PausedPrompt + InvalidPrompt + "厨房柜门";
    public string InteractionPrompt => ConfigurationError() != null ? InvalidPrompt :
        State == ApartmentCabinetState.Closed ? OpenPrompt : State == ApartmentCabinetState.Open ? ClosePrompt :
        State == ApartmentCabinetState.Opening ? OpeningPrompt : State == ApartmentCabinetState.Closing ? ClosingPrompt :
        State == ApartmentCabinetState.Blocked ? BlockedPrompt : PausedPrompt;

    readonly Collider[] neighbours = new Collider[128];
    float targetAngle;

    public string ConfigurationError()
    {
        if (doorCollider == null || doorRenderer == null || hingeBody == null || inputOwner == null)
            return "Cabinet door references are incomplete.";
        if (doorCollider.transform.parent != transform || doorRenderer.gameObject != doorCollider.gameObject ||
            hingeBody.transform != transform || !doorCollider.enabled || doorCollider.isTrigger ||
            !hingeBody.isKinematic || hingeBody.useGravity || Vector3.Distance(transform.lossyScale, Vector3.one) > .001f)
            return "Expected unscaled kinematic hinge with an enabled solid child door.";
        if (!Finite(openAngle) || Mathf.Abs(openAngle) < 1f || Mathf.Abs(openAngle) > 90f ||
            !Finite(degreesPerSecond) || degreesPerSecond < 15f || degreesPerSecond > 180f)
            return "Invalid opening angle or speed.";
        return null;
    }

    protected void Awake() { ResetClosed(); }
    protected void OnDisable() { PauseMotion(); }
    protected void FixedUpdate()
    {
        if (!IsMoving) return;
        if (inputOwner == null || !inputOwner.RuntimeReady) { PauseMotion(); return; }
        AdvanceMotion(Time.fixedDeltaTime, inputOwner.HeadPosition, true);
    }

    public bool TryInteract(out string reason)
    {
        reason = ConfigurationError();
        if (reason != null) return false;
        if (IsMoving) { reason = InteractionPrompt; return false; }
        if (State == ApartmentCabinetState.Closed) targetAngle = openAngle;
        else if (State == ApartmentCabinetState.Open) targetAngle = 0f;
        // Blocked/paused retain the previous target. A fresh press resumes, never auto-resumes.
        SetState(Mathf.Abs(targetAngle) > Mathf.Abs(CurrentAngle) ? ApartmentCabinetState.Opening : ApartmentCabinetState.Closing, "");
        reason = "";
        return true;
    }

    public void PauseMotion()
    {
        if (IsMoving) SetState(ApartmentCabinetState.Paused, "Input focus or tracking lost.");
    }

    // Also used by explicit Editor checks; no input or player movement is synthesized.
    public void ResetClosed()
    {
        CurrentAngle = targetAngle = 0f;
        CommitPose(0f);
        SetState(ApartmentCabinetState.Closed, "");
    }

    public void AdvanceMotion(float seconds, Vector3 headPosition, bool protectTrackedHead)
    {
        if (!IsMoving) return;
        if (!Finite(seconds) || seconds <= 0f || ConfigurationError() != null)
        { SetState(ApartmentCabinetState.Paused, "Invalid motion configuration."); return; }
        Physics.SyncTransforms();
        Vector3 half = Vector3.Scale(doorCollider.size, Abs(doorCollider.transform.lossyScale)) * .5f;
        float reach = (doorCollider.transform.localPosition + Vector3.Scale(doorCollider.center, doorCollider.transform.localScale)).magnitude + half.magnitude;
        // At most 1.5mm of edge travel per sample; the broadphase is expanded by that amount.
        float angularStep = Mathf.Min(.5f, .0015f / Mathf.Max(reach, .01f) * Mathf.Rad2Deg);
        float remaining = degreesPerSecond * Mathf.Min(seconds, .05f);
        while (remaining > .00001f && Mathf.Abs(CurrentAngle - targetAngle) > .00001f)
        {
            float next = Mathf.MoveTowards(CurrentAngle, targetAngle, Mathf.Min(remaining, angularStep));
            if (!PoseIsClear(next, half, headPosition, protectTrackedHead, out string reason))
            { SetState(ApartmentCabinetState.Blocked, reason); return; }
            remaining -= Mathf.Abs(next - CurrentAngle);
            CurrentAngle = next;
            CommitPose(next);
        }
        if (Mathf.Abs(CurrentAngle - targetAngle) < .0001f)
            SetState(Mathf.Abs(targetAngle) < .0001f ? ApartmentCabinetState.Closed : ApartmentCabinetState.Open, "");
    }

    bool PoseIsClear(float angle, Vector3 half, Vector3 head, bool protectHead, out string reason)
    {
        Quaternion pivotRotation = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) *
            closedLocalRotation * Quaternion.AngleAxis(angle, Vector3.up);
        Quaternion leafRotation = pivotRotation * doorCollider.transform.localRotation;
        Vector3 leafPosition = transform.position + pivotRotation * doorCollider.transform.localPosition;
        Vector3 center = leafPosition + leafRotation * Vector3.Scale(doorCollider.center, doorCollider.transform.lossyScale);
        if (protectHead)
        {
            Vector3 localHead = Quaternion.Inverse(leafRotation) * (head - center);
            Vector3 closest = new Vector3(Mathf.Clamp(localHead.x, -half.x, half.x),
                Mathf.Clamp(localHead.y, -half.y, half.y), Mathf.Clamp(localHead.z, -half.z, half.z));
            if ((localHead - closest).sqrMagnitude < .12f * .12f)
            { reason = "Tracked head is near the door sweep."; return false; }
        }
        int count = Physics.OverlapBoxNonAlloc(center, half + Vector3.one * .0015f, neighbours,
            leafRotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == neighbours.Length) { reason = "Collision query capacity exceeded."; return false; }
        for (int i = 0; i < count; i++)
        {
            Collider other = neighbours[i];
            if (other == null || other == doorCollider || other.transform.IsChildOf(transform) ||
                other.gameObject.scene != gameObject.scene) continue;
            if (Physics.ComputePenetration(doorCollider, leafPosition, leafRotation, other,
                other.transform.position, other.transform.rotation, out _, out float penetration) && penetration > .0002f)
            { reason = "Door sweep blocked by " + other.name; return false; }
        }
        reason = "";
        return true;
    }

    void CommitPose(float angle)
    {
        transform.localRotation = closedLocalRotation * Quaternion.AngleAxis(angle, Vector3.up);
        if (hingeBody != null) hingeBody.rotation = transform.rotation;
        // Auto Sync Transforms is disabled in this project. Other doors/rays/teleport must see this pose now.
        Physics.SyncTransforms();
    }
    void SetState(ApartmentCabinetState next, string reason)
    {
        bool changed = State != next || BlockedReason != reason;
        State = next; BlockedReason = reason;
        if (changed) StateChanged?.Invoke(next, reason);
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
}
