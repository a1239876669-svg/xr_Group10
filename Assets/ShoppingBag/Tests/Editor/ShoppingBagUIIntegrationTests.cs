using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace XRGroup10.ShoppingBag.Tests
{
    // The test asmdef must not reference Assembly-CSharp; reflection tests the real adapter.
    public sealed class ShoppingBagUIIntegrationTests
    {
        GameObject owner, instance;
        ShoppingBag bag;
        Component menu, bridge;
        Type menuType, bridgeType;
        static Type ApartmentType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
        static void SetProperty(Component target, string name, object value) => target.GetType().GetProperty(name)
            .GetSetMethod(true).Invoke(target, new[] { value });
        void SetField(string name, object value) => bridgeType.GetField(name).SetValue(bridge, value);
        object Call(string name, params object[] args) => bridgeType.GetMethod(name).Invoke(bridge, args);
        void SetMenu(bool open, int page = 3)
        {
            var panelType = ApartmentType("ApartmentPanelState");
            SetProperty(menu, "ActivePanel", Enum.ToObject(panelType, open ? 1 : 0));
            SetProperty(menu, "InputReady", true); SetProperty(menu, "Page", page);
        }
        [SetUp] public void Setup()
        {
            menuType = ApartmentType("ApartmentMenuController"); bridgeType = ApartmentType("ApartmentShoppingBagBridge");
            Assert.That(menuType, Is.Not.Null); Assert.That(bridgeType, Is.Not.Null);
            owner = new GameObject("UI adapter test"); menu = owner.AddComponent(menuType); bridge = owner.AddComponent(bridgeType);
            instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab"));
            bag = instance.GetComponent<ShoppingBag>(); Call("Configure", menu, bag); SetMenu(true);
        }
        [TearDown] public void Teardown()
        {
            if (bag != null) bag.EndDesktopSimulation();
            if (owner != null) Object.DestroyImmediate(owner);
            if (instance != null) Object.DestroyImmediate(instance);
        }
        [TestCase(BagSize.Small, .30f)] [TestCase(BagSize.Medium, .45f)] [TestCase(BagSize.Large, .60f)]
        public void MenuBlocksGripButAllowsActualProfileChange(BagSize size, float width)
        {
            Assert.That(((IBagInteractionContext)bridge).Blockers.HasFlag(BagInteractionBlockers.Menu), Is.True);
            Assert.That((bool)Call("TrySelectSize", size), Is.True);
            Assert.That(bag.CurrentSize, Is.EqualTo(size)); Assert.That(bag.BodyCollider.size.x, Is.EqualTo(width).Within(.0001));
        }
        [Test] public void StaleButtonCannotResizeHeldBagAndReleaseRestoresSizing()
        {
            bag.SetAcquisitionAllowed(true); Assert.That(bag.TryBeginDesktopSimulation(Handedness.Left), Is.True);
            var size = bag.CurrentSize;
            Assert.That((bool)Call("TrySelectSize", BagSize.Large), Is.False); Assert.That(bag.CurrentSize, Is.EqualTo(size));
            bag.EndDesktopSimulation(); Assert.That((bool)Call("TrySelectSize", BagSize.Large), Is.True);
        }
        [Test] public void ClosedOrUnrelatedPageRejectsOldCallbacks()
        {
            SetMenu(false); Assert.That((bool)Call("TrySelectSize", BagSize.Small), Is.False);
            SetMenu(true, 4); Assert.That((bool)Call("TrySelectSize", BagSize.Small), Is.False);
        }
        [Test] public void DisabledOrMissingContextFailsClosed()
        {
            ((Behaviour)bridge).enabled = false;
            Assert.That(((IBagInteractionContext)bridge).Blockers.HasFlag(BagInteractionBlockers.ContextUnavailable), Is.True);
            Assert.That((bool)Call("TrySelectSize", BagSize.Small), Is.False);
            ((Behaviour)bridge).enabled = true; SetField("additionalContext", bag);
            Assert.That(((IBagInteractionContext)bridge).Blockers.HasFlag(BagInteractionBlockers.ContextUnavailable), Is.True);
        }
        [Test] public void UnrelatedPageDoesNotRefreshAndChangesAreCoalesced()
        {
            SetMenu(true, 4);
            int revision = (int)menuType.GetProperty("UIRevision").GetValue(menu);
            bag.TrySetSize(BagSize.Small); bag.TrySetSize(BagSize.Large);
            bridgeType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bridge, null);
            Assert.That(menuType.GetProperty("UIRevision").GetValue(menu), Is.EqualTo(revision));
            SetMenu(true); bag.TrySetSize(BagSize.Medium); bag.TrySetSize(BagSize.Small);
            bridgeType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bridge, null);
            Assert.That(menuType.GetProperty("UIRevision").GetValue(menu), Is.EqualTo(revision + 1));
        }
        [Test] public void FixtureHasSerializedGateBridgeAndNoSecondRig()
        {
            var scene = EditorSceneManager.OpenScene("Assets/ShoppingBag/Integration/ShoppingBag_UI_Test.unity", OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
                var adapter = components.Single(c => c != null && c.GetType() == bridgeType);
                var fixtureBag = (ShoppingBag)bridgeType.GetField("bag").GetValue(adapter);
                Assert.That(fixtureBag, Is.Not.Null);
                var gate = (BagInteractionGate)bridgeType.GetField("gate").GetValue(adapter);
                Assert.That(gate, Is.Not.Null);
                Assert.That(new SerializedObject(gate).FindProperty("contextProvider").objectReferenceValue, Is.EqualTo(adapter));
                Assert.That(components.OfType<Camera>(), Is.Empty);
                Assert.That(components.Count(c => c != null && c.GetType().Name == "OVRManager"), Is.Zero);
                Assert.That(components.OfType<BagDesktopTestDriver>(), Is.Empty);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}