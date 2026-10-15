using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XRGroup10.ShoppingBag.Tests
{
    public sealed class BagGripSafetyTests
    {
        public static IEnumerable Blockers
        {
            get
            {
                foreach (BagInteractionBlockers value in Enum.GetValues(typeof(BagInteractionBlockers)))
                    if (value != BagInteractionBlockers.None) yield return new TestCaseData(value);
            }
        }
        private static BagGripInputSnapshot Sample(int frame, bool left = false, bool right = false,
            BagInteractionBlockers blockers = BagInteractionBlockers.None, bool leftTracked = true, bool rightTracked = true)
            => new BagGripInputSnapshot { Frame = frame, LeftGrip = left, RightGrip = right,
                LeftTracked = leftTracked, RightTracked = rightTracked, Blockers = blockers };

        [TestCaseSource(nameof(Blockers))]
        public void EveryBlockerRequiresANewNeutralFrame(BagInteractionBlockers blocker)
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(10)); state.Observe(Sample(11, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 11), Is.True);
            state.Observe(Sample(12, true, true, blocker));
            state.Observe(Sample(13, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 13), Is.False);
            Assert.That(state.CanAcquire(Handedness.Right, 13), Is.False);
            state.Observe(Sample(14));
            Assert.That(state.CanAcquire(Handedness.Left, 14), Is.False);
            state.Observe(Sample(15, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 15), Is.True);
            Assert.That(state.CanAcquire(Handedness.Right, 15), Is.True);
        }
        [TestCase(Handedness.Left)] [TestCase(Handedness.Right)]
        public void GripHeldAtStartupCannotAcquire(Handedness hand)
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(1, true, true)); state.Observe(Sample(2, true, true));
            Assert.That(state.CanAcquire(hand, 2), Is.False);
        }
        [Test] public void EachHandRearmsIndependently()
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(1, true, true, BagInteractionBlockers.Menu));
            state.Observe(Sample(2, false, true)); state.Observe(Sample(3, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 3), Is.True);
            Assert.That(state.CanAcquire(Handedness.Right, 3), Is.False);
            state.Observe(Sample(4, true, false)); state.Observe(Sample(5, true, true));
            Assert.That(state.CanAcquire(Handedness.Right, 5), Is.True);
        }
        [TestCase(Handedness.Left)] [TestCase(Handedness.Right)]
        public void InvalidTrackingCannotBeMistakenForARelease(Handedness hand)
        {
            bool left = hand == Handedness.Left;
            var state = new BagGripRearmState();
            state.Observe(Sample(1)); state.Observe(Sample(2, false, false, leftTracked: !left, rightTracked: left));
            state.Observe(Sample(3, true, true));
            Assert.That(state.CanAcquire(hand, 3), Is.False);
            Assert.That(state.CanAcquire(left ? Handedness.Right : Handedness.Left, 3), Is.True);
        }
        [Test] public void NeutralDuringBlockingDoesNotArmAndClearInSameFrameDoesNotArm()
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(5, blockers: BagInteractionBlockers.Menu));
            state.Observe(Sample(5)); state.Observe(Sample(6, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 6), Is.False);
        }
        [Test] public void MultipleReadsAndReleasePressInOneFrameDoNotRearm()
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(5)); state.Observe(Sample(5)); state.Observe(Sample(5, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 5), Is.False);
            state.Observe(Sample(6, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 6), Is.True);
        }
        [Test] public void LaterBlockerInSameFrameOverridesEarlierArmedSample()
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(1)); state.Observe(Sample(2, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 2), Is.True);
            state.Observe(Sample(2, true, true, BagInteractionBlockers.MenuIntent));
            Assert.That(state.CanAcquire(Handedness.Left, 2), Is.False);
            state.Observe(Sample(3, true, true));
            Assert.That(state.CanAcquire(Handedness.Left, 3), Is.False);
        }
        [Test] public void StaleSampleAndUnknownHandFailClosed()
        {
            var state = new BagGripRearmState();
            state.Observe(Sample(8)); state.Observe(Sample(9)); state.Observe(Sample(8));
            Assert.That(state.CanAcquire(Handedness.Left, 9), Is.False);
            Assert.That(state.CanAcquire(Handedness.Left, 8), Is.False);
            Assert.That(state.CanAcquire((Handedness)99, 9), Is.False);
        }

        private sealed class FakeInput : IBagGripInputSource
        {
            public BagGripInputSnapshot Value = Sample(100);
            public bool Throw;
            private readonly int offset = Time.frameCount;
            public BagGripInputSnapshot Read()
            {
                if (Throw) throw new InvalidOperationException("Unavailable");
                var result = Value; result.Frame += offset; return result;
            }
        }
        private sealed class FakeContext : IBagInteractionContext
        {
            public BagInteractionBlockers Value;
            public bool Throw;
            public BagInteractionBlockers Blockers => Throw ? throw new InvalidOperationException("Unavailable") : Value;
        }
        private sealed class FakeController : IController
        {
            public Handedness Handedness { get; set; } = Handedness.Left;
            public bool Connected = true, Tracked = true;
            public ControllerButtonUsage Buttons;
            public float Scale => 1;
            public bool IsConnected => Connected;
            public bool IsPoseValid => Tracked;
            public ControllerInput ControllerInput => new ControllerInput { ButtonUsageMask = Buttons };
            public bool TryGetPose(out Pose pose) { pose = Pose.identity; return Tracked; }
            public bool TryGetPointerPose(out Pose pose) { pose = Pose.identity; return Tracked; }
            public bool IsButtonUsageAnyActive(ControllerButtonUsage usage) => (Buttons & usage) != 0;
            public bool IsButtonUsageAllActive(ControllerButtonUsage usage) => (Buttons & usage) == usage;
            public event Action WhenUpdated { add { } remove { } }
        }
        private Scene scene;
        private GameObject root, handRoot;
        private ShoppingBag bag;
        private BagInteractionGate gate;
        private BagGripFilter filter;
        private FakeInput input;
        private FakeContext context;
        private FakeController controller;
        private ControllerSelector selector;
        private GrabInteractor interactor;
        private static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }
        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab"));
            SceneManager.MoveGameObjectToScene(root, scene);
            bag = root.GetComponent<ShoppingBag>(); gate = root.GetComponent<BagInteractionGate>(); filter = root.GetComponent<BagGripFilter>();
            Invoke(root.GetComponent<Grabbable>(), "Awake");
            Invoke(gate, "OnEnable");
            input = new FakeInput(); context = new FakeContext(); controller = new FakeController();
            gate.SetInputSource(input); gate.BindContext(context);
            handRoot = new GameObject("C1 test hand"); SceneManager.MoveGameObjectToScene(handRoot, scene);
            var body = handRoot.AddComponent<Rigidbody>(); body.isKinematic = true;
            handRoot.AddComponent<SphereCollider>().isTrigger = true;
            selector = handRoot.AddComponent<ControllerSelector>(); selector.ControllerButtonUsage = ControllerButtonUsage.GripButton;
            selector.InjectController(controller);
            interactor = handRoot.AddComponent<GrabInteractor>(); interactor.InjectAllGrabInteractor(selector, body);
        }
        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (handRoot != null) Object.DestroyImmediate(handRoot);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
        }
        private void Arm()
        {
            input.Value = Sample(100); controller.Buttons = ControllerButtonUsage.None;
            Assert.That(filter.Filter(handRoot), Is.False);
            input.Value = Sample(101);
            Assert.That(filter.Filter(handRoot), Is.True);
        }
        [TestCaseSource(nameof(Blockers))]
        public void ContextBlockersAreReadInsideTheSdkFilter(BagInteractionBlockers blocker)
        {
            Arm(); context.Value = blocker; // No Gate.Update or apartment LateUpdate between queries.
            Assert.That(filter.Filter(handRoot), Is.False);
            context.Value = BagInteractionBlockers.None; controller.Buttons = ControllerButtonUsage.GripButton;
            input.Value = Sample(102, true);
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void BAndGripInSameSampleBlockBeforeMenuStateChanges()
        {
            Arm(); controller.Handedness = Handedness.Right;
            controller.Buttons = ControllerButtonUsage.SecondaryButton | ControllerButtonUsage.GripButton;
            input.Value = Sample(102, right: true);
            Assert.That(context.Value, Is.EqualTo(BagInteractionBlockers.None));
            Assert.That(filter.Filter(handRoot), Is.False);
            controller.Buttons = ControllerButtonUsage.GripButton; input.Value = Sample(103, right: true);
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void RightMenuIntentAlsoBlocksLeftGripAndIsNotCachedPerFrame()
        {
            Arm(); input.Value = Sample(102, true); controller.Buttons = ControllerButtonUsage.GripButton;
            Assert.That(filter.Filter(handRoot), Is.True);
            input.Value = Sample(102, true, blockers: BagInteractionBlockers.MenuIntent);
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void RawTeleportIntentBlocksBeforeTeleportStateChanges()
        {
            Arm(); controller.Handedness = Handedness.Right;
            controller.Buttons = ControllerButtonUsage.PrimaryButton | ControllerButtonUsage.GripButton;
            input.Value = Sample(102, right: true);
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void StackedManualBlockersCannotPrematurelyReenable()
        {
            Arm(); gate.SetMenuOpen(true); gate.SetTutorialOpen(true); gate.SetTeleportAiming(true);
            gate.SetTextEditing(true); gate.SetTrackingValid(false); gate.SetInputFocused(false);
            gate.SetMenuOpen(false); gate.SetTutorialOpen(false); gate.SetTeleportAiming(false);
            gate.SetTextEditing(false); gate.SetTrackingValid(true);
            Assert.That(bag.AcquisitionAllowed, Is.False); Assert.That(filter.Filter(handRoot), Is.False);
            gate.SetInputFocused(true); input.Value = Sample(102, true); controller.Buttons = ControllerButtonUsage.GripButton;
            Assert.That(filter.Filter(handRoot), Is.False);
            controller.Buttons = ControllerButtonUsage.None; input.Value = Sample(103);
            Assert.That(filter.Filter(handRoot), Is.False);
            controller.Buttons = ControllerButtonUsage.GripButton; input.Value = Sample(104, true);
            Assert.That(filter.Filter(handRoot), Is.True);
        }
        [Test] public void ContextDisappearanceOrExceptionFailsClosed()
        {
            Arm(); context.Throw = true; Assert.That(filter.Filter(handRoot), Is.False);
            context.Throw = false; gate.BindContext(null); Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void InitiallyBlockedContextCanRecoverWithoutAnotherBindCall()
        {
            context.Value = BagInteractionBlockers.Menu; gate.BindContext(context);
            Assert.That(filter.Filter(handRoot), Is.False);
            context.Value = BagInteractionBlockers.None; input.Value = Sample(101);
            Assert.That(filter.Filter(handRoot), Is.False);
            input.Value = Sample(102, true); controller.Buttons = ControllerButtonUsage.GripButton;
            Assert.That(filter.Filter(handRoot), Is.True);
        }
        [Test] public void FocusAndPauseCallbacksRemainBlockedUntilAllHaveRecovered()
        {
            Arm(); Invoke(gate, "OnApplicationFocus", false); Invoke(gate, "OnApplicationPause", true);
            Assert.That(bag.AcquisitionAllowed, Is.False);
            Invoke(gate, "OnApplicationFocus", true);
            Assert.That(filter.Filter(handRoot), Is.False);
            Invoke(gate, "OnApplicationPause", false);
            controller.Buttons = ControllerButtonUsage.GripButton; input.Value = Sample(102, true);
            Assert.That(filter.Filter(handRoot), Is.False);
            controller.Buttons = ControllerButtonUsage.None; input.Value = Sample(103);
            Assert.That(filter.Filter(handRoot), Is.False);
            controller.Buttons = ControllerButtonUsage.GripButton; input.Value = Sample(104, true);
            Assert.That(filter.Filter(handRoot), Is.True);
        }
        [Test] public void SdkGripStillPressedCannotBeArmedByNeutralRawInput()
        {
            Arm(); context.Value = BagInteractionBlockers.Menu; Assert.That(filter.Filter(handRoot), Is.False);
            context.Value = BagInteractionBlockers.None; controller.Buttons = ControllerButtonUsage.GripButton;
            input.Value = Sample(102); Assert.That(filter.Filter(handRoot), Is.False);
            input.Value = Sample(103); Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void BackgroundPollingCannotArmAStaleHeldSdkGrip()
        {
            Arm(); context.Value = BagInteractionBlockers.Menu; Assert.That(filter.Filter(handRoot), Is.False);
            context.Value = BagInteractionBlockers.None; controller.Buttons = ControllerButtonUsage.GripButton;
            input.Value = Sample(102); Invoke(gate, "Update");
            input.Value = Sample(103); Invoke(gate, "Update");
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void NewlySeenSdkControllerCannotInheritRawOnlyArming()
        {
            input.Value = Sample(100); Invoke(gate, "Update");
            input.Value = Sample(101); Invoke(gate, "Update");
            controller.Buttons = ControllerButtonUsage.GripButton; input.Value = Sample(102, true);
            Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void InputExceptionMissingTrackingAndDisconnectedControllerFailClosed()
        {
            Arm(); controller.Connected = false; Assert.That(filter.Filter(handRoot), Is.False);
            controller.Connected = true; controller.Tracked = false; Assert.That(filter.Filter(handRoot), Is.False);
            controller.Tracked = true; input.Throw = true; Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void GateDisableOrRemovalAndNonGripSelectorsFailClosed()
        {
            Arm(); gate.enabled = false; Assert.That(filter.Filter(handRoot), Is.False);
            gate.enabled = true; selector.ControllerButtonUsage = ControllerButtonUsage.TriggerButton;
            Assert.That(filter.Filter(handRoot), Is.False);
            selector.ControllerButtonUsage = ControllerButtonUsage.GripButton;
            Object.DestroyImmediate(gate); Assert.That(filter.Filter(handRoot), Is.False);
        }
        [Test] public void ExistingOwnerPassesWhileBlockedButSecondHandCannotTakeOver()
        {
            Arm(); root.GetComponent<Grabbable>().GrabPoints.Add(Pose.identity);
            typeof(ShoppingBag).GetProperty(nameof(ShoppingBag.HeldInteractor)).SetValue(bag, interactor);
            context.Value = BagInteractionBlockers.Menu | BagInteractionBlockers.Tracking;
            gate.SetMenuOpen(true);
            Assert.That(filter.Filter(handRoot), Is.True);
            typeof(ShoppingBag).GetProperty(nameof(ShoppingBag.HeldInteractor)).SetValue(bag, null);
            Assert.That(filter.Filter(handRoot), Is.False);
            root.GetComponent<Grabbable>().GrabPoints.Clear();
        }
        [Test] public void SdkSelectionEligibilityUsesFilterBeforeSelect()
        {
            var target = root.GetComponent<GrabInteractable>(); Invoke(target, "Awake"); Invoke(target, "Start"); target.Enable();
            Arm();
            Assert.That(target.CanBeSelectedBy(interactor), Is.True);
            // This is the real SDK eligibility API used by its candidate registry, not a mock registry.
            input.Value = Sample(102, true, blockers: BagInteractionBlockers.MenuIntent);
            Assert.That(target.CanBeSelectedBy(interactor), Is.False);
            Assert.That(target.SelectingInteractors.Count, Is.Zero);
        }
        [Test] public void SdkQueuedGripReleaseIsNotSuppressedByBlocking()
        {
            Invoke(interactor, "Awake"); Invoke(interactor, "Start");
            Invoke(interactor, "OnDisable"); Invoke(interactor, "OnEnable");
            var stateType = interactor.GetType(); FieldInfo stateField = null;
            while (stateType != null && stateField == null)
            { stateField = stateType.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance); stateType = stateType.BaseType; }
            stateField.SetValue(interactor, InteractorState.Select);
            context.Value = BagInteractionBlockers.Menu | BagInteractionBlockers.Tracking;
            gate.SetMenuOpen(true); controller.Buttons = ControllerButtonUsage.GripButton;
            Invoke(selector, "Update"); controller.Buttons = ControllerButtonUsage.None; Invoke(selector, "Update");
            // Consume the synthetic select event, leaving the SDK's real Unselect event queued.
            var type = interactor.GetType(); FieldInfo queueField = null;
            while (type != null && queueField == null)
            { queueField = type.GetField("_selectorQueue", BindingFlags.NonPublic | BindingFlags.Instance); type = type.BaseType; }
            var queue = (Queue<bool>)queueField.GetValue(interactor);
            Assert.That(queue.Dequeue(), Is.True);
            Assert.That(interactor.ShouldUnselect, Is.True);
            interactor.Unselect();
            Assert.That(interactor.State, Is.EqualTo(InteractorState.Hover));
        }
    }
}
