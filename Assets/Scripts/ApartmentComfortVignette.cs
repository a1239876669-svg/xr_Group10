using UnityEngine;

// Drives the Meta OVRVignette from real locomotion motion, so the darkening only appears while the
// view is actually flowing. Teleport never darkens, and because any open menu panel blocks world
// input the vignette fades out by itself while the UI is in front of the user.
//
// The driver owns no comfort state of its own: strengthPercent is written by ApartmentMenuController
// from the persisted profile, and the current speed/turn rates are read from ApartmentVRLocomotion.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10500)]
public sealed class ApartmentComfortVignette : MonoBehaviour
{
    public ApartmentVRLocomotion locomotion;
    public OVRVignette vignette;
    [Range(0f, 80f)] public float strengthPercent = 40f;
    [Tooltip("Width of the soft edge in degrees.")]
    public float falloffDegrees = 24f;
    [Tooltip("Seconds to darken once real motion starts.")]
    public float fadeInSeconds = .15f;
    [Tooltip("Seconds to clear again after motion stops.")]
    public float fadeOutSeconds = .35f;
    [Tooltip("Clear field of view at maximum strength and maximum motion.")]
    public float minimumClearFov = 45f;
    [Tooltip("Clear field of view that draws nothing; also the resting value.")]
    public float disabledClearFov = 120f;

    /// <summary>Currently applied clear field of view. The inspector value is only ever a request.</summary>
    public float ClearFov { get; private set; }

    /// <summary>
    /// Pure mapping from strength and observed motion to the clear field of view, so the comfort
    /// behaviour can be asserted without a tracked headset.
    /// </summary>
    public static float TargetClearFov(float strengthPercent, float motion, float ceiling, float minimumClearFov)
    {
        float span = Mathf.Clamp01(strengthPercent / ApartmentComfortProfile.VignetteMax) * Mathf.Clamp01(motion);
        return Mathf.Lerp(Mathf.Max(minimumClearFov + 5f, ceiling), minimumClearFov, span);
    }

    public bool HasEffect => isActiveAndEnabled && vignette != null && locomotion != null && strengthPercent > .5f;

    void OnEnable() { ClearFov = disabledClearFov; }
    void OnDisable() { Apply(disabledClearFov, false); }

    void LateUpdate()
    {
        if (vignette == null) return;
        bool active = HasEffect;
        if (vignette.enabled != active) vignette.enabled = active;
        if (!active) { Apply(disabledClearFov, false); return; }
        float ceiling = Mathf.Max(minimumClearFov + 5f, disabledClearFov);
        float motion = Motion();
        float target = TargetClearFov(strengthPercent, motion, ceiling, minimumClearFov);
        float span = Mathf.Max(1f, ceiling - minimumClearFov);
        // Darkening appears quickly because it is the comfort cue; clearing is gentler so the edge is
        // never a sudden flash.
        float seconds = target < ClearFov ? fadeInSeconds : fadeOutSeconds;
        Apply(ClearFov <= 0f ? ceiling : Mathf.MoveTowards(ClearFov, target, span / Mathf.Max(.01f, seconds) * Time.unscaledDeltaTime), true);
    }

    // 0 while a panel is open, while desktop debug drives the camera, or before tracking is ready.
    float Motion()
    {
        if (locomotion == null || locomotion.debugMode) return 0f;
        var menu = locomotion.menu;
        if (menu != null && menu.isActiveAndEnabled && menu.HasOpenPanel) return 0f;
        float speed = locomotion.moveSpeed > .001f ? Mathf.Abs(locomotion.CurrentPlanarSpeed) / locomotion.moveSpeed : 0f;
        float turn = locomotion.smoothTurnSpeed > .001f ? Mathf.Abs(locomotion.CurrentTurnRate) / locomotion.smoothTurnSpeed : 0f;
        return Mathf.Clamp01(Mathf.Max(speed, turn));
    }

    void Apply(float clearFov, bool draw)
    {
        ClearFov = clearFov;
        if (vignette == null) return;
        vignette.VignetteFieldOfView = clearFov;
        if (!draw) return;
        vignette.VignetteFalloffDegrees = falloffDegrees;
        vignette.VignetteColor = Color.black;
    }
}
