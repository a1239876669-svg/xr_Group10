using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    public sealed class BagHapticCooldown
    {
        private float left = float.NegativeInfinity, right = float.NegativeInfinity;
        public bool TryStart(Handedness hand, float now, float cooldown)
        {
            if ((hand != Handedness.Left && hand != Handedness.Right) || float.IsNaN(now) || float.IsInfinity(now)) return false;
            float last = hand == Handedness.Left ? left : right;
            if (now - last < Mathf.Max(0, cooldown)) return false;
            if (hand == Handedness.Left) left = now; else right = now;
            return true;
        }
    }

    public readonly struct BagHapticRequest
    {
        public readonly Handedness Hand;
        public readonly float Amplitude, Duration;
        public readonly bool HardwareCommandIssued;
        public BagHapticRequest(Handedness hand, float amplitude, float duration, bool issued)
        { Hand=hand; Amplitude=amplitude; Duration=duration; HardwareCommandIssued=issued; }
    }

    [DisallowMultipleComponent]
    public sealed class BagHaptics : MonoBehaviour
    {
        [SerializeField] private BagContactSensor sensor;
        [SerializeField, Range(0, 1)] private float amplitude = .35f;
        [SerializeField, Range(0, 1)] private float frequency = .5f;
        [SerializeField, Range(.01f, .5f)] private float duration = .08f;
        [SerializeField, Min(0)] private float cooldown = .25f;
        [Tooltip("Uncheck explicitly for Quest Link hardware testing. Desktop simulation never sends hardware commands.")]
        [SerializeField] private bool dryRunInEditor = true;
        [SerializeField] private bool logRequests;
        private readonly BagHapticCooldown limiter = new BagHapticCooldown();
        private Handedness pulseHand;
        private float stopAt;
        private bool hardwareStarted;
        public bool IsPulsing { get; private set; }
        public event Action<BagHapticRequest> Requested;
        private void OnEnable()
        {
            if (sensor == null) sensor = GetComponentInChildren<BagContactSensor>(true);
            if (sensor != null) { sensor.ContactStarted -= OnContact; sensor.ContactsCleared -= StopPulse; sensor.ContactStarted += OnContact; sensor.ContactsCleared += StopPulse; }
        }
        private void OnDisable()
        {
            if (sensor != null) { sensor.ContactStarted -= OnContact; sensor.ContactsCleared -= StopPulse; }
            StopPulse();
        }
        private void OnDestroy() => StopPulse();
        private void OnApplicationFocus(bool focused) { if (!focused) StopPulse(); }
        private void OnApplicationPause(bool paused) { if (paused) StopPulse(); }
        private void OnContact(BagContactTarget target)
        {
            if (target == null || sensor == null || !sensor.IsContactActive || !sensor.Bag.HeldHand.HasValue) return;
            var hand = sensor.Bag.HeldHand.Value;
            if (!limiter.TryStart(hand, Time.unscaledTime, cooldown)) return;
            StopPulse(); pulseHand = hand;
            float strength = Mathf.Clamp01(amplitude), seconds = Mathf.Clamp(duration, .01f, .5f);
            if (strength <= 0) return;
            hardwareStarted = false;
            bool dry = Application.isEditor && dryRunInEditor;
            if (!dry && sensor.Bag.IsSdkHeld && Application.isFocused
                && sensor.Bag.HeldController != null && sensor.Bag.HeldController.IsConnected
                && OVRManager.loadedXRDevice == OVRManager.XRDevice.Oculus && OVRManager.hasInputFocus && OVRManager.hasVrFocus)
            {
                OVRInput.SetControllerVibration(Mathf.Clamp01(frequency), strength, Controller(hand)); hardwareStarted = true;
            }
            IsPulsing = true; stopAt = Time.unscaledTime + seconds;
            var request = new BagHapticRequest(hand, strength, seconds, hardwareStarted);
            Requested?.Invoke(request);
            if (logRequests) Debug.Log($"Bag haptic request: {hand}, {strength:F2}, {seconds:F2}s, hardware command={hardwareStarted}", this);
        }
        private static OVRInput.Controller Controller(Handedness hand) => hand == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        private void Update()
        {
            if (IsPulsing && (Time.unscaledTime >= stopAt || sensor == null || !sensor.IsContactActive
                || sensor.Bag.HeldHand != pulseHand)) StopPulse();
        }
        public void StopPulse()
        {
            if (!IsPulsing && !hardwareStarted) return;
            if (hardwareStarted) OVRInput.SetControllerVibration(0, 0, Controller(pulseHand));
            bool issued = hardwareStarted; hardwareStarted = false; IsPulsing = false;
            Requested?.Invoke(new BagHapticRequest(pulseHand, 0, 0, issued));
        }
    }
}
