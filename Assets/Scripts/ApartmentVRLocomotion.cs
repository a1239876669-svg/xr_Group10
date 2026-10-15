using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.AI;
using Unity.AI.Navigation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// One owner for walking, head-pivot turning and validated teleportation. No automatic scene installation.
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
    public ApartmentTurnMode turnMode = ApartmentTurnMode.Smooth;
    [Range(15f, 180f)] public float smoothTurnSpeed = 60f;
    [Range(.05f, .8f)] public float turnDeadZone = .20f;
    public float snapAngle = 30f;
    public float snapThreshold = .70f;
    public float snapRearmThreshold = .25f;

    [Header("Comfort: ramp time to reach full speed / full turn rate (0 = instant, legacy behaviour)")]
    [Range(0f, 1f)] public float moveSmoothTime = .25f;
    [Range(0f, 1f)] public float turnSmoothTime = .20f;
    [Header("Comfort: brake time from full speed / full turn rate down to a stop (0 = stop in the same frame)")]
    [Range(0f, 1f)] public float moveStopSmoothTime;
    [Range(0f, 1f)] public float turnStopSmoothTime;

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

    [Header("Teleport: hold right A, release to travel")]
    public bool teleportEnabled;
    public ApartmentTeleportValidationMode teleportValidationMode;
    public ApartmentTutorialController tutorial;
    public ApartmentMenuController menu;
    public ApartmentRelocationReason LastRelocationReason { get; private set; }
    public NavMeshSurface teleportSurface;
    public LayerMask teleportFloorMask;
    [Range(1f, 8f)] public float teleportMaxDistance = 5f;

    public ApartmentTeleportState TeleportState { get; private set; }
    public string TeleportFailureReason { get; private set; } = "";
    public Vector3 TeleportTarget { get; private set; }
    public bool IsTeleportAiming => teleportButton != null && teleportButton.IsAiming;
    public event Action<Vector3> Teleported;
    public event Action<float> Walked;
    public event Action<float> Turned;
    public float LastTeleportDistance { get; private set; }
    public event Action<ApartmentTeleportState, string> TeleportStateChanged;

    [Header("Explicit desktop test; never a Quest Link test")]
    public bool debugMode;

    public bool Grounded { get; private set; }
    /// <summary>Smoothed planar speed actually applied last frame, in metres per second.</summary>
    public float CurrentPlanarSpeed => currentPlanarSpeed;
    /// <summary>Smoothed yaw rate actually applied last frame, in degrees per second.</summary>
    public float CurrentTurnRate => currentTurnRate;
    public string Status { get; private set; } = "Not running";
    CharacterController controller;
    readonly ApartmentSnapTurnLatch snap = new ApartmentSnapTurnLatch();
    ApartmentSmoothTurnInput smoothTurn;
    readonly Collider[] overlaps = new Collider[64];
    readonly RaycastHit[] casts = new RaycastHit[64];
    Camera[] rigCameras;
    AudioListener[] rigListeners;
    bool[] originalCameras, originalListeners;
    bool originalRigEnabled, appliedDebugMode, configured, inputFailed;
    float verticalVelocity, nextWarningTime;
    // Startup-only ramps driven by the comfort settings. A value leaving rest is eased up to its target;
    // any slow-down, release or hard stop is applied in the same frame, so nothing glides on after the
    // stick is released.
    ApartmentMotionRamp moveRamp, turnRamp;
    // Ramped by the comfort settings. Cleared outright whenever input is blocked, so closing a menu
    // can never leave residual motion behind.
    float currentPlanarSpeed, currentTurnRate;
    Vector3 lastMoveDirection;
    ApartmentTeleportButtonLatch teleportButton;
    NavMeshPath teleportPath;
    Vector3[] arcPoints;
    Vector3[] ringPoints;
    GameObject teleportVisuals;
    LineRenderer teleportArc, teleportRing;
    Material teleportMaterial;
    bool vrTeleportInputReady;

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
        float[] values = { moveSpeed, moveDeadZone, smoothTurnSpeed, turnDeadZone, snapAngle, snapThreshold, snapRearmThreshold,
            moveSmoothTime, turnSmoothTime, moveStopSmoothTime, turnStopSmoothTime,
            capsuleRadius, minimumHeight, maximumHeight, headClearance, skinWidth, stepOffset,
            slopeLimit, gravity, groundedVelocity, terminalVelocity, maximumFrameTime };
        foreach (float value in values)
            if (float.IsNaN(value) || float.IsInfinity(value)) return "A parameter is not finite.";
        if (moveSpeed <= 0f || moveSpeed > 3f || moveDeadZone < 0f || moveDeadZone >= 1f ||
            smoothTurnSpeed < 15f || smoothTurnSpeed > 180f || turnDeadZone < .05f || turnDeadZone > .8f ||
            (turnMode != ApartmentTurnMode.Smooth && turnMode != ApartmentTurnMode.Snap) ||
            snapAngle <= 0f || snapAngle > 90f || snapThreshold > 1f ||
            snapRearmThreshold < 0f || snapThreshold <= snapRearmThreshold ||
            moveSmoothTime < 0f || moveSmoothTime > 1f || turnSmoothTime < 0f || turnSmoothTime > 1f ||
            moveStopSmoothTime < 0f || moveStopSmoothTime > 1f || turnStopSmoothTime < 0f || turnStopSmoothTime > 1f ||
            capsuleRadius < .1f || skinWidth <= 0f || skinWidth >= capsuleRadius ||
            minimumHeight < 2f * capsuleRadius || maximumHeight < minimumHeight ||
            headClearance < 0f || stepOffset < 0f || stepOffset >= minimumHeight ||
            slopeLimit < 0f || slopeLimit > 60f || gravity >= 0f || groundedVelocity >= 0f ||
            terminalVelocity >= groundedVelocity || maximumFrameTime <= 0f || maximumFrameTime > .1f)
            return "Movement/capsule parameters are outside the supported safety ranges.";
        return null;
    }

    public string TeleportConfigurationError()
    {
        if (!teleportEnabled) return "传送未开启";
        if (teleportFloorMask.value == 0) return "请选择 TeleportFloor 地板层";
        if (float.IsNaN(teleportMaxDistance) || float.IsInfinity(teleportMaxDistance) ||
            teleportMaxDistance < 1f || teleportMaxDistance > 8f) return "传送距离必须在 1 到 8 米之间";
        if (teleportValidationMode == ApartmentTeleportValidationMode.FloorLanding) return null;
        if (teleportValidationMode != ApartmentTeleportValidationMode.NavMeshRoute) return "传送模式无效";
        if (teleportSurface == null || !teleportSurface.isActiveAndEnabled || teleportSurface.navMeshData == null)
            return "传送地图未配置或未烘焙，请运行 M2-B3 的配置菜单";
        if (teleportSurface.gameObject.scene != gameObject.scene) return "传送地图与玩家不在同一个场景";
        var settings = NavMesh.GetSettingsByID(teleportSurface.agentTypeID);
        if (settings.agentTypeID == -1 || settings.agentRadius + .001f < capsuleRadius ||
            settings.agentHeight + .001f < minimumHeight)
            return "导航体型配置无效，请重新配置 TeleportPlayer 并烘焙";
        // The map is a candidate route, not the final height check. Live capsule queries below
        // enforce the actual tracked height, including taller users, under every lintel.
        return null;
    }

    void OnEnable()
    {
        EnsureTeleportRuntimeState();
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
        smoothTurn?.Reset();
        CancelTeleport();
        Status = debugMode ? "DESKTOP DEBUG ONLY" : "Waiting for tracked HMD";
    }

    void OnDisable()
    {
        CancelTeleport();
        if (teleportVisuals != null)
        {
            if (Application.isPlaying) Destroy(teleportVisuals); else DestroyImmediate(teleportVisuals);
        }
        if (teleportMaterial != null)
        {
            if (Application.isPlaying) Destroy(teleportMaterial); else DestroyImmediate(teleportMaterial);
        }
        teleportVisuals = null;
        teleportMaterial = null;
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
        smoothTurn?.Reset();
    }

    void LateUpdate()
    {
        if (!configured || controller == null || !controller.enabled) return;
        if (ConfigurationError() != null) { Warn("Configuration changed to unsupported values; locomotion disabled."); enabled = false; return; }
        if (debugMode != appliedDebugMode) ApplyMode();
        Vector2 move = Vector2.zero, turn = Vector2.zero;
        Transform head = null;
        bool canControl = false;
        bool rightA = false, teleportTracked = false;
        vrTeleportInputReady = false;
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
                        if (OVRManager.hasVrFocus && OVRManager.hasInputFocus && Application.isFocused)
                        {
                            if (OVRInput.IsControllerConnected(OVRInput.Controller.LTouch))
                                move = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
                            if (OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
                            {
                                turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
                                canControl = true;
                                rightA = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
                                teleportTracked = OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch) &&
                                    OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch);
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
        bool movementBlocked = false, turningBlocked = false;
        if (tutorial != null && tutorial.isActiveAndEnabled)
        {
            if (!tutorial.AllowMove) { move = Vector2.zero; movementBlocked = true; }
            if (!tutorial.AllowTurn) { turn = Vector2.zero; turningBlocked = true; }
            if (!tutorial.AllowTeleport) teleportTracked = false;
        }
        if (menu != null && menu.isActiveAndEnabled)
        {
            if (menu.BlocksWorldInput) { move = turn = Vector2.zero; canControl = teleportTracked = false; movementBlocked = turningBlocked = true; }
            if (!menu.AllowsSmoothMove) { move = Vector2.zero; movementBlocked = true; }
            if (!menu.AllowsTeleport) teleportTracked = false;
        }
        vrTeleportInputReady = !debugMode && canControl && teleportTracked && poseSafe && !inputFailed;
        // A press/release is processed by the same movement owner. No cached green target is committed.
        bool teleportFrame = ProcessTeleport(vrTeleportInputReady, rightA);
        if (teleportFrame) { move = Vector2.zero; canControl = false; movementBlocked = turningBlocked = true; }
        float dt = Mathf.Min(Time.deltaTime, maximumFrameTime);
        // Only continuous (smooth) turning is ramped. Snap turning, keyboard debug turning and any
        // blocked state clear the ramp outright instead of coasting through a gate.
        float targetTurnRate = 0f;
        bool rampedTurn = false;
        if (!canControl) { snap.Reset(); smoothTurn.Reset(); }
        else if (!debugMode && turnMode == ApartmentTurnMode.Smooth && !turningBlocked)
        {
            snap.Reset();
            rampedTurn = true;
            float strength = smoothTurn.Sample(turn, turnDeadZone);
            if (Mathf.Abs(strength) > .0001f) targetTurnRate = strength * smoothTurnSpeed;
        }
        else
        {
            smoothTurn.Reset();
            if (turningBlocked) snap.Reset();
            else
            {
                int direction = snap.Sample(turn.x, turn.magnitude, snapThreshold, snapRearmThreshold);
                if (direction != 0) TryTurn(head, direction * snapAngle);
            }
        }
        currentTurnRate = turnRamp.Step(currentTurnRate, targetTurnRate, smoothTurnSpeed, turnSmoothTime, turnStopSmoothTime, dt, !rampedTurn);
        if (rampedTurn && Mathf.Abs(currentTurnRate) > .0001f) TryTurn(head, currentTurnRate * dt);
        if (!Application.isFocused) { move = Vector2.zero; movementBlocked = true; }
        Grounded = controller.isGrounded;
        if (Grounded && verticalVelocity < 0f) verticalVelocity = groundedVelocity;
        verticalVelocity = Mathf.Max(terminalVelocity, verticalVelocity + gravity * dt);
        float targetSpeed = 0f;
        if (head != null && move.magnitude > moveDeadZone && !movementBlocked)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (!Finite(forward) || forward.sqrMagnitude < .001f) forward = transform.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 axis = Vector2.ClampMagnitude(move, 1f);
            float strength = Mathf.Clamp01((axis.magnitude - moveDeadZone) / (1f - moveDeadZone));
            Vector3 heading = forward * axis.y + right * axis.x;
            if (Finite(heading) && heading.sqrMagnitude > .000001f) lastMoveDirection = heading.normalized;
            targetSpeed = moveSpeed * strength;
        }
        // The ramp is on the speed only and keeps the last heading. It eases the start of a movement and
        // ends the movement in the same frame the stick is released, so there is no coasting.
        currentPlanarSpeed = moveRamp.Step(currentPlanarSpeed, targetSpeed, moveSpeed, moveSmoothTime, moveStopSmoothTime, dt, !canControl || movementBlocked);
        Vector3 planar = lastMoveDirection * currentPlanarSpeed;
        // Walking/gravity use Move; turning uses an atomic head-pivot compensation.
        // Teleport is the only operation that relocates the tracked head to a new landing point.
        Vector3 beforeWalk = transform.position;
        CollisionFlags flags = controller.Move((planar + Vector3.up * verticalVelocity) * dt);
        if (planar.sqrMagnitude > .000001f)
            Walked?.Invoke(Vector3.ProjectOnPlane(transform.position - beforeWalk, Vector3.up).magnitude);
        Grounded = (flags & CollisionFlags.Below) != 0 || controller.isGrounded;
        if (Grounded && verticalVelocity < 0f) verticalVelocity = groundedVelocity;
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
    }

    bool ProcessTeleport(bool inputReady, bool held)
    {
        EnsureTeleportRuntimeState();
        string error = TeleportConfigurationError();
        var action = teleportButton.Sample(inputReady && error == null, held);
        if (!inputReady || error != null)
        {
            HideTeleportVisuals();
            SetTeleportState(teleportEnabled ? ApartmentTeleportState.Unavailable : ApartmentTeleportState.Idle,
                error ?? "等待头显、右手柄追踪和输入焦点");
            return action == ApartmentTeleportButtonAction.Cancel;
        }
        if (action == ApartmentTeleportButtonAction.None) return false;
        if (action == ApartmentTeleportButtonAction.Cancel) { HideTeleportVisuals(); return true; }

        bool valid = TryFindTeleportArc(out RaycastHit hit, out string reason, out int pointCount);
        if (valid) valid = ValidateTeleportDestination(hit.point, out Vector3 floor, out reason) && SetTarget(floor);
        if (action == ApartmentTeleportButtonAction.Release)
        {
            HideTeleportVisuals();
            if (valid) TryTeleportTo(TeleportTarget, out reason);
            else SetTeleportState(ApartmentTeleportState.Idle, reason);
        }
        else
        {
            DrawTeleportPreview(pointCount, hit.collider != null, hit.point, valid);
            SetTeleportState(valid ? ApartmentTeleportState.AimingValid : ApartmentTeleportState.AimingInvalid, reason);
        }
        return true;
    }

    bool SetTarget(Vector3 floor) { TeleportTarget = floor; return true; }

    public void CancelTeleport()
    {
        vrTeleportInputReady = false;
        smoothTurn?.Reset();
        snap.Reset();
        moveRamp.Reset();
        turnRamp.Reset();
        if (teleportButton != null) teleportButton.Reset();
        HideTeleportVisuals();
        SetTeleportState(ApartmentTeleportState.Idle, "");
    }

    void OnApplicationFocus(bool focused) { if (!focused) CancelTeleport(); }
    void OnApplicationPause(bool paused) { if (paused) CancelTeleport(); }

    /// <summary>
    /// Applies one comfort profile through a single clamped entry point. Returns false and rolls back
    /// to the defaults only when this assignment is what introduced a configuration error, so a
    /// pre-existing unrelated error is never silently turned into "settings were reset".
    /// </summary>
    public bool ApplyComfort(ApartmentComfortProfile profile)
    {
        if (profile == null) return false;
        string before = ConfigurationError();
        profile.Clamp();
        Assign(profile);
        if (before == null && ConfigurationError() != null)
        {
            UnityEngine.Debug.LogWarning("Comfort values were rejected by the movement configuration check; defaults restored.", this);
            Assign(ApartmentComfortProfile.Default);
            return false;
        }
        return true;
    }

    void Assign(ApartmentComfortProfile profile)
    {
        moveSpeed = profile.moveSpeed;
        moveSmoothTime = profile.moveSmoothTime;
        moveStopSmoothTime = profile.moveStopSmoothTime;
        smoothTurnSpeed = profile.turnSpeed;
        turnSmoothTime = profile.turnSmoothTime;
        turnStopSmoothTime = profile.turnStopSmoothTime;
        turnMode = profile.snapTurn ? ApartmentTurnMode.Snap : ApartmentTurnMode.Smooth;
        snapAngle = profile.snapAngle;
        currentPlanarSpeed = 0f;
        currentTurnRate = 0f;
        moveRamp.Reset();
        turnRamp.Reset();
        snap.Reset();
        smoothTurn?.Reset();
    }

    /// <summary>Current live values as a profile. Used by the editor checks, never by gameplay.</summary>
    public ApartmentComfortProfile CaptureComfort()
    {
        var profile = new ApartmentComfortProfile
        {
            moveSpeed = moveSpeed, moveSmoothTime = moveSmoothTime, moveStopSmoothTime = moveStopSmoothTime,
            turnSpeed = smoothTurnSpeed, turnSmoothTime = turnSmoothTime, turnStopSmoothTime = turnStopSmoothTime,
            snapTurn = turnMode == ApartmentTurnMode.Snap, snapAngle = snapAngle
        };
        return profile;
    }

    void SetTeleportState(ApartmentTeleportState state, string reason)
    {
        reason = reason ?? "";
        bool changed = TeleportState != state || TeleportFailureReason != reason;
        TeleportState = state;
        TeleportFailureReason = reason;
        if (changed) TeleportStateChanged?.Invoke(state, reason);
    }

    bool TryFindTeleportArc(out RaycastHit hit, out string reason, out int count)
    {
        hit = default;
        reason = "请指向公寓地板";
        count = 0;
        Transform origin = rig.rightControllerAnchor;
        if (origin == null || !Finite(origin.position) || !Finite(origin.forward)) return false;
        Vector3 start = origin.position, previous = start, velocity = origin.forward * 6f;
        arcPoints[count++] = previous;
        float travelled = 0f;
        for (int i = 1; i < arcPoints.Length; i++)
        {
            float time = i * .04f;
            Vector3 next = start + velocity * time + Vector3.down * (4.905f * time * time);
            Vector3 delta = next - previous;
            float length = delta.magnitude;
            if (travelled + length > teleportMaxDistance)
            {
                length = teleportMaxDistance - travelled;
                next = previous + delta.normalized * length;
            }
            if (length <= .0001f) break;
            if (FirstSceneHit(new Ray(previous, delta.normalized), length, out hit, out bool saturated))
            {
                arcPoints[count++] = hit.point;
                if (saturated) { reason = "碰撞查询过多，无法安全判断落点"; return false; }
                if ((teleportFloorMask.value & (1 << hit.collider.gameObject.layer)) == 0)
                { reason = "墙壁、门和家具不能作为传送落点"; return false; }
                if (Vector3.Angle(hit.normal, Vector3.up) > slopeLimit)
                { reason = "地面坡度过大"; return false; }
                return true;
            }
            arcPoints[count++] = next;
            travelled += length;
            previous = next;
            if (travelled >= teleportMaxDistance - .0001f) break;
        }
        return false;
    }

    // Query every solid layer, then classify the nearest external hit. Never raycast only the floor layer.
    bool FirstSceneHit(Ray ray, float distance, out RaycastHit hit, out bool saturated)
    {
        int count = Physics.RaycastNonAlloc(ray, casts, distance, ~0, QueryTriggerInteraction.Collide);
        hit = default;
        saturated = count == casts.Length;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
            if (ApartmentDoorAimSurface.IsAimSolid(casts[i].collider) && !casts[i].collider.transform.IsChildOf(transform) && casts[i].distance < closest)
            { closest = casts[i].distance; hit = casts[i]; }
        return saturated || hit.collider != null;
    }

    // Also callable by the read-only Editor checks; it never changes player position.
    public bool ValidateTeleportDestination(Vector3 requested, out Vector3 floorPoint, out string reason)
    {
        EnsureTeleportRuntimeState();
        floorPoint = requested;
        reason = TeleportConfigurationError();
        if (reason != null) return false;
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null || rig == null || rig.centerEyeAnchor == null || !Finite(requested) ||
            !Finite(rig.centerEyeAnchor.position) || !Finite(transform.position))
        { reason = "玩家引用或落点无效"; return false; }
        Vector3 feet = new Vector3(rig.centerEyeAnchor.position.x, transform.position.y, rig.centerEyeAnchor.position.z);
        if (Vector3.Distance(feet, requested) > teleportMaxDistance + .001f)
        { reason = "落点超出传送距离"; return false; }
        if (!FirstSceneHit(new Ray(requested + Vector3.up * .15f, Vector3.down), .35f, out RaycastHit ground, out bool saturated) ||
            saturated || ground.collider == null || (teleportFloorMask.value & (1 << ground.collider.gameObject.layer)) == 0 ||
            Vector3.Angle(ground.normal, Vector3.up) > slopeLimit || Mathf.Abs(ground.point.y - requested.y) > .12f)
        { reason = "落点不是可站立的地板"; return false; }
        floorPoint = ground.point;
        if (teleportValidationMode == ApartmentTeleportValidationMode.FloorLanding)
            return LandingBodyClear(floorPoint, out reason);
        var filter = new NavMeshQueryFilter { agentTypeID = teleportSurface.agentTypeID,
            areaMask = 1 << NavMesh.GetAreaFromName("Walkable") };
        filter.SetAreaCost(NavMesh.GetAreaFromName("Walkable"), 1f);
        if (!NavMesh.SamplePosition(floorPoint, out NavMeshHit target, .12f, filter) ||
            Vector3.ProjectOnPlane(target.position - floorPoint, Vector3.up).magnitude > .06f ||
            Mathf.Abs(target.position.y - floorPoint.y) > .12f)
        { reason = "落点太靠近墙、家具或地板边缘"; return false; }
        if (!NavMesh.SamplePosition(feet, out NavMeshHit from, .25f, filter) ||
            !NavMesh.CalculatePath(from.position, target.position, filter, teleportPath) ||
            teleportPath.status != NavMeshPathStatus.PathComplete)
        { reason = "落点与当前位置的可行走区域不连通"; return false; }
        // Runtime floor support catches gaps/removed floors even if the bake is stale.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * .25f;
            Vector3 rim = floorPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * capsuleRadius;
            if (!FirstSceneHit(new Ray(rim + Vector3.up * .10f, Vector3.down), .25f, out RaycastHit support, out saturated) ||
                saturated || support.collider == null || (teleportFloorMask.value & (1 << support.collider.gameObject.layer)) == 0 ||
                Mathf.Abs(support.point.y - floorPoint.y) > .08f)
            { reason = "身体脚下缺少完整地板支撑"; return false; }
        }
        Vector3 headLocal = transform.InverseTransformPoint(rig.centerEyeAnchor.position);
        float height = controller.height;
        Vector3 desiredRoot = ApartmentTeleportMath.RootAtLanding(transform.position, rig.centerEyeAnchor.position, floorPoint);
        Vector3 proposedCenter = desiredRoot + transform.rotation * new Vector3(headLocal.x, height * .5f, headLocal.z);
        Capsule(proposedCenter, height, out Vector3 bottom, out Vector3 top, out float radius);
        if (Blocked(bottom, top, radius)) { reason = "落点没有足够空间容纳玩家身体"; return false; }
        // Teleport is not a straight-line body dash. Follow the complete NavMesh walking route;
        // check each segment against current physics so closed doors/stale bakes still block it.
        Vector3 previous = feet;
        foreach (Vector3 corner in teleportPath.corners)
        {
            if (!RouteSegmentClear(previous, corner, height))
            { reason = "通路被关闭的门、家具或低门框挡住"; return false; }
            previous = corner;
        }
        if (!RouteSegmentClear(previous, floorPoint, height))
        { reason = "落点入口被障碍物挡住"; return false; }
        reason = "";
        return true;
    }

    bool LandingBodyClear(Vector3 floor, out string reason)
    {
        Vector3 localHead = transform.InverseTransformPoint(rig.centerEyeAnchor.position);
        Vector3 root = ApartmentTeleportMath.RootAtLanding(transform.position, rig.centerEyeAnchor.position, floor);
        Vector3 center = root + transform.rotation * new Vector3(localHead.x, controller.height * .5f, localHead.z);
        Capsule(center, controller.height, out Vector3 bottom, out Vector3 top, out float radius);
        bool clear = !Blocked(bottom, top, radius);
        reason = clear ? "" : "落点没有足够空间容纳玩家身体";
        return clear;
    }

    bool RouteSegmentClear(Vector3 fromFeet, Vector3 toFeet, float height)
    {
        Capsule(fromFeet + Vector3.up * (height * .5f), height, out Vector3 bottom, out Vector3 top, out float radius);
        Vector3 delta = toFeet - fromFeet;
        return !Blocked(bottom, top, radius) && !Blocked(bottom + delta, top + delta, radius) &&
            !SweepBlocked(bottom, top, radius, delta);
    }

    public bool TryTeleportTo(Vector3 requested, out string failureReason)
    {
        failureReason = "需要有效的 Quest 头显、右手柄追踪和输入焦点";
        if (!configured || !isActiveAndEnabled || debugMode || !vrTeleportInputReady || !Application.isFocused ||
            controller == null || !controller.enabled || ConfigurationError() != null) return false;
        if (tutorial != null && tutorial.isActiveAndEnabled && !tutorial.AllowTeleport) return false;
        if (menu != null && menu.isActiveAndEnabled && (menu.BlocksWorldInput || !menu.AllowsTeleport)) return false;
        try
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!rig.isActiveAndEnabled || !manager.isActiveAndEnabled || OVRManager.instance != manager ||
                !XRSettings.isDeviceActive || !OVRManager.isHmdPresent || !OVRManager.hasVrFocus || !OVRManager.hasInputFocus ||
                manager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel || !device.isValid ||
                !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) || !tracked ||
                !OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) ||
                !OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch) ||
                !OVRInput.GetControllerOrientationTracked(OVRInput.Controller.RTouch))
            { CancelTeleport(); return false; }
        }
        catch (Exception) { CancelTeleport(); return false; }
        // This method is the only teleport commit path and always revalidates immediately before relocation.
        Physics.SyncTransforms();
        if (teleportValidationMode == ApartmentTeleportValidationMode.FloorLanding &&
            (!TryFindTeleportArc(out RaycastHit currentHit, out failureReason, out _) ||
             Vector3.Distance(currentHit.point, requested) > .05f))
        { SetTeleportState(ApartmentTeleportState.Idle, "请重新瞄准有效地面"); return false; }
        if (!UpdateCapsule(rig.centerEyeAnchor))
        {
            failureReason = "当前头部或身体位置不安全，请先回到可站立位置";
            SetTeleportState(ApartmentTeleportState.Idle, failureReason);
            return false;
        }
        if (!ValidateTeleportDestination(requested, out Vector3 floor, out failureReason))
        { SetTeleportState(ApartmentTeleportState.Idle, failureReason); return false; }
        CommitRelocation(floor, ApartmentRelocationReason.AimedTeleport);
        return true;
    }

    // Recovery intentionally does not require a clear *current* capsule, an A aim ray, or a walkable route.
    // It still requires tracked input and validates the destination immediately before committing.
    public bool TrySafetyReset(Vector3 requested, out string reason)
    {
        reason = "等待头显、右手柄追踪和输入焦点";
        if (!Application.isPlaying || !configured || !isActiveAndEnabled || debugMode ||
            controller == null || !controller.enabled || ConfigurationError() != null || menu == null ||
            menu.interactor == null || !menu.interactor.RuntimeReady ||
            manager.trackingOriginType != OVRManager.TrackingOrigin.FloorLevel) return false;
        Physics.SyncTransforms();
        if (!ValidateSafetyDestination(requested, out Vector3 floor, out reason)) return false;
        Vector3 local = transform.InverseTransformPoint(rig.centerEyeAnchor.position);
        float height = Mathf.Clamp(local.y + headClearance, minimumHeight, maximumHeight);
        controller.height = height;
        controller.center = new Vector3(local.x, height * .5f, local.z);
        CommitRelocation(floor, ApartmentRelocationReason.SafetyReset);
        return true;
    }

    public bool ValidateSafetyDestination(Vector3 requested, out Vector3 floor, out string reason)
    {
        floor = requested;
        reason = "安全点配置无效";
        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null || rig == null || rig.centerEyeAnchor == null || teleportFloorMask.value == 0 ||
            !Finite(requested) || !Finite(rig.centerEyeAnchor.position) || !Finite(transform.position)) return false;
        Vector3 local = transform.InverseTransformPoint(rig.centerEyeAnchor.position);
        if (!Finite(local) || local.y < .4f || local.y > 3f) { reason = "头显高度无效，请恢复正常追踪"; return false; }
        if (!FirstSceneHit(new Ray(requested + Vector3.up * .15f, Vector3.down), .35f, out var ground, out bool full) ||
            full || ground.collider == null || (teleportFloorMask.value & (1 << ground.collider.gameObject.layer)) == 0 ||
            Vector3.Angle(ground.normal, Vector3.up) > slopeLimit || Mathf.Abs(ground.point.y-requested.y) > .12f)
        { reason = "安全点下方没有可站立的地板"; return false; }
        floor = ground.point;
        float height = Mathf.Clamp(local.y + headClearance, minimumHeight, maximumHeight);
        Capsule(floor + Vector3.up * (height * .5f), height, out var bottom, out var top, out float radius);
        if (Blocked(bottom, top, radius)) { reason = "安全点被物体占用，请稍后重试"; return false; }
        reason = ""; return true;
    }

    void CommitRelocation(Vector3 floor, ApartmentRelocationReason kind)
    {
        CancelTeleport();
        Vector3 destination = ApartmentTeleportMath.RootAtLanding(transform.position, rig.centerEyeAnchor.position, floor);
        LastTeleportDistance = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).magnitude;
        controller.enabled = false;
        try { transform.position = destination; }
        finally { controller.enabled = true; }
        Physics.SyncTransforms();
        verticalVelocity = groundedVelocity;
        Grounded = true;
        snap.Reset();
        smoothTurn?.Reset();
        TeleportTarget = floor;
        LastRelocationReason = kind;
        SetTeleportState(ApartmentTeleportState.Teleported, "");
        Teleported?.Invoke(floor);
    }

    void DrawTeleportPreview(int count, bool hasHit, Vector3 hit, bool valid)
    {
        if (teleportVisuals == null)
        {
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) { Warn("Teleport preview shader Unlit/Color is missing."); return; }
            teleportMaterial = new Material(shader) { name = "Teleport preview (runtime only)", hideFlags = HideFlags.DontSave };
            teleportVisuals = new GameObject("TeleportPreview (runtime only)") { hideFlags = HideFlags.DontSave };
            teleportVisuals.transform.SetParent(transform, false);
            teleportArc = CreateTeleportLine("AimArc", .012f);
            teleportRing = CreateTeleportLine("LandingRing", .018f);
        }
        teleportVisuals.SetActive(true);
        teleportMaterial.color = valid ? new Color(.1f, 1f, .3f) : new Color(1f, .15f, .1f);
        teleportArc.positionCount = count;
        for (int i = 0; i < count; i++) teleportArc.SetPosition(i, arcPoints[i]);
        teleportRing.enabled = hasHit;
        if (!hasHit) return;
        Vector3 center = (valid ? TeleportTarget : hit) + Vector3.up * .015f;
        for (int i = 0; i < ringPoints.Length; i++)
        {
            float angle = i * Mathf.PI * 2f / (ringPoints.Length - 1);
            ringPoints[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * capsuleRadius;
        }
        teleportRing.positionCount = ringPoints.Length;
        teleportRing.SetPositions(ringPoints);
    }

    LineRenderer CreateTeleportLine(string name, float width)
    {
        var item = new GameObject(name) { hideFlags = HideFlags.DontSave };
        item.transform.SetParent(teleportVisuals.transform, false);
        var line = item.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = teleportMaterial;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    void HideTeleportVisuals() { if (teleportVisuals != null) teleportVisuals.SetActive(false); }

    void EnsureTeleportRuntimeState()
    {
        // The project's hot reload can retain an existing component without running new field initializers.
        if (teleportButton == null) teleportButton = new ApartmentTeleportButtonLatch();
        if (smoothTurn == null) smoothTurn = new ApartmentSmoothTurnInput();
        if (teleportPath == null) teleportPath = new NavMeshPath();
        if (arcPoints == null) arcPoints = new Vector3[65];
        if (ringPoints == null) ringPoints = new Vector3[33];
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

    bool TryTurn(Transform head, float degrees)
    {
        Vector3 oldHead = head.position;
        Quaternion rotation = Quaternion.AngleAxis(degrees, Vector3.up);
        Vector3 destination = ApartmentTurnMath.RootAroundHead(transform.position, oldHead, degrees);
        Quaternion facing = rotation * transform.rotation;
        Vector3 finalCenter = destination + facing * controller.center;
        Capsule(transform.TransformPoint(controller.center), controller.height, out Vector3 bottom, out Vector3 top, out float radius);
        Vector3 shift = finalCenter - transform.TransformPoint(controller.center);
        if (Blocked(bottom + shift, top + shift, radius) || SweepBlocked(bottom, top, radius, shift))
        { Warn("Turn refused: tracked body is obstructed. Return to clear space."); return false; }
        // Apply yaw and its root compensation together. The vertical capsule is symmetric;
        // its world centre stays fixed, without a temporary sideways sweep into a door frame.
        controller.enabled = false;
        try { transform.SetPositionAndRotation(destination, facing); }
        finally { controller.enabled = true; }
        Physics.SyncTransforms();
        Vector3 drift = head.position - oldHead;
        drift.y = 0f;
        if (drift.magnitude > .01f)
            Warn("Head-pivot turn drift exceeded 1 cm; stop and inspect.");
        Turned?.Invoke(Mathf.Abs(degrees));
        return true;
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

public enum ApartmentTeleportValidationMode { NavMeshRoute, FloorLanding }
public enum ApartmentTurnMode { Smooth, Snap }

// Require a neutral stick after startup/focus/teleport before resuming continuous yaw.
public sealed class ApartmentSmoothTurnInput
{
    bool armed;
    public void Reset() { armed = false; }
    public float Sample(Vector2 axis, float deadZone)
    {
        if (float.IsNaN(axis.x) || float.IsInfinity(axis.x) || float.IsNaN(axis.y) || float.IsInfinity(axis.y))
        { Reset(); return 0f; }
        if (axis.magnitude <= deadZone) { armed = true; return 0f; }
        if (!armed || Mathf.Abs(axis.x) <= deadZone) return 0f;
        return Mathf.Sign(axis.x) * Mathf.Clamp01((Mathf.Abs(axis.x) - deadZone) / (1f - deadZone));
    }
}

public static class ApartmentTurnMath
{
    public static Vector3 RootAroundHead(Vector3 root, Vector3 head, float degrees)
    {
        Vector3 offset = Vector3.ProjectOnPlane(head - root, Vector3.up);
        return root + offset - Quaternion.AngleAxis(degrees, Vector3.up) * offset;
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

public enum ApartmentTeleportState { Idle, Unavailable, AimingInvalid, AimingValid, Teleported }
public enum ApartmentTeleportButtonAction { None, Begin, Hold, Release, Cancel }

// Pure state machine. Focus/tracking loss disarms until A has been seen released again.
public sealed class ApartmentTeleportButtonLatch
{
    bool armed;
    public bool IsAiming { get; private set; }
    public void Reset() { armed = false; IsAiming = false; }
    public ApartmentTeleportButtonAction Sample(bool ready, bool held)
    {
        if (!ready)
        {
            bool cancelled = IsAiming;
            Reset();
            return cancelled ? ApartmentTeleportButtonAction.Cancel : ApartmentTeleportButtonAction.None;
        }
        if (!armed) { if (!held) armed = true; return ApartmentTeleportButtonAction.None; }
        if (!IsAiming)
        {
            if (!held) return ApartmentTeleportButtonAction.None;
            IsAiming = true;
            return ApartmentTeleportButtonAction.Begin;
        }
        if (held) return ApartmentTeleportButtonAction.Hold;
        IsAiming = false;
        return ApartmentTeleportButtonAction.Release;
    }
}

public static class ApartmentTeleportMath
{
    // Move the floor-origin root so the tracked head's ground projection lands on the selected point.
    // Head height and root rotation are deliberately not modified.
    public static Vector3 RootAtLanding(Vector3 root, Vector3 head, Vector3 floor) =>
        new Vector3(floor.x - (head.x - root.x), floor.y, floor.z - (head.z - root.z));
}
