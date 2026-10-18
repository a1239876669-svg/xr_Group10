using UnityEngine;

/// <summary>
/// Two-phase motion ramp with independent start and stop smoothing.
///
/// <para><b>Start</b> (<c>smoothTime</c>): a value leaving rest is eased up to its target over that many
/// seconds. The ease ends the moment the target is reached, so later speed changes while already moving
/// are immediate.</para>
///
/// <para><b>Stop</b> (<c>stopSmoothTime</c>): when the target drops to zero -- the stick was released or
/// recentred -- the value brakes linearly to zero over that many seconds. Zero means the motion stops in
/// the same frame and nothing is travelled afterwards.</para>
///
/// <para>A partial pull-back (still pushing, just less) or a reversal stays immediate: those are speed
/// changes the player asked for, not stops. Any blocked/hard-stop state also collapses to zero at once.</para>
///
/// Holding this as a struct field is deliberate: <c>Step</c> mutates the struct in place, so no
/// allocation happens per frame.
/// </summary>
public struct ApartmentMotionRamp
{
    bool ramping;

    /// <summary>True only while a start-of-motion ramp is in progress.</summary>
    public bool IsRamping => ramping;

    public void Reset() { ramping = false; }

    /// <param name="fullMagnitude">Full speed / full turn rate; the reference for both ramp rates.</param>
    public float Step(float current, float target, float fullMagnitude, float smoothTime, float stopSmoothTime, float dt, bool hardStop)
    {
        if (float.IsNaN(current) || float.IsInfinity(current)) current = 0f;
        if (float.IsNaN(target) || float.IsInfinity(target) || hardStop) { ramping = false; return 0f; }
        float targetMagnitude = Mathf.Abs(target), currentMagnitude = Mathf.Abs(current);
        float full = Mathf.Max(.0001f, fullMagnitude);

        // Released or centred: brake to a stop, or stop dead when no stop smoothing is configured.
        if (targetMagnitude <= .0001f)
        {
            ramping = false;
            if (stopSmoothTime <= .0001f || currentMagnitude <= .0001f) return 0f;
            return Mathf.MoveTowards(current, 0f, full / stopSmoothTime * dt);
        }
        // Slowing to a lower speed, or reversing, is immediate.
        if (currentMagnitude > targetMagnitude) { ramping = false; return target; }
        // Already moving and asking for more: immediate. Only a value leaving rest is eased.
        if (!ramping)
        {
            if (currentMagnitude > .0001f) return target;
            ramping = true;
        }
        if (smoothTime <= .0001f) { ramping = false; return target; }
        float next = Mathf.MoveTowards(current, target, full / smoothTime * dt);
        // The ease ends as soon as the target is reached, so later increases are immediate again.
        if (Mathf.Abs(next) >= targetMagnitude - .0001f) { ramping = false; next = target; }
        return next;
    }
}
