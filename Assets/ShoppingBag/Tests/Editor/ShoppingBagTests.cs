using NUnit.Framework;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;

namespace XRGroup10.ShoppingBag.Tests
{
    public sealed class ShoppingBagTests
    {
        private GameObject instance;
        private ShoppingBag bag;
        [SetUp] public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");
            Assert.That(prefab, Is.Not.Null);
            instance = Object.Instantiate(prefab);
            bag = instance.GetComponent<ShoppingBag>();
            var grabbable = instance.GetComponent<Grabbable>();
            if (grabbable.GrabPoints == null)
                typeof(Grabbable).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(grabbable, null);
        }
        [TearDown] public void TearDown() { if (instance != null) Object.DestroyImmediate(instance); }

        [TestCase(BagSize.Small, .30f, .30f, .15f)]
        [TestCase(BagSize.Medium, .45f, .40f, .22f)]
        [TestCase(BagSize.Large, .60f, .48f, .30f)]
        public void SizeChangesBodyAndColliderInMetres(BagSize size, float w, float h, float d)
        {
            Assert.That(bag.TrySetSize(size), Is.True);
            var expected = new Vector3(w, h, d);
            Assert.That(Vector3.Distance(bag.BodyCollider.size, expected), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(instance.transform.Find("Body").localScale, expected), Is.LessThan(.00001f));
            Assert.That(bag.BodyCollider.center.y, Is.EqualTo(h * .5f).Within(.00001f));
            Assert.That(bag.GrabPoint.localPosition.y, Is.GreaterThan(h));
            Assert.That(instance.transform.Find("HandleTop").localPosition, Is.EqualTo(bag.GrabPoint.localPosition));
        }

        [Test] public void HeldSizeChangeDoesNotMutateGeometryOrEmitEvent()
        {
            bag.TrySetSize(BagSize.Medium);
            var grabbable = instance.GetComponent<Grabbable>();
            // Public SDK grab-point list is sufficient to exercise the held guard without a rig.
            grabbable.GrabPoints.Add(Pose.identity);
            int changes = 0; bag.SizeChanged += _ => changes++;
            Assert.That(bag.TrySetSize(BagSize.Large), Is.False);
            Assert.That(bag.CurrentSize, Is.EqualTo(BagSize.Medium));
            Assert.That(bag.BodyCollider.size.x, Is.EqualTo(.45f));
            Assert.That(changes, Is.Zero);
            grabbable.GrabPoints.Clear();
            Assert.That(bag.TrySetSize(BagSize.Large), Is.True);
        }

        [Test] public void InvalidSizeOrScaledParentCannotResize()
        {
            Assert.That(bag.TrySetSize((BagSize)99), Is.False);
            instance.transform.localScale = Vector3.one * 2f;
            Assert.That(bag.TrySetSize(BagSize.Small), Is.False);
        }

        [Test] public void PrefabHasSingleControllerNearGrabAndNoRemoteOrHandTargets()
        {
            var interactable = instance.GetComponent<GrabInteractable>();
            Assert.That(interactable.MaxSelectingInteractors, Is.EqualTo(1));
            Assert.That(instance.GetComponent<Grabbable>().MaxGrabPoints, Is.EqualTo(1));
            Assert.That(instance.GetComponentsInChildren<DistanceGrabInteractable>(), Is.Empty);
            Assert.That(instance.GetComponentsInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>(), Is.Empty);
            Assert.That(instance.GetComponent<Rigidbody>().useGravity, Is.True);
            Assert.That(instance.GetComponent<Rigidbody>().isKinematic, Is.False);
            Assert.That(instance.transform.Find("GrabHandleVolume").GetComponent<BoxCollider>().isTrigger, Is.True);
        }

        [Test] public void FilterRejectsObjectsWithoutControllerNearGrab()
        {
            var filter = instance.GetComponent<BagGripFilter>();
            Assert.That(filter.Filter(null), Is.False);
            Assert.That(filter.Filter(instance), Is.False);
            bag.SetAcquisitionAllowed(false);
            Assert.That(bag.AcquisitionAllowed, Is.False);
        }
    }
}
