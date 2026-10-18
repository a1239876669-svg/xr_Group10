using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Only the existing controller near-grab path is accepted. No input binding is changed.
    public sealed class BagGripFilter : MonoBehaviour, IGameObjectFilter
    {
        [SerializeField] private ShoppingBag bag;
        private BagInteractionGate gate;

        public bool Filter(GameObject candidate)
        {
            if (!isActiveAndEnabled || bag == null || !bag.isActiveAndEnabled || candidate == null) return false;
            var interactor = candidate.GetComponent<GrabInteractor>();
            if (interactor == null) return false;
            // Held owner remains valid even when acquisition is blocked. SDK Unselect is untouched.
            if (bag.IsHeld) return bag.HeldInteractor == interactor;
            var controller = ControllerFor(interactor);
            if (gate == null) gate = bag.GetComponent<BagInteractionGate>();
            // Missing/disabled gate fails closed; no fallback to the old unguarded selection path.
            if (gate == null || !gate.CanAcquire(controller)) return false;
            return bag.AcquisitionAllowed;
        }

        public static IController ControllerFor(GrabInteractor interactor)
        {
            if (interactor == null) return null;
            // The retained rig has one GripButtonSelector below each controller GrabInteractor.
            // Reject other selector configurations rather than silently accepting Trigger grabs.
            var selectors = interactor.GetComponentsInChildren<ControllerSelector>(true);
            if (selectors.Length != 1 || selectors[0].ControllerButtonUsage != ControllerButtonUsage.GripButton)
                return null;
            return selectors[0].Controller;
        }
    }
}
