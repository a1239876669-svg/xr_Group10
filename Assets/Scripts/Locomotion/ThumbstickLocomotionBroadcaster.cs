using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace QuestGuidance.Locomotion
{
    /// <summary>
    /// Drives smooth free-walk from the left thumbstick.
    ///
    /// The Interaction SDK ships broadcasters for teleport, turning and hand gestures, but
    /// none that turns an analog stick into movement, so FirstPersonLocomotor has nothing to
    /// consume for smooth walking out of the box. This fills that gap: it reads the stick,
    /// converts it into a head-relative world-space velocity and hands it straight to the
    /// Locomotor. Collision, gravity, slopes and steps all stay with the Locomotor's own
    /// CharacterController.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThumbstickLocomotionBroadcaster : MonoBehaviour, ILocomotionEventBroadcaster
    {
        [Header("Wiring")]
        [SerializeField]
        [Tooltip("Locomotor that receives the movement. Resolved from the rig when left empty.")]
        private FirstPersonLocomotor locomotor;

        [SerializeField]
        [Tooltip("Head transform movement is made relative to. Resolved from the rig when left empty.")]
        private Transform head;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Which controller's thumbstick drives movement.")]
        private OVRInput.Controller hand = OVRInput.Controller.LTouch;

        [SerializeField]
        [Tooltip("Walking speed in metres per second at full stick deflection.")]
        private float moveSpeed = 1.6f;

        [SerializeField]
        [Tooltip("Stick deflection below which no input is sent.")]
        private float deadZone = 0.15f;

        private UniqueIdentifier _identifier;

        public int Identifier => _identifier.ID;

        public event Action<LocomotionEvent> WhenLocomotionPerformed = delegate { };

        /// <summary>
        /// FirstPersonLocomotor converges on a steady state of
        /// <c>v = emitted * acceleration * speedFactor / groundDamping</c> — the per-frame
        /// delta time cancels out, so that holds at any frame rate. Inverting it here keeps
        /// <see cref="moveSpeed"/> honest in metres per second, and keeps it correct if the
        /// Locomotor's own tuning is ever changed.
        /// </summary>
        private float EmittedMagnitude
        {
            get
            {
                float gain = locomotor.Acceleration * locomotor.SpeedFactor
                             / Mathf.Max(1e-4f, locomotor.GroundDamping);
                return moveSpeed / Mathf.Max(1e-4f, gain);
            }
        }

        private void Awake()
        {
            _identifier = UniqueIdentifier.Generate(Context.Global.GetInstance(), this);
        }

        private void Start()
        {
            if (locomotor == null)
            {
                locomotor = GetComponentInParent<FirstPersonLocomotor>();
            }

            if (locomotor == null)
            {
                locomotor = FindAnyObjectByType<FirstPersonLocomotor>();
            }

            if (head == null && locomotor != null)
            {
                head = ResolveHead();
            }

            if (locomotor == null || head == null)
            {
                Debug.LogError($"{nameof(ThumbstickLocomotionBroadcaster)}: could not resolve " +
                               "the locomotor or head transform; smooth locomotion is disabled.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// Prefers the rig's own centre-eye anchor over Camera.main, so the head reference
        /// stays correct regardless of which camera happens to carry the MainCamera tag.
        /// </summary>
        private Transform ResolveHead()
        {
            var rigRef = GetComponentInParent<OVRCameraRigRef>();
            if (rigRef == null)
            {
                rigRef = FindAnyObjectByType<OVRCameraRigRef>();
            }

            if (rigRef != null && rigRef.CameraRig != null && rigRef.CameraRig.centerEyeAnchor != null)
            {
                return rigRef.CameraRig.centerEyeAnchor;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private void Update()
        {
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, hand);

            // Emit zero rather than going silent, so releasing the stick damps to a stop
            // through the Locomotor instead of coasting on stale velocity.
            Vector3 velocity = Vector3.zero;

            if (stick.sqrMagnitude >= deadZone * deadZone)
            {
                Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = Vector3.forward;
                }
                forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward);

                Vector3 direction = forward * stick.y + right * stick.x;
                if (direction.sqrMagnitude > 1f)
                {
                    direction.Normalize();   // clamp diagonal input to the same speed as forward
                }

                // Velocity events are world space; the head-relative rotation happens here.
                velocity = direction * EmittedMagnitude;
            }

            var locomotionEvent = new LocomotionEvent(
                Identifier, velocity, LocomotionEvent.TranslationType.Velocity);

            locomotor.HandleLocomotionEvent(locomotionEvent);
            WhenLocomotionPerformed.Invoke(locomotionEvent);
        }
    }
}
