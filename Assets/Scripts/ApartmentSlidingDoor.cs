using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ApartmentSlidingDoor : MonoBehaviour, IApartmentInteractable
{
    public Transform movingLeaf;
    public BoxCollider[] movingColliders;
    public Renderer doorRenderer;
    public Rigidbody body;
    public ApartmentFurnitureInteractor inputOwner;
    public Vector3 closedLocalPosition;
    public Vector3 localTravel = new Vector3(1.55f, 0, 0);
    public float metresPerSecond = .75f;
    public ApartmentCabinetState State { get; private set; }
    public float CurrentTravel { get; private set; }
    public string BlockedReason { get; private set; } = "";
    public bool IsMoving => State == ApartmentCabinetState.Opening || State == ApartmentCabinetState.Closing;
    public bool CanInteract => isActiveAndEnabled && ConfigurationError() == null && !IsMoving;
    public Transform HintAnchor => doorRenderer != null ? doorRenderer.transform : transform;
    public Renderer HighlightRenderer => doorRenderer;
    public ApartmentInteractionKind HintCategory => ApartmentInteractionKind.Balcony;
    public bool NeedsAttention => State == ApartmentCabinetState.Blocked || State == ApartmentCabinetState.Paused || ConfigurationError() != null;
    public event Action<ApartmentCabinetState, string> StateChanged;
    public string InteractionPrompt => ConfigurationError() != null ? "推拉门配置不完整" :
        State == ApartmentCabinetState.Closed ? ApartmentCabinetDoor.OpenPrompt :
        State == ApartmentCabinetState.Open ? ApartmentCabinetDoor.ClosePrompt :
        State == ApartmentCabinetState.Opening ? ApartmentCabinetDoor.OpeningPrompt :
        State == ApartmentCabinetState.Closing ? ApartmentCabinetDoor.ClosingPrompt :
        State == ApartmentCabinetState.Blocked ? ApartmentCabinetDoor.BlockedPrompt : ApartmentCabinetDoor.PausedPrompt;
    readonly Collider[] neighbours = new Collider[128];
    float target;

    public string ConfigurationError()
    {
        if (movingLeaf == null || movingColliders == null || movingColliders.Length != 5 || body == null ||
            doorRenderer == null || inputOwner == null || body.transform != movingLeaf || !body.isKinematic || body.useGravity)
            return "Sliding door references are incomplete.";
        if (movingLeaf.parent != transform || Vector3.Distance(transform.lossyScale, Vector3.one) > .001f ||
            localTravel.sqrMagnitude < .01f || !Finite(localTravel.x) || !Finite(localTravel.y) || !Finite(localTravel.z) ||
            !Finite(metresPerSecond) || metresPerSecond <= 0 || metresPerSecond > 2) return "Invalid sliding geometry.";
        foreach (var c in movingColliders)
            if (c == null || !c.enabled || c.isTrigger || !c.transform.IsChildOf(movingLeaf)) return "Invalid sliding collider.";
        return null;
    }
    void Awake() { ResetClosed(); }
    void OnDisable() { PauseMotion(); }
    void FixedUpdate()
    {
        if (!IsMoving) return;
        if (inputOwner == null || !inputOwner.RuntimeReady) { PauseMotion(); return; }
        AdvanceMotion(Time.fixedDeltaTime, inputOwner.HeadPosition, true);
    }
    public void ResetClosed() { CurrentTravel = target = 0; Commit(); SetState(ApartmentCabinetState.Closed, ""); }
    public void PauseMotion() { if (IsMoving) SetState(ApartmentCabinetState.Paused, "Input focus or tracking lost."); }
    public bool TryInteract(out string reason)
    {
        reason = ConfigurationError(); if (reason != null) return false;
        if (IsMoving) { reason = InteractionPrompt; return false; }
        if (State == ApartmentCabinetState.Closed) target = localTravel.magnitude;
        else if (State == ApartmentCabinetState.Open) target = 0;
        SetState(target > CurrentTravel ? ApartmentCabinetState.Opening : ApartmentCabinetState.Closing, "");
        reason = ""; return true;
    }
    public void AdvanceMotion(float seconds, Vector3 head, bool protectHead)
    {
        if (!IsMoving) return;
        if (!Finite(seconds) || seconds <= 0 || ConfigurationError() != null) { PauseMotion(); return; }
        Physics.SyncTransforms();
        float remaining = metresPerSecond * Mathf.Min(seconds, .05f);
        while (remaining > .000001f && Mathf.Abs(CurrentTravel - target) > .000001f)
        {
            float next = Mathf.MoveTowards(CurrentTravel, target, Mathf.Min(.0015f, remaining));
            Vector3 delta = transform.TransformVector(localTravel.normalized * (next - CurrentTravel));
            foreach (var c in movingColliders)
                if (!PoseClear(c, delta, head, protectHead, out string reason))
                { SetState(ApartmentCabinetState.Blocked, reason); return; }
            remaining -= Mathf.Abs(next - CurrentTravel); CurrentTravel = next; Commit();
        }
        if (Mathf.Abs(CurrentTravel - target) < .00001f)
            SetState(target < .00001f ? ApartmentCabinetState.Closed : ApartmentCabinetState.Open, "");
    }
    bool PoseClear(BoxCollider c, Vector3 delta, Vector3 head, bool protectHead, out string reason)
    {
        Vector3 position = c.transform.position + delta;
        Quaternion rotation = c.transform.rotation;
        Vector3 half = Vector3.Scale(c.size, Abs(c.transform.lossyScale)) * .5f;
        Vector3 center = c.transform.TransformPoint(c.center) + delta;
        Vector3 h = Quaternion.Inverse(rotation) * (head - center);
        Vector3 closest = new Vector3(Mathf.Clamp(h.x,-half.x,half.x),Mathf.Clamp(h.y,-half.y,half.y),Mathf.Clamp(h.z,-half.z,half.z));
        if (protectHead && (h - closest).sqrMagnitude < .12f * .12f) { reason = "Tracked head near sliding door."; return false; }
        int count = Physics.OverlapBoxNonAlloc(center, half + Vector3.one * .0015f, neighbours, rotation, ~0, QueryTriggerInteraction.Ignore);
        if (count == neighbours.Length) { reason = "Collision query capacity exceeded."; return false; }
        for (int i=0;i<count;i++)
        {
            var other = neighbours[i];
            if (other == null || other.transform.IsChildOf(movingLeaf) || other.gameObject.scene != gameObject.scene) continue;
            if (Physics.ComputePenetration(c,position,rotation,other,other.transform.position,other.transform.rotation,out _,out float depth) && depth > .0002f)
            { reason = "Sliding door blocked by " + other.name + " depth=" + depth.ToString("F6"); return false; }
        }
        reason = ""; return true;
    }
    void Commit()
    {
        if (movingLeaf == null) return;
        movingLeaf.localPosition = closedLocalPosition + localTravel.normalized * CurrentTravel;
        if (body != null) body.position = movingLeaf.position;
        Physics.SyncTransforms();
    }
    void SetState(ApartmentCabinetState state, string reason)
    {
        bool changed = State != state || BlockedReason != reason; State = state; BlockedReason = reason;
        if (changed) StateChanged?.Invoke(state,reason);
    }
    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
}
