using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace QuestGuidance.Locomotion
{
    /// <summary>
    /// Swings a door open when a hand or controller comes within reach of it, and lets it
    /// fall shut again once they leave.
    ///
    /// This exists because the Interaction SDK's controller grab path cannot drive a door.
    /// GrabInteractable is discovered through a CollisionInteractionRegistry, so an
    /// interactable only becomes a candidate when its colliders physically touch the
    /// interactor's — and the stock ControllerGrabInteractor ships with no collider at all.
    /// Grip on a controller can therefore never register anything. Rather than patch the
    /// SDK rig's prefab internals, the door watches the tracking anchors directly, which
    /// works identically for hands and controllers.
    ///
    /// Grabbing still takes priority: while a hand holds the door this stands down and
    /// simply follows whatever angle the grab left it at.
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorAutoSwing : MonoBehaviour
    {
        [Header("Wiring (resolved automatically when left empty)")]
        [SerializeField]
        [Tooltip("Read for the swing direction, so the auto swing matches the grab range.")]
        private OneGrabRotateTransformer rotator;

        [SerializeField]
        [Tooltip("While this reports a held grab point, the auto swing stays out of the way.")]
        private Grabbable grabbable;

        [SerializeField]
        [Tooltip("Source of the hand and controller anchors that trigger the door.")]
        private OVRCameraRigRef rigRef;

        [Header("Swing")]
        [SerializeField]
        [Tooltip("Degrees per second while opening.")]
        private float openSpeed = 150f;

        [SerializeField]
        [Tooltip("Degrees per second while easing shut.")]
        private float closeSpeed = 70f;

        [SerializeField]
        [Tooltip("Seconds to stay open after the last hand or controller leaves.")]
        private float holdOpenFor = 1.2f;

        [SerializeField]
        [Tooltip("Extra margin around the door's closed bounds that counts as 'touching'.")]
        private float reachMargin = 0.1f;

        private Bounds _triggerBox;
        private float _angle;
        private float _lastTouched;

        private void Awake()
        {
            if (rotator == null) rotator = GetComponent<OneGrabRotateTransformer>();
            if (grabbable == null) grabbable = GetComponent<Grabbable>();
            if (rigRef == null) rigRef = GetComponentInParent<OVRCameraRigRef>();
            if (rigRef == null) rigRef = FindAnyObjectByType<OVRCameraRigRef>();

            // A fixed box around the door's closed position. The door's own bounds rotate
            // as it swings, which would make the trigger chase itself.
            var leaf = transform.childCount > 0 ? transform.GetChild(0) : null;
            var rend = leaf != null ? leaf.GetComponent<Renderer>() : GetComponent<Renderer>();
            _triggerBox = rend != null
                ? rend.bounds
                : new Bounds(transform.position, new Vector3(1f, 2.2f, 1f));
            _triggerBox.Expand(reachMargin * 2f);
        }

        private float OpenAngle
        {
            get
            {
                if (rotator == null) return 95f;

                // Match whichever direction the grab constraints allow.
                var c = rotator.Constraints;
                if (c.MaxAngle != null && c.MaxAngle.Constrain && c.MaxAngle.Value > 0f)
                {
                    return c.MaxAngle.Value;
                }
                if (c.MinAngle != null && c.MinAngle.Constrain && c.MinAngle.Value < 0f)
                {
                    return c.MinAngle.Value;
                }
                return 95f;
            }
        }

        private void Update()
        {
            bool grabbing = grabbable != null && grabbable.SelectingPointsCount > 0;

            if (grabbing || Touched())
            {
                _lastTouched = Time.time;
            }

            if (grabbing)
            {
                // Hand over control: track what the grab is doing instead of driving it.
                _angle = Normalise(transform.localEulerAngles.y);
                return;
            }

            bool open = Time.time - _lastTouched < holdOpenFor;
            float target = open ? OpenAngle : 0f;
            float speed = open ? openSpeed : closeSpeed;

            _angle = Mathf.MoveTowards(_angle, target, speed * Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }

        private bool Touched()
        {
            if (rigRef == null || rigRef.CameraRig == null) return false;

            var rig = rigRef.CameraRig;
            return Inside(rig.leftHandAnchor)
                || Inside(rig.rightHandAnchor)
                || Inside(rig.leftControllerAnchor)
                || Inside(rig.rightControllerAnchor);
        }

        private bool Inside(Transform anchor)
        {
            return anchor != null && _triggerBox.Contains(anchor.position);
        }

        private static float Normalise(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            return degrees;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.9f, 0.25f, 0.35f);
            var b = Application.isPlaying ? _triggerBox : ComputeEditorBox();
            Gizmos.DrawWireCube(b.center, b.size);
        }

        private Bounds ComputeEditorBox()
        {
            var leaf = transform.childCount > 0 ? transform.GetChild(0) : null;
            var rend = leaf != null ? leaf.GetComponent<Renderer>() : GetComponent<Renderer>();
            var b = rend != null ? rend.bounds : new Bounds(transform.position, new Vector3(1f, 2.2f, 1f));
            b.Expand(reachMargin * 2f);
            return b;
        }
    }
}
