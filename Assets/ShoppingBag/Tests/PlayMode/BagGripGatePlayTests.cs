#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace XRGroup10.ShoppingBag.Tests
{
    // Real SDK Update/physics/selector flow with deterministic input, not a hardware acceptance test.
    public sealed class BagGripGatePlayTests
    {
        private GameObject fixture;
        private ShoppingBag bag;
        private GrabInteractor interactor;
        private BagGripTestController input;
        private static IEnumerator Frames(int count = 4)
        {
            for (int i = 0; i < count; i++) yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        private void Create(Handedness hand)
        {
            fixture = new GameObject("C1 isolated SDK fixture"); fixture.SetActive(false);
            fixture.transform.position = new Vector3(10000, 1000, 10000);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab"), fixture.transform);
            root.transform.localPosition = Vector3.zero;
            bag = root.GetComponent<ShoppingBag>();
            var body = root.GetComponent<Rigidbody>(); body.useGravity = false; body.isKinematic = true;
            input = fixture.AddComponent<BagGripTestController>(); input.Handedness = hand;
            var gate = root.GetComponent<BagInteractionGate>(); gate.SetInputSource(input); gate.BindContext(input);
            var handObject = new GameObject("SDK test hand"); handObject.transform.SetParent(fixture.transform, false);
            // Same near-handle geometry as the prefab, no candidate/selection override or ForceSelect.
            handObject.transform.position = bag.GrabPoint.position;
            var handBody = handObject.AddComponent<Rigidbody>(); handBody.isKinematic = true;
            handObject.AddComponent<SphereCollider>().isTrigger = true;
            handObject.GetComponent<SphereCollider>().radius = .08f;
            var selector = handObject.AddComponent<ControllerSelector>();
            selector.ControllerButtonUsage = ControllerButtonUsage.GripButton; selector.InjectController(input);
            interactor = handObject.AddComponent<GrabInteractor>(); interactor.InjectAllGrabInteractor(selector, handBody);
            fixture.SetActive(true);
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            if (fixture != null) Object.Destroy(fixture);
            yield return null;
        }
        [UnityTest] public IEnumerator LeftSdkGripBlocksRearmsAndReleases() { yield return Exercise(Handedness.Left); }
        [UnityTest] public IEnumerator RightSdkGripBlocksRearmsAndReleases() { yield return Exercise(Handedness.Right); }
        private IEnumerator Exercise(Handedness hand)
        {
            Create(hand); yield return Frames();
            input.Blockers = BagInteractionBlockers.MenuIntent; input.Grip = true;
            yield return Frames();
            Assert.That(bag.IsSdkHeld, Is.False, "B + Grip must not select before menu opens");
            input.Blockers = BagInteractionBlockers.Menu; yield return Frames();
            input.Blockers = BagInteractionBlockers.None; yield return Frames();
            Assert.That(bag.IsSdkHeld, Is.False, "Holding Grip through menu close must not acquire");
            input.Tracked = false; input.Grip = false; yield return Frames();
            input.Tracked = true; input.Grip = true; yield return Frames();
            Assert.That(bag.IsSdkHeld, Is.False, "Untracked release cannot arm on tracking recovery");
            input.Grip = false; yield return Frames();
            input.Grip = true; yield return Frames();
            Assert.That(bag.IsSdkHeld, Is.True, "Valid neutral then Grip should use real near-grab flow");
            Assert.That(bag.HeldHand, Is.EqualTo(hand));
            Assert.That(bag.HeldInteractor, Is.SameAs(interactor));
            input.Blockers = BagInteractionBlockers.Menu | BagInteractionBlockers.TextEditing | BagInteractionBlockers.InputFocus;
            yield return Frames(); Assert.That(bag.IsSdkHeld, Is.True, "Blocking acquisition must not force-drop");
            input.Grip = false; yield return Frames();
            Assert.That(bag.IsSdkHeld, Is.False, "Blocked owner's SDK Grip release must still work");
        }
    }
}
#endif
