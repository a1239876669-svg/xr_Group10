using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Group10.GuidedJourney.Tests
{
    public sealed class ReviewBillboardTests
    {
        GameObject viewer, marker, label;
        Component billboard;
        Type type;
        object Call(string method, params object[] args) => type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance).Invoke(billboard, args);

        [SetUp] public void Setup()
        {
            type = Type.GetType("QuestGuidance.Review.ReviewBillboard, Assembly-CSharp", true);
            viewer = new GameObject("Billboard viewer");
            marker = new GameObject("Real record point");
            label = new GameObject("Label only");
            label.transform.SetParent(marker.transform, false);
            billboard = label.AddComponent(type);
        }

        [TearDown] public void Teardown()
        {
            Object.DestroyImmediate(marker);
            Object.DestroyImmediate(viewer);
        }

        [Test] public void AnchoredLabelMovesTowardViewerWithoutMovingRealPointAndFacesEye()
        {
            Vector3 point = new Vector3(1f, 1.4f, 2f);
            marker.transform.position = point;
            viewer.transform.position = new Vector3(0f, 1.6f, 0f);
            viewer.transform.rotation = Quaternion.Euler(10f, 30f, 0f);
            Assert.IsTrue((bool)Call("SetAnchor", point));
            Assert.IsTrue((bool)Call("PlaceForViewer", viewer.transform));
            Vector3 expected = point + (viewer.transform.position - point).normalized * .06f + Vector3.up * .08f;
            Assert.That(Vector3.Distance(expected, label.transform.position), Is.LessThan(.00001f));
            Assert.AreEqual(point, marker.transform.position, "Only the label may move, never the actual record point.");
            Assert.That(Vector3.Angle(label.transform.forward, label.transform.position - viewer.transform.position), Is.LessThan(.01f));
            Vector3 previous = label.transform.position;
            Call("PlaceForViewer", viewer.transform);
            Assert.That(Vector3.Distance(previous, label.transform.position), Is.LessThan(.00001f), "Placement must not accumulate offsets.");
        }

        [Test] public void CoincidentViewerAndInvalidAnchorCannotProduceNonfinitePose()
        {
            Vector3 point = new Vector3(1f, 2f, 3f);
            viewer.transform.position = point;
            Assert.IsTrue((bool)Call("SetAnchor", point));
            Assert.IsFalse((bool)Call("SetAnchor", new Vector3(float.NaN, 0f, 0f)));
            Assert.IsFalse((bool)Call("PlaceForViewer", new object[] { null }));
            Assert.IsTrue((bool)Call("PlaceForViewer", viewer.transform));
            Vector3 actual = label.transform.position;
            Quaternion rotation = label.transform.rotation;
            foreach (float value in new[] {actual.x, actual.y, actual.z, rotation.x, rotation.y, rotation.z, rotation.w})
                Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value));
            Assert.That(Vector3.Distance(point + Vector3.up * .08f, actual), Is.LessThan(.00001f), "Rejected anchor must not replace the last valid anchor.");
        }

        [Test] public void UnanchoredLegacyLabelRetainsPositionAndCopiesViewerRotation()
        {
            Vector3 original = new Vector3(2f, 1f, 4f);
            label.transform.position = original;
            viewer.transform.rotation = Quaternion.Euler(12f, 34f, 5f);
            Assert.IsTrue((bool)Call("PlaceForViewer", viewer.transform));
            Assert.AreEqual(original, label.transform.position);
            Assert.That(Quaternion.Angle(viewer.transform.rotation, label.transform.rotation), Is.LessThan(.001f));
        }
    }
}
