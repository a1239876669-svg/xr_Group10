#if UNITY_EDITOR
using System;
using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag.Tests
{
    // Unity object so ControllerSelector retains its injected reference through its real Awake.
    public sealed class BagGripTestController : MonoBehaviour, IController, IBagGripInputSource, IBagInteractionContext
    {
        public Handedness Handedness { get; set; }
        public bool Grip, Tracked = true;
        public BagInteractionBlockers Blockers { get; set; }
        public float Scale => 1;
        public bool IsConnected => true;
        public bool IsPoseValid => Tracked;
        public ControllerInput ControllerInput => new ControllerInput
        { ButtonUsageMask = Grip ? ControllerButtonUsage.GripButton : ControllerButtonUsage.None };
        public bool TryGetPose(out Pose pose) { pose = Pose.identity; return Tracked; }
        public bool TryGetPointerPose(out Pose pose) { pose = Pose.identity; return Tracked; }
        public bool IsButtonUsageAnyActive(ControllerButtonUsage usage) => (ControllerInput.ButtonUsageMask & usage) != 0;
        public bool IsButtonUsageAllActive(ControllerButtonUsage usage) => (ControllerInput.ButtonUsageMask & usage) == usage;
        public event Action WhenUpdated { add { } remove { } }
        public BagGripInputSnapshot Read() => new BagGripInputSnapshot
        {
            Frame = Time.frameCount,
            LeftTracked = Handedness != Handedness.Left || Tracked,
            RightTracked = Handedness != Handedness.Right || Tracked,
            LeftGrip = Handedness == Handedness.Left && Grip,
            RightGrip = Handedness == Handedness.Right && Grip
        };
    }
}
#endif
