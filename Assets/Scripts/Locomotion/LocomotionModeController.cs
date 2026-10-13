using System;
using Oculus.Interaction.Locomotion;
using UnityEngine;

namespace QuestGuidance.Locomotion
{
    /// <summary>
    /// Single source of truth for the locomotion mode, and the one place that turns it into
    /// an actual behaviour change.
    ///
    /// Teleport is the default; ticking useSmoothLocomotion in the Inspector, or flipping the
    /// panel toggle, swaps to free walking. Both routes write this one field, so there is
    /// never a second flag that can drift out of sync.
    ///
    /// The switching itself is delegated to the SDK's own settings rather than hand-rolled:
    /// <see cref="MovingSetting"/> already owns the teleport and slide GameObjects (including
    /// the teleport interactor, its arc and its reticle) and <see cref="TurningSetting"/>
    /// already knows how to put the turners into snap mode. Writing a parallel enable/disable
    /// path here would just be a second thing to keep aligned.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocomotionModeController : MonoBehaviour
    {
        [Header("Mode  (single source of truth)")]
        [SerializeField]
        [Tooltip("Off = teleport (default). On = smooth free-walk on the left stick.")]
        private bool useSmoothLocomotion;

        [Header("Turning")]
        [SerializeField]
        [Tooltip("On = snap turn on the right stick (comfort default). Off = smooth turn.")]
        private bool useSnapTurn = true;

        [SerializeField]
        [Range(15f, 90f)]
        [Tooltip("Degrees turned per snap.")]
        private float snapTurnDegrees = 45f;

        [Header("Wiring")]
        [SerializeField]
        [Tooltip("Smooth locomotion source to switch on and off. Resolved from the rig when left empty.")]
        private ThumbstickLocomotionBroadcaster smoothBroadcaster;

        private MovingSetting _movingSetting;
        private TurningSetting _turningSetting;
        private bool _applying;

        /// <summary>Raised after the mode actually changes, so UI can mirror it.</summary>
        public event Action<bool> WhenModeChanged = delegate { };

        public bool UseSmoothLocomotion
        {
            get => useSmoothLocomotion;
            set
            {
                if (useSmoothLocomotion == value)
                {
                    return;
                }

                useSmoothLocomotion = value;
                Apply();
                WhenModeChanged.Invoke(useSmoothLocomotion);
            }
        }

        public bool UseSnapTurn
        {
            get => useSnapTurn;
            set
            {
                if (useSnapTurn == value)
                {
                    return;
                }

                useSnapTurn = value;
                Apply();
            }
        }

        public float SnapTurnDegrees
        {
            get => snapTurnDegrees;
            set
            {
                value = Mathf.Clamp(value, 15f, 90f);
                if (Mathf.Approximately(snapTurnDegrees, value))
                {
                    return;
                }

                snapTurnDegrees = value;
                Apply();
            }
        }

        private void Awake()
        {
            ResolveReferences();

            if (_movingSetting == null || _turningSetting == null)
            {
                Debug.LogError($"{nameof(LocomotionModeController)}: could not find " +
                               $"{nameof(MovingSetting)}/{nameof(TurningSetting)} in the rig.", this);
                enabled = false;
                return;
            }

            // WhenChanged also fires on subscribe, so this doubles as the initial pull into sync.
            _movingSetting.ControllerMovement.WhenChanged += HandleMovementChanged;
            _turningSetting.ControllerTurn.WhenChanged += HandleTurnStyleChanged;

            Apply();
        }

        private void OnDestroy()
        {
            if (_movingSetting != null)
            {
                _movingSetting.ControllerMovement.WhenChanged -= HandleMovementChanged;
            }

            if (_turningSetting != null)
            {
                _turningSetting.ControllerTurn.WhenChanged -= HandleTurnStyleChanged;
            }
        }

        /// <summary>
        /// Keeps the Inspector field truthful when the mode is changed from somewhere else —
        /// the SDK's own Locomotion Settings inspector, or a future UI panel that writes the
        /// setting directly.
        /// </summary>
        private void HandleMovementChanged(MovingSetting.MovementStyle style)
        {
            if (_applying)
            {
                return;
            }

            bool smooth = style == MovingSetting.MovementStyle.Slide;
            if (smooth == useSmoothLocomotion)
            {
                return;
            }

            useSmoothLocomotion = smooth;
            SyncBroadcaster();
            WhenModeChanged.Invoke(useSmoothLocomotion);
        }

        private void HandleTurnStyleChanged(TurningSetting.RotationStyle style)
        {
            if (!_applying)
            {
                useSnapTurn = style == TurningSetting.RotationStyle.Snap;
            }
        }

        private void ResolveReferences()
        {
            if (_movingSetting == null)
            {
                _movingSetting = GetComponentInChildren<MovingSetting>(true);
            }

            if (_movingSetting == null)
            {
                _movingSetting = FindAnyObjectByType<MovingSetting>();
            }

            if (_turningSetting == null)
            {
                _turningSetting = GetComponentInChildren<TurningSetting>(true);
            }

            if (_turningSetting == null)
            {
                _turningSetting = FindAnyObjectByType<TurningSetting>();
            }

            if (smoothBroadcaster == null)
            {
                smoothBroadcaster = GetComponentInChildren<ThumbstickLocomotionBroadcaster>(true);
            }

            if (smoothBroadcaster == null)
            {
                smoothBroadcaster = FindAnyObjectByType<ThumbstickLocomotionBroadcaster>();
            }
        }

        private void Apply()
        {
            if (_movingSetting == null || _turningSetting == null)
            {
                return;
            }

            _applying = true;

            _movingSetting.ControllerMovement.Value = useSmoothLocomotion
                ? MovingSetting.MovementStyle.Slide
                : MovingSetting.MovementStyle.Teleport;

            _turningSetting.ControllerTurn.Value = useSnapTurn
                ? TurningSetting.RotationStyle.Snap
                : TurningSetting.RotationStyle.Smooth;
            _turningSetting.RotationSnapAngle.Value = snapTurnDegrees;

            SyncBroadcaster();

            _applying = false;
        }

        private void SyncBroadcaster()
        {
            if (smoothBroadcaster != null)
            {
                smoothBroadcaster.enabled = useSmoothLocomotion;
            }
        }
    }
}
