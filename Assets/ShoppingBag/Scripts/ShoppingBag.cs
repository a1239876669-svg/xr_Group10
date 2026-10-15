using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    [DisallowMultipleComponent]
    public sealed class ShoppingBag : MonoBehaviour
    {
        [Header("Size profiles (body dimensions in metres)")]
        [SerializeField] private BagSizeProfile small;
        [SerializeField] private BagSizeProfile medium;
        [SerializeField] private BagSizeProfile large;
        [SerializeField] private BagSize initialSize = BagSize.Medium;
        [Header("Existing Meta SDK near-grab components")]
        [SerializeField] private Grabbable grabbable;
        [SerializeField] private GrabInteractable grabInteractable;
        [SerializeField] private Rigidbody body;
        [Header("Geometry; root must remain unscaled")]
        [SerializeField] private Transform bodyVisual;
        [SerializeField] private Transform leftHandle;
        [SerializeField] private Transform rightHandle;
        [SerializeField] private Transform topHandle;
        [SerializeField] private Transform grabPoint;
        [SerializeField] private BoxCollider bodyCollider;
        [SerializeField] private BoxCollider handleTrigger;

        public BagSize CurrentSize { get; private set; }
        // SDK's selecting-point list is initialized in Awake; Inspector/EditMode and an earlier
        // component Awake must also be able to query this safely.
        public bool IsSdkHeld => grabbable != null && grabbable.GrabPoints != null && grabbable.GrabPoints.Count > 0;
        public bool IsHeld => IsSdkHeld || DesktopSimulationActive;
        public bool DesktopSimulationActive { get; private set; }
        public bool AcquisitionAllowed { get; private set; } = true;
        public GrabInteractor HeldInteractor { get; private set; }
        public IController HeldController => BagGripFilter.ControllerFor(HeldInteractor);
        public Handedness? HeldHand => DesktopSimulationActive ? desktopHand : HeldController?.Handedness;
        public Vector3 BodySize => Profile(CurrentSize)?.BodySize ?? Vector3.zero;
        public Transform GrabPoint => grabPoint;
        public BoxCollider BodyCollider => bodyCollider;
        public event Action<BagSize> SizeChanged;
        // State is read from this component; no dependency on menus, furniture or haptics.
        public event Action<ShoppingBag> HoldStateChanged;
        private int ownerId = -1;
        private bool disabledGrabForCleanup;
        private Handedness desktopHand;
        private bool savedKinematic, savedGravity;

        private void Awake()
        {
            if (!HasWiring()) { Debug.LogError("ShoppingBag: prefab references are incomplete.", this); enabled = false; return; }
            grabbable.MaxGrabPoints = 1;
            grabInteractable.MaxSelectingInteractors = 1;
            // Size is applied before the SDK's Start caches collider references.
            if (!TrySetSize(initialSize))
            { Debug.LogError("ShoppingBag: invalid size profile or scaled root. Check prefab configuration.", this); enabled = false; }
        }

        private void OnEnable()
        {
            if (grabbable != null) grabbable.WhenPointerEventRaised += HandlePointer;
            if (disabledGrabForCleanup && grabInteractable != null)
            { disabledGrabForCleanup = false; grabInteractable.Enable(); }
        }

        private void OnDisable()
        {
            EndDesktopSimulation();
            if (grabbable != null) grabbable.WhenPointerEventRaised -= HandlePointer;
            // Never leave an invisible/non-solid dropped bag after this component is disabled.
            if (grabInteractable != null && IsSdkHeld)
            { disabledGrabForCleanup = true; grabInteractable.Disable(); }
            ClearOwner();
        }

        public BagSizeProfile Profile(BagSize size)
        {
            switch (size)
            {
                case BagSize.Small: return small;
                case BagSize.Medium: return medium;
                case BagSize.Large: return large;
                default: return null;
            }
        }

        // Future BagInteractionGate can block new selections without breaking release of a held bag.
        public void SetAcquisitionAllowed(bool allowed) => AcquisitionAllowed = allowed;

        // Explicit Editor-only simulation: never selects an SDK interactor or reads VR buttons.
        public bool TryBeginDesktopSimulation(Handedness hand)
        {
#if UNITY_EDITOR
            if (!isActiveAndEnabled || IsHeld || !AcquisitionAllowed || !HasWiring()
                || (hand != Handedness.Left && hand != Handedness.Right)) return false;
            savedKinematic = body.isKinematic; savedGravity = body.useGravity;
            body.isKinematic = true; body.useGravity = false;
            desktopHand = hand; DesktopSimulationActive = true;
            bodyCollider.enabled = false;
            HoldStateChanged?.Invoke(this);
            return true;
#else
            return false;
#endif
        }

        public void EndDesktopSimulation()
        {
            if (!DesktopSimulationActive) return;
            DesktopSimulationActive = false;
            if (body != null) { body.isKinematic = savedKinematic; body.useGravity = savedGravity; }
            if (bodyCollider != null) bodyCollider.enabled = true;
            HoldStateChanged?.Invoke(this);
        }

        public bool TrySetSize(BagSize size)
        {
            var profile = Profile(size);
            if (IsHeld || !HasWiring() || profile == null || !profile.IsValid
                || Vector3.Distance(transform.lossyScale, Vector3.one) > .0001f) return false;
            Vector3 dimensions = profile.BodySize;
            float width = dimensions.x * .55f;
            float thickness = profile.HandleThickness;
            float handleHeight = profile.HandleHeight;
            // Root is on the bottom face: all profiles rest on the same floor height.
            SetPart(bodyVisual, new Vector3(0, dimensions.y * .5f, 0), dimensions);
            SetPart(leftHandle, new Vector3(-width * .5f, dimensions.y + handleHeight * .5f, 0),
                new Vector3(thickness, handleHeight, thickness));
            SetPart(rightHandle, new Vector3(width * .5f, dimensions.y + handleHeight * .5f, 0),
                new Vector3(thickness, handleHeight, thickness));
            Vector3 grip = new Vector3(0, dimensions.y + handleHeight, 0);
            SetPart(topHandle, grip, new Vector3(width + thickness, thickness, thickness));
            grabPoint.localPosition = grip;
            bodyCollider.center = new Vector3(0, dimensions.y * .5f, 0);
            bodyCollider.size = dimensions;
            handleTrigger.center = grip;
            handleTrigger.size = new Vector3(width + thickness, thickness * 2f, thickness * 2f);
            body.ResetCenterOfMass();
            body.ResetInertiaTensor();
            CurrentSize = size;
            if (Application.isPlaying && !body.isKinematic) body.WakeUp();
            SizeChanged?.Invoke(size);
            return true;
        }

        private static void SetPart(Transform part, Vector3 position, Vector3 scale)
        { part.localPosition = position; part.localRotation = Quaternion.identity; part.localScale = scale; }

        private bool HasWiring() => small != null && medium != null && large != null
            && grabbable != null && grabInteractable != null && body != null && bodyVisual != null
            && leftHandle != null && rightHandle != null && topHandle != null && grabPoint != null
            && bodyCollider != null && handleTrigger != null;

        private void HandlePointer(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select)
            {
                if (ownerId != -1) return;
                ownerId = evt.Identifier;
                foreach (var interactor in grabInteractable.Interactors)
                    if (interactor.Identifier == ownerId) { HeldInteractor = interactor; break; }
                // While held, only the handle trigger participates in physics. The future
                // contact sensor should use an independent trigger with the full body shape.
                bodyCollider.enabled = false;
                HoldStateChanged?.Invoke(this);
            }
            else if ((evt.Type == PointerEventType.Unselect || evt.Type == PointerEventType.Cancel)
                && evt.Identifier == ownerId) ClearOwner();
        }

        private void ClearOwner()
        {
            bool hadOwner = ownerId != -1;
            ownerId = -1; HeldInteractor = null;
            if (bodyCollider != null) bodyCollider.enabled = true;
            if (hadOwner) HoldStateChanged?.Invoke(this);
        }
    }
}
