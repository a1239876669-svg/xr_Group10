using System;
using UnityEngine;

// One session-independent comfort profile. Every value is clamped into the ranges that
// ApartmentVRLocomotion.ConfigurationError() accepts, so a stored or hand-edited profile can never
// put the rig into an unsupported configuration (which would disable VR input, including the menu).
[Serializable]
public sealed class ApartmentComfortProfile
{
    public const float VignetteMin = 0f, VignetteMax = 80f, VignetteStep = 10f;
    public const float SpeedMin = .4f, SpeedMax = 2.5f, SpeedStep = .1f;
    public const float SpeedSmoothMin = 0f, SpeedSmoothMax = .6f, SpeedSmoothStep = .05f;
    public const float TurnSpeedMin = 15f, TurnSpeedMax = 120f, TurnSpeedStep = 15f;
    public const float TurnSmoothMin = 0f, TurnSmoothMax = .5f, TurnSmoothStep = .05f;
    public const float MoveStopMin = 0f, MoveStopMax = .6f, MoveStopStep = .05f;
    public const float TurnStopMin = 0f, TurnStopMax = .5f, TurnStopStep = .05f;
    public const float SnapAngleMin = 30f, SnapAngleMax = 60f, SnapAngleStep = 15f;

    public float vignettePercent = 40f;
    public float moveSpeed = 1.2f;
    public float moveSmoothTime = .25f;
    public float turnSpeed = 60f;
    public float turnSmoothTime = .20f;
    // Independent stop smoothing. 0 keeps the "stops in the same frame" behaviour the comfort feature
    // was built around; a positive value brakes to a stop over that many seconds from full speed.
    public float moveStopSmoothTime;
    public float turnStopSmoothTime;
    public bool snapTurn;
    public float snapAngle = 45f;

    public static ApartmentComfortProfile Default => new ApartmentComfortProfile();

    // Slow, strongly tunnelled, with softly braked stops: the least likely to provoke discomfort.
    public static ApartmentComfortProfile Comfort => new ApartmentComfortProfile
    { vignettePercent = 60f, moveSpeed = .8f, moveSmoothTime = .35f, turnSpeed = 45f, turnSmoothTime = .30f,
      moveStopSmoothTime = .20f, turnStopSmoothTime = .15f };

    // Responsive: no tunnelling, near-instant response and dead stops.
    public static ApartmentComfortProfile Responsive => new ApartmentComfortProfile
    { vignettePercent = 0f, moveSpeed = 2f, moveSmoothTime = .10f, turnSpeed = 120f, turnSmoothTime = .05f,
      moveStopSmoothTime = 0f, turnStopSmoothTime = 0f };

    public static ApartmentComfortProfile Preset(int index) =>
        index == 0 ? Comfort : index == 2 ? Responsive : Default;

    public static string PresetName(int index) => index == 0 ? "舒适" : index == 2 ? "灵敏" : "标准";

    /// <summary>Forces every value into the supported window and onto a step boundary. Returns true when anything moved.</summary>
    public bool Clamp()
    {
        bool changed = false;
        changed |= Step(ref vignettePercent, VignetteMin, VignetteMax, VignetteStep);
        changed |= Step(ref moveSpeed, SpeedMin, SpeedMax, SpeedStep);
        changed |= Step(ref moveSmoothTime, SpeedSmoothMin, SpeedSmoothMax, SpeedSmoothStep);
        changed |= Step(ref turnSpeed, TurnSpeedMin, TurnSpeedMax, TurnSpeedStep);
        changed |= Step(ref turnSmoothTime, TurnSmoothMin, TurnSmoothMax, TurnSmoothStep);
        changed |= Step(ref moveStopSmoothTime, MoveStopMin, MoveStopMax, MoveStopStep);
        changed |= Step(ref turnStopSmoothTime, TurnStopMin, TurnStopMax, TurnStopStep);
        // Only three snap angles are supported, so round to the nearest one instead of wrapping.
        float angle = float.IsNaN(snapAngle) || float.IsInfinity(snapAngle) ? 45f :
            snapAngle <= (SnapAngleMin + SnapAngleStep * .5f) ? SnapAngleMin :
            snapAngle <= (SnapAngleMin + SnapAngleStep * 1.5f) ? SnapAngleMin + SnapAngleStep : SnapAngleMax;
        if (Mathf.Abs(angle - snapAngle) > .0001f) { snapAngle = angle; changed = true; }
        return changed;
    }

    static bool Step(ref float value, float min, float max, float step)
    {
        float original = value;
        // NaN and either infinity repair to the lower bound on purpose: for a comfort profile the low
        // end of every range is the least sickness-prone value, so a corrupt record fails safe.
        if (float.IsNaN(value) || float.IsInfinity(value)) value = min;
        value = Mathf.Clamp(value, min, max);
        value = Mathf.Clamp(min + Mathf.Round((value - min) / step) * step, min, max);
        value = Mathf.Round(value * 1000f) / 1000f;
        return Mathf.Abs(value - original) > .0001f;
    }

    public ApartmentComfortProfile Copy() => new ApartmentComfortProfile
    {
        vignettePercent = vignettePercent, moveSpeed = moveSpeed, moveSmoothTime = moveSmoothTime,
        turnSpeed = turnSpeed, turnSmoothTime = turnSmoothTime,
        moveStopSmoothTime = moveStopSmoothTime, turnStopSmoothTime = turnStopSmoothTime,
        snapTurn = snapTurn, snapAngle = snapAngle
    };
}

// PlayerPrefs is the only durable store in this project. Reading always sanitises, and a corrupt or
// out-of-range record is discarded instead of being carried into the session.
public static class ApartmentComfortStore
{
    public const string Key = "ApartmentComfort.v1";

    public static ApartmentComfortProfile Load()
    {
        if (!PlayerPrefs.HasKey(Key)) return ApartmentComfortProfile.Default;
        try
        {
            var profile = JsonUtility.FromJson<ApartmentComfortProfile>(PlayerPrefs.GetString(Key, ""));
            if (profile == null) throw new InvalidOperationException("empty record");
            profile.Clamp();
            return profile;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Comfort settings could not be restored and were reset: " + exception.Message);
            Clear();
            return ApartmentComfortProfile.Default;
        }
    }

    public static void Save(ApartmentComfortProfile profile)
    {
        if (profile == null) return;
        profile.Clamp();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }

    public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
}
