using QuestGuidance.Locomotion;
using UnityEngine;
using UnityEngine.UI;

namespace QuestGuidance.UI
{
    /// <summary>
    /// Hand-interactable settings panel for locomotion, built to grow into a full UI module.
    ///
    /// This script deliberately holds no state of its own: it reads and writes
    /// <see cref="LocomotionModeController.UseSmoothLocomotion"/> and mirrors whatever that
    /// says. That way the Inspector field, the panel toggle, and any future caller all share
    /// one value, and flipping the mode from outside the UI still updates the panel.
    ///
    /// New settings should be added as sibling panels under <see cref="PanelsRoot"/> with their
    /// own small controller, rather than by growing this class.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocomotionUIModule : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField]
        [Tooltip("Locomotion mode this panel drives. The single source of truth.")]
        private LocomotionModeController modeController;

        [SerializeField]
        [Tooltip("Switch that turns smooth free-walk on and off.")]
        private Toggle smoothLocomotionToggle;

        [Header("Visuals")]
        [SerializeField]
        [Tooltip("Shown while teleport is the active mode.")]
        private GameObject teleportModeHighlight;

        [SerializeField]
        [Tooltip("Shown while smooth locomotion is the active mode.")]
        private GameObject smoothModeHighlight;

        [SerializeField]
        [Tooltip("Container for this module's setting panels. Add future panels under here.")]
        private Transform panelsRoot;

        /// <summary>
        /// Container new setting panels should be parented under, so this module can be
        /// extended without restructuring it.
        /// </summary>
        public Transform PanelsRoot => panelsRoot;

        private void Awake()
        {
            if (modeController == null)
            {
                Debug.LogError($"{nameof(LocomotionUIModule)}: no {nameof(LocomotionModeController)} assigned.", this);
                enabled = false;
                return;
            }

            if (smoothLocomotionToggle == null)
            {
                Debug.LogError($"{nameof(LocomotionUIModule)}: no smooth locomotion toggle assigned.", this);
                enabled = false;
                return;
            }

            smoothLocomotionToggle.onValueChanged.AddListener(OnSmoothLocomotionToggled);
        }

        private void OnDestroy()
        {
            if (smoothLocomotionToggle != null)
            {
                smoothLocomotionToggle.onValueChanged.RemoveListener(OnSmoothLocomotionToggled);
            }
        }

        private void OnEnable()
        {
            if (modeController == null)
            {
                return;
            }

            modeController.WhenModeChanged += HandleModeChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (modeController != null)
            {
                modeController.WhenModeChanged -= HandleModeChanged;
            }
        }

        /// <summary>
        /// Hook for the toggle's On Value Changed, and for UISet buttons that want to drive
        /// the mode directly.
        /// </summary>
        public void OnSmoothLocomotionToggled(bool smooth)
        {
            modeController.UseSmoothLocomotion = smooth;
        }

        /// <summary>
        /// Re-reads the mode. Called both when the toggle is used and when the mode changes
        /// from somewhere else — an Inspector tick, or another script.
        /// </summary>
        private void HandleModeChanged(bool smooth)
        {
            Refresh();
        }

        private void Refresh()
        {
            bool smooth = modeController.UseSmoothLocomotion;

            // Setting isOn re-fires onValueChanged, but the mode setter ignores writes of the
            // value it already holds, so this settles immediately instead of recursing.
            smoothLocomotionToggle.SetIsOnWithoutNotify(smooth);

            if (teleportModeHighlight != null)
            {
                teleportModeHighlight.SetActive(!smooth);
            }

            if (smoothModeHighlight != null)
            {
                smoothModeHighlight.SetActive(smooth);
            }
        }
    }
}
