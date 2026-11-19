using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XRGroup10.ShoppingBag.Tests
{
    public sealed class KitchenRouteContactTests
    {
        private Scene apartment;
        private Transform environment;
        private Type doorType;
        private Material red;

        [OneTimeSetUp]
        public void OpenApartment()
        {
            apartment = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Apartment_Detailed_VR.unity");
            environment = apartment.GetRootGameObjects().Single(go => go.name == "DetailedEnvironment").transform;
            doorType = Type.GetType("ApartmentCabinetDoor, Assembly-CSharp", true);
            red = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShoppingBag/Materials/ContactWarning.mat");
            Assert.That(red, Is.Not.Null);
        }

        [OneTimeTearDown]
        public void CloseApartment()
        {
            if (apartment.IsValid() && apartment.isLoaded) EditorSceneManager.ClosePreviewScene(apartment);
        }

        private Transform Find(string path)
        {
            var result = environment.Find("House/" + path);
            Assert.That(result, Is.Not.Null, path);
            return result;
        }

        private BagContactTarget CheckBinding(Transform leaf)
        {
            var target = leaf.GetComponent<BagContactTarget>();
            Assert.That(target, Is.Not.Null, leaf.name);
            var collider = leaf.GetComponent<Collider>();
            var renderer = leaf.GetComponent<MeshRenderer>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.enabled && !collider.isTrigger, Is.True);
            Assert.That(renderer, Is.Not.Null);
            using (var serialized = new SerializedObject(target))
            {
                var colliders = serialized.FindProperty("furnitureColliders");
                var renderers = serialized.FindProperty("furnitureRenderers");
                Assert.That(colliders.arraySize, Is.EqualTo(1), leaf.name);
                Assert.That(renderers.arraySize, Is.EqualTo(1), leaf.name);
                Assert.That(colliders.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(collider));
                Assert.That(renderers.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(renderer));
                Assert.That(serialized.FindProperty("warningMaterial").objectReferenceValue, Is.SameAs(red));
            }
            return target;
        }

        [TestCase("Structure/WallLC_2a")]
        [TestCase("Structure/WallLC_4a")]
        [TestCase("Structure/WallS_6a")]
        [TestCase("Structure/WallMW_1")]
        [TestCase("Structure/WallMW_3")]
        [TestCase("Structure/WallMN_1")]
        [TestCase("Structure/WallBalW")]
        [TestCase("Structure/WallBalS_1")]
        [TestCase("Structure/WallS_3")]
        [TestCase("Structure/WallLC_1")]
        [TestCase("Structure/WallLC_2b")]
        [TestCase("Fixtures/Kitchen/BaseCab_W_Interactive/SideNorth")]
        [TestCase("Fixtures/Kitchen/BaseCab_W_Interactive/SideSouth")]
        [TestCase("Fixtures/Kitchen/BaseCab_N_Interactive/SideEast")]
        [TestCase("Fixtures/Kitchen/BaseCab_N_Interactive/SideWest")]
        public void RoutePanelRegistersOnlyItsOwnSolidAndVisibleMesh(string path)
        {
            CheckBinding(Find(path));
        }

        private static void Lifecycle(Component component, string method)
        {
            component.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        private void ToggleDoor(Component door, string expected)
        {
            object[] reason = { null };
            Assert.That((bool)doorType.GetMethod("TryInteract").Invoke(door, reason), Is.True, reason[0] as string);
            var advance = doorType.GetMethod("AdvanceMotion");
            var moving = doorType.GetProperty("IsMoving");
            for (int step = 0; step < 40 && (bool)moving.GetValue(door); step++)
                advance.Invoke(door, new object[] { .05f, Vector3.zero, false });
            Assert.That(doorType.GetProperty("State").GetValue(door).ToString(), Is.EqualTo(expected),
                (string)doorType.GetProperty("BlockedReason").GetValue(door));
        }

        private static void ScanAt(ShoppingBag bag, BagContactSensor sensor, Vector3 center)
        {
            bag.transform.position = center - bag.BodyCollider.center;
            Physics.SyncTransforms();
            sensor.ScanNow();
        }

        [Test]
        public void HeldBagCrossesARealRouteWallAndOnlyWarnsAtItsSurface()
        {
            var wall = Find("Structure/WallS_3");
            var target = CheckBinding(wall);
            var solid = wall.GetComponent<Collider>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");
            var root = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(root, apartment);
            var bag = root.GetComponent<ShoppingBag>();
            var sensor = root.GetComponentInChildren<BagContactSensor>();
            try
            {
                Lifecycle(target, "OnEnable");
                Lifecycle(sensor, "OnEnable");
                Assert.That(bag.TrySetSize(BagSize.Medium), Is.True);
                Assert.That(bag.TryBeginDesktopSimulation(Handedness.Right), Is.True);
                Physics.SyncTransforms();
                var bounds = solid.bounds;
                var normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
                Vector3 boundaryOffset = normal * (Vector3.Dot(bounds.extents, normal) + 1f);
                var middle = bounds.center;
                var ray = new Ray(middle + boundaryOffset, -normal);
                Assert.That(solid.Raycast(ray, out var face, boundaryOffset.magnitude * 2f), Is.True);
                ScanAt(bag, sensor, ray.origin);
                Assert.That(target.WarningActive, Is.False);
                ScanAt(bag, sensor, face.point + normal * .02f);
                Assert.That(sensor.ColliderCount(target), Is.EqualTo(1));
                Assert.That(target.WarningActive, Is.True);
                Assert.That(bag.BodyCollider.enabled, Is.False);
                Assert.That(sensor.ContactTrigger.isTrigger, Is.True);
                ScanAt(bag, sensor, middle - boundaryOffset);
                Assert.That(target.WarningActive, Is.False);
                Assert.That(bag.IsHeld, Is.True);
            }
            finally
            {
                bag.EndDesktopSimulation();
                Lifecycle(sensor, "OnDisable");
                Lifecycle(target, "OnDisable");
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("W", 1, Handedness.Left)]
        [TestCase("W", 2, Handedness.Right)]
        [TestCase("W", 3, Handedness.Left)]
        [TestCase("W", 4, Handedness.Right)]
        [TestCase("N", 1, Handedness.Left)]
        [TestCase("N", 2, Handedness.Right)]
        public void GrabbedBagWarnsOnTheMovingDoorInsteadOfItsOldPosition(string bank, int number, Handedness hand)
        {
            string name = "Door_" + number;
            var hinge = Find("Fixtures/Kitchen/BaseCab_" + bank + "_Interactive/" + name + "_Hinge");
            var door = hinge.GetComponent(doorType);
            var leaf = hinge.Find(name);
            var target = CheckBinding(leaf);
            var solid = leaf.GetComponent<BoxCollider>();
            Assert.That(door, Is.Not.Null);
            Assert.That(doorType.GetField("doorCollider").GetValue(door), Is.SameAs(solid));
            Assert.That(doorType.GetField("doorRenderer").GetValue(door), Is.SameAs(leaf.GetComponent<MeshRenderer>()));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");
            var root = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(root, apartment);
            var bag = root.GetComponent<ShoppingBag>();
            var sensor = root.GetComponentInChildren<BagContactSensor>();
            var haptics = root.GetComponent<BagHaptics>();
            try
            {
                doorType.GetMethod("ResetClosed").Invoke(door, null);
                Lifecycle(target, "OnEnable");
                Lifecycle(sensor, "OnEnable");
                Lifecycle(haptics, "OnEnable");
                Assert.That(bag.TrySetSize(BagSize.Small), Is.True);
                Assert.That(bag.TryBeginDesktopSimulation(hand), Is.True);
                var requests = new List<BagHapticRequest>();
                haptics.Requested += requests.Add;
                int enters = 0;
                sensor.ContactStarted += hit => { if (hit == target) enters++; };
                Vector3 center = solid.transform.TransformPoint(solid.center);
                Vector3 towardEdge = Vector3.ProjectOnPlane(center - hinge.position, Vector3.up);
                Vector3 closedPoint = center + towardEdge * .65f;
                Vector3 localPoint = leaf.InverseTransformPoint(closedPoint);
                ScanAt(bag, sensor, closedPoint);
                sensor.ScanNow();
                Assert.That(enters, Is.EqualTo(1));
                Assert.That(requests.Count, Is.EqualTo(1));
                Assert.That(requests[0].Hand, Is.EqualTo(hand));
                Assert.That(requests[0].Amplitude, Is.GreaterThan(0f));
                Assert.That(requests[0].Duration, Is.InRange(.01f, .5f));
                Assert.That(requests[0].HardwareCommandIssued, Is.False);
                Assert.That(target.OverlayRenderers.Count, Is.EqualTo(1));
                Assert.That(target.OverlayRenderers[0].transform.parent, Is.SameAs(leaf));
                Assert.That(target.OverlayRenderers[0].enabled, Is.True);
                Assert.That(sensor.ContactTrigger.isTrigger, Is.True);
                Assert.That(bag.BodyCollider.enabled, Is.False);

                ToggleDoor(door, "Open");
                ScanAt(bag, sensor, closedPoint);
                Assert.That(sensor.ColliderCount(target), Is.Zero);
                Assert.That(target.WarningActive, Is.False);
                Vector3 openPoint = leaf.TransformPoint(localPoint);
                ScanAt(bag, sensor, openPoint);
                Assert.That(sensor.ColliderCount(target), Is.EqualTo(1));
                Assert.That(enters, Is.EqualTo(2));
                Assert.That(target.OverlayRenderers[0].enabled, Is.True);

                ToggleDoor(door, "Closed");
                ScanAt(bag, sensor, openPoint);
                Assert.That(target.WarningActive, Is.False);
                ScanAt(bag, sensor, closedPoint);
                Assert.That(target.WarningActive, Is.True);
                bag.EndDesktopSimulation();
                Assert.That(sensor.ContactCount, Is.Zero);
                Assert.That(target.WarningActive, Is.False);
                Assert.That(haptics.IsPulsing, Is.False);
            }
            finally
            {
                bag.EndDesktopSimulation();
                Lifecycle(haptics, "OnDisable");
                Lifecycle(sensor, "OnDisable");
                Lifecycle(target, "OnDisable");
                Object.DestroyImmediate(root);
                doorType.GetMethod("ResetClosed").Invoke(door, null);
            }
        }
    }
}
