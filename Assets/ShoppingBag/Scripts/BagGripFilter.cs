using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Only the existing controller near-grab path is accepted. No input binding is changed.
    public sealed class BagGripFilter : MonoBehaviour, IGameObjectFilter
    {
        [SerializeField] private ShoppingBag bag;

        public bool Filter(GameObject candidate)
        {
            if (!isActiveAndEnabled || bag == null || !bag.isActiveAndEnabled || candidate == null) return false;
            var interactor = candidate.GetComponent<GrabInteractor>();
            if (interactor == null) return false;
            if (bag.IsHeld) return bag.HeldInteractor == interactor;
            if (!bag.AcquisitionAllowed) return false;
            var controller = ControllerFor(interactor);
            return controller != null && controller.IsConnected && controller.IsPoseValid;
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
