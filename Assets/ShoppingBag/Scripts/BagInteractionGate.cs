using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Acquisition only. Never disable a selector/interactor or suppress its queued Unselect.
    public sealed class BagInteractionGate : MonoBehaviour
    {
        [SerializeField] private ShoppingBag bag;
        [SerializeField, Tooltip("Optional C2 apartment adapter implementing IBagInteractionContext.")]
        private MonoBehaviour contextProvider;
        private IBagInteractionContext context;
        private bool contextBound;
        private IBagGripInputSource input = new BagRuntimeInputSource();
        private IController leftController, rightController;
        private readonly BagGripRearmState rearm = new BagGripRearmState();
        private BagInteractionBlockers manual;
        private int lastFrame = -1;
        public BagInteractionBlockers LastBlockers { get; private set; }

        private void OnEnable()
        {
            if (bag == null) bag = GetComponent<ShoppingBag>();
            if (!ReferenceEquals(contextProvider, null)) BindContext(contextProvider as IBagInteractionContext);
            rearm.BlockAll(Time.frameCount);
            Apply();
        }
        private void OnDisable()
        {
            rearm.BlockAll(lastFrame < 0 ? Time.frameCount : lastFrame);
            if (bag != null) bag.SetAcquisitionAllowed(false);
        }
        private void Update() { Observe(null); }
        public void BindContext(IBagInteractionContext provider)
        {
            context = provider; contextBound = true;
            rearm.BlockAll(lastFrame < 0 ? Time.frameCount : lastFrame);
            Apply();
        }
        public void SetInputSource(IBagGripInputSource source)
        {
            input = source ?? throw new ArgumentNullException(nameof(source));
            rearm.BlockAll(lastFrame < 0 ? Time.frameCount : lastFrame);
        }
        public void SetMenuOpen(bool value) => Set(BagInteractionBlockers.Menu, value);
        public void SetTutorialOpen(bool value) => Set(BagInteractionBlockers.Tutorial, value);
        public void SetTeleportAiming(bool value) => Set(BagInteractionBlockers.Teleport, value);
        public void SetTextEditing(bool value) => Set(BagInteractionBlockers.TextEditing, value);
        public void SetTrackingValid(bool value) => Set(BagInteractionBlockers.Tracking, !value);
        public void SetInputFocused(bool value) => Set(BagInteractionBlockers.InputFocus, !value);
        private void OnApplicationFocus(bool value) => Set(BagInteractionBlockers.ApplicationFocus, !value);
        private void OnApplicationPause(bool value) => Set(BagInteractionBlockers.Paused, value);
        private void Set(BagInteractionBlockers flag, bool value)
        {
            manual = value ? manual | flag : manual & ~flag;
            Apply();
        }
        private BagInteractionBlockers SemanticBlockers()
        {
            var result = manual;
            if (!isActiveAndEnabled) result |= BagInteractionBlockers.Disabled;
            if (contextBound)
            {
                try
                {
                    if (context == null || (context is UnityEngine.Object obj && obj == null) ||
                        (context is Behaviour behaviour && !behaviour.isActiveAndEnabled))
                        result |= BagInteractionBlockers.ContextUnavailable;
                    else result |= context.Blockers;
                }
                catch (Exception) { result |= BagInteractionBlockers.ContextUnavailable; }
            }
            return result;
        }
        private void Apply()
        {
            var blockers = SemanticBlockers();
            if (blockers != BagInteractionBlockers.None)
                rearm.BlockAll(lastFrame < 0 ? Time.frameCount : lastFrame);
            // Keep the existing Editor mouse simulator independent of hardware readiness.
            // Actual SDK acquisition additionally MUST pass CanAcquire, below.
            if (bag != null) bag.SetAcquisitionAllowed(blockers == BagInteractionBlockers.None);
        }
        private BagGripInputSnapshot Observe(IController candidate)
        {
            BagGripInputSnapshot sample;
            try { sample = input.Read(); }
            catch (Exception) { sample = new BagGripInputSnapshot { Frame = Time.frameCount, Blockers = BagInteractionBlockers.InputUnavailable }; }
            var semantic = SemanticBlockers();
            if (bag != null) bag.SetAcquisitionAllowed(semantic == BagInteractionBlockers.None);
            sample.Blockers |= semantic;
            try
            {
                if (candidate != null)
                {
                    if (candidate.Handedness == Handedness.Left && !ReferenceEquals(leftController, candidate))
                    { leftController = candidate; rearm.BlockHand(Handedness.Left, sample.Frame - 1); }
                    if (candidate.Handedness == Handedness.Right && !ReferenceEquals(rightController, candidate))
                    { rightController = candidate; rearm.BlockHand(Handedness.Right, sample.Frame - 1); }
                }
                // Continue observing the SDK snapshot away from the bag, not just during a query.
                // Both raw OVR and the registered SDK controller must agree that Grip is released.
                MergeController(ref sample, leftController, Handedness.Left);
                MergeController(ref sample, rightController, Handedness.Right);
            }
            catch (Exception) { sample.Blockers |= BagInteractionBlockers.InputUnavailable; }
            lastFrame = sample.Frame; LastBlockers = sample.Blockers;
            rearm.Observe(sample);
            return sample;
        }
        private static void MergeController(ref BagGripInputSnapshot sample, IController controller, Handedness hand)
        {
            if (ReferenceEquals(controller, null)) return;
            bool valid = !(controller is UnityEngine.Object obj && obj == null)
                && controller.Handedness == hand && controller.IsConnected && controller.IsPoseValid;
            bool grip = valid && controller.IsButtonUsageAnyActive(ControllerButtonUsage.GripButton);
            if (hand == Handedness.Left) { sample.LeftTracked &= valid; sample.LeftGrip |= grip; }
            else
            {
                sample.RightTracked &= valid; sample.RightGrip |= grip;
                if (valid && controller.IsButtonUsageAnyActive(ControllerButtonUsage.SecondaryButton)) sample.Blockers |= BagInteractionBlockers.MenuIntent;
                if (valid && controller.IsButtonUsageAnyActive(ControllerButtonUsage.PrimaryButton)) sample.Blockers |= BagInteractionBlockers.TeleportIntent;
            }
        }
        public bool CanAcquire(IController candidate)
        {
            if (candidate == null) return false;
            var sample = Observe(candidate); // No per-frame cache: B may become visible after an earlier query.
            return sample.Blockers == BagInteractionBlockers.None && rearm.CanAcquire(candidate.Handedness, sample.Frame);
        }
    }
}
