using System;
using UnityEngine;
using UnityEngine.XR;

namespace XRGroup10.ShoppingBag
{
    // OVR mappings match SDK 205 FromOVRControllerDataSource; no new input bindings.
    public sealed class BagRuntimeInputSource : IBagGripInputSource
    {
        public BagGripInputSnapshot Read()
        {
            var sample = new BagGripInputSnapshot { Frame = Time.frameCount };
            if (!Application.isPlaying || OVRManager.instance == null)
            { sample.Blockers = BagInteractionBlockers.InputUnavailable; return sample; }
            try
            {
                if (!Application.isFocused) sample.Blockers |= BagInteractionBlockers.ApplicationFocus;
                if (!OVRManager.hasInputFocus) sample.Blockers |= BagInteractionBlockers.InputFocus;
                if (!OVRManager.hasVrFocus) sample.Blockers |= BagInteractionBlockers.VrFocus;
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                if (!OVRManager.isHmdPresent || !head.isValid ||
                    !head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
                    sample.Blockers |= BagInteractionBlockers.Tracking;
                sample.LeftTracked = Tracked(OVRInput.Controller.LTouch);
                sample.RightTracked = Tracked(OVRInput.Controller.RTouch);
                sample.LeftGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch);
                sample.RightGrip = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);
                // Reserve menu/teleport intent BEFORE the apartment's LateUpdate processes B/A.
                if (OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                    sample.Blockers |= BagInteractionBlockers.MenuIntent;
                if (OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch))
                    sample.Blockers |= BagInteractionBlockers.TeleportIntent;
            }
            catch (Exception)
            { sample.Blockers |= BagInteractionBlockers.InputUnavailable; sample.LeftTracked = sample.RightTracked = false; }
            return sample;
        }
        private static bool Tracked(OVRInput.Controller hand) => OVRInput.IsControllerConnected(hand)
            && OVRInput.GetControllerPositionTracked(hand) && OVRInput.GetControllerOrientationTracked(hand);
    }
}
