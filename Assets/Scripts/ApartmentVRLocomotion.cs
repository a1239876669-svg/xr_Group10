using System;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// No editor callbacks, automatic installation, teleportation, or secondary mover.
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(10000)]
public sealed class ApartmentVRLocomotion : MonoBehaviour
{
    [Header("Assigned by the M2-B2 installer")]
    public OVRCameraRig rig;
    public OVRManager manager;
    public Camera desktopCamera;

    [Header("VR input")]
    public float moveSpeed = 1.2f;
    public float moveDeadZone = .15f;
    public float snapAngle = 30f;
    public float snapThreshold = .70f;
    public float snapRearmThreshold = .25f;

    [Header("FloorLevel capsule (metres)")]
    public float capsuleRadius = .30f;
    public float minimumHeight = .90f;
    public float maximumHeight = 2.10f;
    public float headClearance = .15f;
    public float skinWidth = .03f;
    public float stepOffset = .20f;
    public float slopeLimit = 45f;
    public float gravity = -9.81f;
    public float groundedVelocity = -1f;
    public float terminalVelocity = -20f;
    public float maximumFrameTime = .05f;

    [Header("Explicit desktop test; never a Quest Link test")]
    public bool debugMode;

    public bool Grounded { get; private set; }
    public string Status { get; private set; } = "Not running";
    CharacterController controller;
    readonly ApartmentSnapTurnLatch snap = new ApartmentSnapTurnLatch();
    readonly Collider[] overlaps = new Collider[64];
    readonly RaycastHit[] casts = new RaycastHit[64];
    Camera[] rigCameras;
    AudioListener[] rigListeners;
    bool[] originalCameras, originalListeners;
    bool originalRigEnabled, appliedDebugMode, configured, inputFailed;
    float verticalVelocity, nextWarningTime;

    public string ConfigurationError()
    {
        if (rig == null || manager == null || desktopCamera == null)
            return "Rig, manager and desktop camera references must be assigned.";
        if (rig.transform.parent != transform || manager.gameObject != rig.gameObject ||
            desktopCamera.transform.parent != transform)
            return "Rig/manager/debug camera hierarchy does not match VRPlayer.";
        if (rig.transform.localPosition.sqrMagnitude > .000001f ||
            Quaternion.Angle(rig.transform.localRotation, Quaternion.identity) > .01f ||
            Vector3.Distance(rig.transform.localScale, Vector3.one) > .001f || GetComponent<Rigidbody>() != null)
            return "Rig must have identity local transform; VRPlayer cannot also have a Rigidbody mover.";
        if (transform.parent != null || Vector3.Distance(transform.lossyScale, Vector3.one) > .001f ||
            Vector3.Angle(transform.up, Vector3.up) > .01f)
            return "VRPlayer must be an upright, unscaled scene root.";
        float[] values = { moveSpeed, moveDeadZone, snapAngle, snapThreshold, snapRearmThreshold,
            capsuleRadius, minimumHeight, maximumHeight, headClearance, skinWidth, stepOffset,
            slopeLimit, gravity, groundedVelocity, terminalVelocity, maximumFrameTime };
        foreach (float value in values)
            if (float.IsNaN(value) || float.IsInfinity(value)) return "A parameter is not finite.";
        if (moveSpeed <= 0f || moveSpeed > 3f || moveDeadZone < 0f || moveDeadZone >= 1f ||
            snapAngle <= 0f || snapAngle > 90f || snapThreshold > 1f ||
            snapRearmThreshold < 0f || snapThreshold <= snapRearmThreshold ||
            capsuleRadius < .1f || skinWidth <= 0f || skinWidth >= capsuleRadius ||
            minimumHeight < 2f * capsuleRadius || maximumHeight < minimumHeight ||
            headClearance < 0f || stepOffset < 0f || stepOffset >= minimumHeight ||
            slopeLimit < 0f || slopeLimit > 60f || gravity >= 0f || groundedVelocity >= 0f ||
            terminalVelocity >= groundedVelocity || maximumFrameTime <= 0f || maximumFrameTime > .1f)
            return "Movement/capsule parameters are outside the supported safety ranges.";
        return null;
    }

    void OnEnable()
    {
        controller = GetComponent<CharacterController>();
        string error = ConfigurationError();
        if (error == null && (controller == null || !controller.enabled ||
            GetComponents<CharacterController>().Length != 1)) error = "Exactly one enabled CharacterController is required.";
        if (error == null)
            foreach (MonoBehaviour behaviour in GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour != this &&
                    (behaviour is ApartmentVRLocomotion || behaviour.GetType().Name == "JoystickLocomotion" ||
                     behaviour.GetType().Name == "OVRPlayerController" || behaviour.transform == transform))
                { error = "Another player controller/script is present: " + behaviour.GetType().Name; break; }
        if (error != null)
        {
            Debug.LogError("M2-B2 stopped: " + error, this);
            enabled = false;
            return;
        }
        controller.radius = capsuleRadius;
        controller.skinWidth = skinWidth;
        controller.stepOffset = stepOffset;
        controller.slopeLimit = slopeLimit;
        controller.minMoveDistance = 0f;
        rigCameras = rig.GetComponentsInChildren<Camera>(true);
        rigListeners = rig.GetComponentsInChildren<AudioListener>(true);
        originalCameras = new bool[rigCameras.Length];
        originalListeners = new bool[rigListeners.Length];
        for (int i = 0; i < rigCameras.Length; i++) originalCameras[i] = rigCameras[i].enabled;
        for (int i = 0; i < rigListeners.Length; i++) originalListeners[i] = rigListeners[i].enabled;
        originalRigEnabled = rig.enabled;
        configured = true;
        inputFailed = false;
        verticalVelocity = 0f;
        ApplyMode();
    }

    void ApplyMode()
    {
        appliedDebugMode = debugMode;
        // The SDK manager remains enabled; this script never changes XR initialization/settings.
        rig.enabled = debugMode ? false : originalRigEnabled;
        for (int i = 0; i < rigCameras.Length; i++)
            if (rigCameras[i] != null) rigCameras[i].enabled = !debugMode && originalCameras[i];
        for (int i = 0; i < rigListeners.Length; i++)
            if (rigListeners[i] != null) rigListeners[i].enabled = !debugMode && originalListeners[i];
        desktopCamera.gameObject.SetActive(debugMode);
        desktopCamera.enabled = debugMode;
        var audio = desktopCamera.GetComponent<AudioListener>();
        if (audio != null) audio.enabled = debugMode;
        snap.Reset();
        Status = debugMode ? "DESKTOP DEBUG ONLY" : "Waiting for tracked HMD";
    }

    void OnDisable()
    {
        if (!configured) return;
        if (rig != null) rig.enabled = originalRigEnabled;
        for (int i = 0; i < rigCameras.Length; i++)
            if (rigCameras[i] != null) rigCameras[i].enabled = originalCameras[i];
        for (int i = 0; i < rigListeners.Length; i++)
            if (rigListeners[i] != null) rigListeners[i].enabled = originalListeners[i];
        if (desktopCamera != null)
        {
            desktopCamera.enabled = false;
            var audio = desktopCamera.GetComponent<AudioListener>();
            if (audio != null) audio.enabled = false;
            desktopCamera.gameObject.SetActive(false);
        }
        configured = false;
        snap.Reset();
    }

    void LateUpdate()
    {
        if (!configured || controller == null || !controller.enabled) return;
        if (ConfigurationError() != null) { Warn("Configuration changed to unsupported values; locomotion disabled."); enabled = false; return; }
        if (debugMode != appliedDebugMode) ApplyMode();
        Vector2 move = Vector2.zero, turn = Vector2.zero;
        Transform head = null;
        bool canControl = false;
        try
        {
            if (!inputFailed && debugMode)
            {
                head = desktopCamera.transform;
#if ENABLE_INPUT_SYSTEM
                var keys = Keyboard.current;
                if (keys != null && Application.isFocused)
                {
                    move = new Vector2((keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                        (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
                    bool left = keys.qKey.isPressed, right = keys.eKey.isPressed;
                    turn = new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), left && right ? 1f : 0f);
                    canControl = true;
                }
#else
                Warn("Desktop debug requires the installed Input System to be enabled; no legacy input fallback.");
#endif
            }
            else if (!debugMode && !inputFailed && rig.isActiveAndEnabled && manager.isActiveAndEnabled &&
                     XRSettings.isDeviceActive && OVRManager.instance == manager)
            {
                var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) &&
                    tracked && OVRManager.isHmdPresent)
                {
                    if (manager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel)
                    {
                        Warn("Runtime tracking origin is not FloorLevel; input paused. No XR setting is changed automatically.");
                    }
                    else
                    {
                        head = rig.centerEyeAnchor;
                        if (OVRManager.hasVrFocus && Application.isFocused)
                        {
                            if (OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
                                move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
                            if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
                            {
                                turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
                                canControl = true;
                            }
                            else snap.Reset();
                        }
                        Status = "Tracked VR; hardware acceptance tests still required";
                    }
                }
                else Status = "Waiting for tracked HMD";
            }
        }
        catch (Exception exception)
        {
            inputFailed = true;
            canControl = false;
            move = Vector2.zero;
            head = null;
            snap.Reset();
            Debug.LogWarning("M2-B2 input suspended after an SDK/input exception; re-enable component to retry. " + exception.Message, this);
        }

        if (!Finite(new Vector3(move.x, move.y, 0f)) || !Finite(new Vector3(turn.x, turn.y, 0f)))
        {
            move = turn = Vector2.zero;
            canControl = false;
            Warn("Non-finite input rejected; no horizontal movement or snap turn executed.");
        }
        bool poseSafe = head != null && UpdateCapsule(head);
        if (!poseSafe) { move = Vector2.zero; canControl = false; }
        if (!canControl) snap.Reset();
        else
        {
            int direction = snap.Sample(turn.x, turn.magnitude, snapThreshold, snapRearmThreshold);
            if (direction != 0) TrySnapTurn(head, direction * snapAngle);
        }
        if (!Application.isFocused) move = Vector2.zero;
        float dt = Mathf.Min(Time.deltaTime, maximumFrameTime);
        Grounded = controller.isGrounded;
        if (Grounded && verticalVelocity < 0f) verticalVelocity = groundedVelocity;
        verticalVelocity = Mathf.Max(terminalVelocity, verticalVelocity + gravity * dt);
        Vector3 planar = Vector3.zero;
        if (head != null && move.magnitude > moveDeadZone)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (!Finite(forward) || forward.sqrMagnitude < .001f) forward = transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 axis = Vector2.ClampMagnitude(move, 1f);
            float strength = Mathf.Clamp01((axis.magnitude - moveDeadZone) / (1f - moveDeadZone));
            planar = (forward * axis.y + right * axis.x).normalized * (moveSpeed * strength);
        }
        // Always executed, including missing HMD / idle input. No Transform translation anywhere.
        CollisionFlags flags = controller.Move((planar + Vector3.up * verticalVelocity) * dt);
        Grounded = (flags & CollisionFlags.Below) != 0 || controller.isGrounded;
        if (Grounded && verticalVelocity < 0f) verticalVelocity = groundedVelocity;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
    }

    bool UpdateCapsule(Transform head)
    {
        Vector3 local = transform.InverseTransformPoint(head.position);
        if (!Finite(local) || local.y < .40f || local.y > 3f) return false;
        float height = Mathf.Clamp(local.y + headClearance, minimumHeight, maximumHeight);
        Vector3 center = new Vector3(local.x, height * .5f, local.z);
        Capsule(transform.TransformPoint(center), height, out Vector3 bottom, out Vector3 top, out float radius);
        if (Blocked(bottom, top, radius))
        {
            Warn("DESIGN LIMIT: tracked capsule adjustment is obstructed; stick movement/turning paused. Physical head tracking is not constrained by CharacterController.");
            return false;
        }
        // Also sweep horizontal room-scale adjustment; reject jumping the body across a wall.
        Vector3 shift = transform.TransformVector(new Vector3(center.x - controller.center.x, 0f, center.z - controller.center.z));
        Capsule(transform.TransformPoint(controller.center), controller.height, out Vector3 oldBottom, out Vector3 oldTop, out float oldRadius);
        if (shift.sqrMagnitude > .000001f && SweepBlocked(oldBottom, oldTop, oldRadius, shift))
        {
            Warn("DESIGN LIMIT: room-scale capsule sweep blocked; return to the previous clear position.");
            return false;
        }
        if (Mathf.Abs(controller.height - height) > .002f) controller.height = height;
        if (Vector3.Distance(controller.center, center) > .001f) controller.center = center;
        return true;
    }

    void TrySnapTurn(Transform head, float degrees)
    {
        Vector3 oldHead = head.position;
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.up);
        Vector3 shift = rotation * (oldHead - transform.position) - (oldHead - transform.position);
        shift.y = 0f;
        Capsule(transform.TransformPoint(controller.center), controller.height, out Vector3 bottom, out Vector3 top, out float radius);
        // Rotation first displaces an off-centre capsule. Check that entire temporary path before rotating.
        if (Blocked(bottom + shift, top + shift, radius) || SweepBlocked(bottom, top, radius, shift))
        { Warn("DESIGN LIMIT: snap pivot compensation is obstructed; turn refused. Return stick to centre before retrying."); return; }
        transform.rotation = rotation * transform.rotation;
        controller.Move(-shift);
        Vector3 drift = head.position - oldHead;
        drift.y = 0f;
        if (drift.magnitude > .01f)
            Warn("Snap pivot correction was limited by collision (horizontal drift " + drift.magnitude.ToString("F3") + " m). Stop and inspect; no forced position correction.");
    }

    void Capsule(Vector3 center, float height, out Vector3 bottom, out Vector3 top, out float radius)
    {
        float segment = Mathf.Max(0f, height * .5f - controller.radius);
        bottom = center - Vector3.up * segment;
        top = center + Vector3.up * segment;
        radius = Mathf.Max(.01f, controller.radius - controller.skinWidth);
    }

    bool Blocked(Vector3 bottom, Vector3 top, float radius)
    {
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) return true;
        for (int i = 0; i < count; i++)
            if (overlaps[i] != null && !overlaps[i].transform.IsChildOf(transform)) return true;
        return false;
    }

    bool SweepBlocked(Vector3 bottom, Vector3 top, float radius, Vector3 delta)
    {
        float distance = delta.magnitude;
        if (distance <= .001f) return false;
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, delta / distance, casts,
            distance, ~0, QueryTriggerInteraction.Ignore);
        if (count == casts.Length) return true;
        for (int i = 0; i < count; i++)
            if (casts[i].collider != null && !casts[i].collider.transform.IsChildOf(transform)) return true;
        return false;
    }

    static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

    void Warn(string message)
    {
        Status = message;
        if (Time.unscaledTime < nextWarningTime) return;
        nextWarningTime = Time.unscaledTime + 5f;
        Debug.LogWarning("M2-B2: " + message, this);
    }
}

// Pure managed state machine: starts disarmed and requires full-stick recentering.
public sealed class ApartmentSnapTurnLatch
{
    bool armed;
    public void Reset() { armed = false; }
    public int Sample(float horizontal, float magnitude, float trigger, float rearm)
    {
        if (float.IsNaN(horizontal) || float.IsNaN(magnitude) || float.IsInfinity(horizontal) || float.IsInfinity(magnitude))
        { Reset(); return 0; }
        if (magnitude <= rearm) { armed = true; return 0; }
        if (!armed || Math.Abs(horizontal) < trigger) return 0;
        armed = false;
        return horizontal > 0f ? 1 : -1;
    }
}
