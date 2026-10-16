using System;
using System.Collections.Generic;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace XRGroup10.ShoppingBag.Tests
{
    public sealed class BagFeedbackTests
    {
        private Scene scene;
        private ShoppingBag bag;
        private BagContactSensor sensor;
        private BagHaptics haptics;
        private Material red;
        private readonly List<GameObject> roots = new List<GameObject>();
        private static readonly Vector3 Origin = new Vector3(10000,1000,10000);
        private static void InvokeLifecycle(object component,string method,params object[] arguments)
        {
            component.GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(component,arguments);
        }

        [SetUp] public void SetUp()
        {
            // Preview scenes preserve an already-open untitled/unsaved user scene.
            scene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");
            var root = Object.Instantiate(prefab); roots.Add(root); SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Origin;
            bag = root.GetComponent<ShoppingBag>();
            Assert.That(bag.TrySetSize(BagSize.Medium), Is.True);
            sensor = root.GetComponentInChildren<BagContactSensor>(true);
            haptics = root.GetComponent<BagHaptics>();
            InvokeLifecycle(sensor,"OnEnable"); InvokeLifecycle(haptics,"OnEnable");
            red = AssetDatabase.LoadAssetAtPath<Material>("Assets/ShoppingBag/Materials/ContactWarning.mat");
            Assert.That(red, Is.Not.Null);
        }
        [TearDown] public void TearDown()
        {
            if (bag != null) bag.EndDesktopSimulation();
            if (sensor != null) InvokeLifecycle(sensor,"OnDisable");
            if (haptics != null) InvokeLifecycle(haptics,"OnDisable");
            foreach (var root in roots)
                if (root != null) foreach(var target in root.GetComponentsInChildren<BagContactTarget>(true)) InvokeLifecycle(target,"OnDisable");
            foreach (var root in roots) if (root != null) Object.DestroyImmediate(root);
            roots.Clear();
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.ClosePreviewScene(scene);
        }
        private BagContactTarget Target(Vector3 position, Vector3 size, bool secondCollider=false)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube); roots.Add(root);
            SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = Origin + position;
            var first = root.GetComponent<BoxCollider>(); first.size = size;
            var colliders = new List<Collider>{first};
            if (secondCollider) { var second = root.AddComponent<BoxCollider>(); second.size = size; colliders.Add(second); }
            var target = root.AddComponent<BagContactTarget>();
            target.Configure(colliders.ToArray(),new[]{root.GetComponent<Renderer>()},red);
            return target;
        }
        private void Scan() { Physics.SyncTransforms(); sensor.ScanNow(); }
        private void Hold(Handedness hand=Handedness.Left) { Assert.That(bag.TryBeginDesktopSimulation(hand),Is.True); }

        [Test] public void DedicatedBodyTriggerMatchesAllUnchangedProfiles()
        {
            Assert.That(sensor,Is.Not.Null);
            Assert.That(sensor.ContactTrigger,Is.Not.SameAs(bag.transform.Find("GrabHandleVolume").GetComponent<Collider>()));
            foreach (var size in new[]{BagSize.Small,BagSize.Medium,BagSize.Large})
            {
                Assert.That(bag.TrySetSize(size),Is.True);
                Assert.That(sensor.ContactTrigger.size,Is.EqualTo(bag.BodySize));
                Assert.That(sensor.ContactTrigger.center,Is.EqualTo(bag.BodyCollider.center));
                Assert.That(sensor.ContactTrigger.isTrigger,Is.True);
            }
            Hold(); Assert.That(bag.TrySetSize(BagSize.Large),Is.False);
        }
        [Test] public void OnlyHeldRegisteredFurnitureIsDetectedAndRepeatedScanDoesNotRetrigger()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var unregistered=GameObject.CreatePrimitive(PrimitiveType.Cube); roots.Add(unregistered);
            SceneManager.MoveGameObjectToScene(unregistered,scene);
            unregistered.transform.position=Origin+new Vector3(0,.2f,0);
            int entered=0,exited=0; sensor.ContactStarted+=_=>entered++; sensor.ContactEnded+=_=>exited++;
            Scan(); Assert.That(sensor.ContactCount,Is.Zero);
            Hold(); Scan(); Scan(); Assert.That(sensor.ContactCount,Is.EqualTo(1)); Assert.That(entered,Is.EqualTo(1));
            Assert.That(target.WarningActive,Is.True);
            bag.transform.position=Origin+Vector3.right*3; Scan(); Assert.That(exited,Is.EqualTo(1)); Assert.That(target.WarningActive,Is.False);
        }
        [Test] public void MultipleCollidersDoNotEndFurnitureWarningUntilLastColliderLeaves()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f,true);
            int starts=0,ends=0;sensor.ContactStarted+=_=>starts++;sensor.ContactEnded+=_=>ends++;
            Hold();Scan(); Assert.That(sensor.ColliderCount(target),Is.EqualTo(2));
            target.GetComponents<BoxCollider>()[0].enabled=false;Scan();
            Assert.That(sensor.ColliderCount(target),Is.EqualTo(1));Assert.That(target.WarningActive,Is.True);Assert.That(ends,Is.Zero);
            target.GetComponents<BoxCollider>()[1].enabled=false;Scan();
            Assert.That(target.WarningActive,Is.False);Assert.That(starts,Is.EqualTo(1));Assert.That(ends,Is.EqualTo(1));
        }
        [Test] public void MultipleFurnitureTargetsAndTargetDisableCleanUpIndependently()
        {
            var one=Target(new Vector3(-.1f,.2f,0),Vector3.one*.1f);
            var two=Target(new Vector3(.1f,.2f,0),Vector3.one*.1f);
            Hold();Scan();Assert.That(sensor.ContactCount,Is.EqualTo(2));
            one.enabled=false;Scan();Assert.That(one.WarningActive,Is.False);Assert.That(two.WarningActive,Is.True);
            Assert.That(sensor.ContactCount,Is.EqualTo(1));
            bag.EndDesktopSimulation(); Assert.That(sensor.ContactCount,Is.Zero); Assert.That(two.WarningActive,Is.False);
        }
        [Test] public void OverlayLeavesHoverPropertyBlockAndSharedMaterialsUntouched()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var renderer=target.GetComponent<Renderer>();var original=renderer.sharedMaterial;
            var hover=new MaterialPropertyBlock();hover.SetColor("_Color",Color.cyan);renderer.SetPropertyBlock(hover);
            Hold();Scan();Assert.That(target.OverlayRenderers.Count,Is.EqualTo(1));
            Assert.That(target.OverlayRenderers[0].enabled,Is.True);
            Assert.That(target.OverlayRenderers[0].sharedMaterial,Is.SameAs(red));
            var current=new MaterialPropertyBlock();renderer.GetPropertyBlock(current);Assert.That(current.GetColor("_Color"),Is.EqualTo(Color.cyan));
            // Simulate teammate changing the hover state during a warning.
            hover.SetColor("_Color",Color.green);renderer.SetPropertyBlock(hover);
            bag.EndDesktopSimulation();Assert.That(target.OverlayRenderers[0].enabled,Is.False);
            renderer.GetPropertyBlock(current);Assert.That(current.GetColor("_Color"),Is.EqualTo(Color.green));
            Assert.That(renderer.sharedMaterial,Is.SameAs(original));
            Assert.That(target.OverlayRenderers[0].GetComponents<Collider>(),Is.Empty);
        }
        [TestCase(Handedness.Left)] [TestCase(Handedness.Right)]
        public void HapticRequestsOnlyUseOwnerAndCleanUpOnRelease(Handedness hand)
        {
            Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var requests=new List<BagHapticRequest>();haptics.Requested+=requests.Add;
            Hold(hand);Scan();Scan();Assert.That(requests.Count,Is.EqualTo(1));
            Assert.That(requests[0].Hand,Is.EqualTo(hand));Assert.That(requests[0].Amplitude,Is.GreaterThan(0));
            Assert.That(requests[0].HardwareCommandIssued,Is.False);Assert.That(haptics.IsPulsing,Is.True);
            bag.EndDesktopSimulation();Assert.That(haptics.IsPulsing,Is.False);
            Assert.That(requests.Count,Is.EqualTo(2));Assert.That(requests[1].Amplitude,Is.Zero);
        }
        [Test] public void FocusLossAndDisableClearContactsAndPulse()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            Hold();Scan();Assert.That(haptics.IsPulsing,Is.True);
            InvokeLifecycle(sensor,"OnApplicationFocus",false);
            Assert.That(sensor.ContactCount,Is.Zero);Assert.That(target.WarningActive,Is.False);Assert.That(haptics.IsPulsing,Is.False);
            InvokeLifecycle(sensor,"OnApplicationFocus",true);Scan();
            InvokeLifecycle(sensor,"OnDisable");Assert.That(sensor.ContactCount,Is.Zero);Assert.That(target.WarningActive,Is.False);
        }
        [Test] public void HapticCooldownIsPerHandAndExpiresAtBoundary()
        {
            var cooldown=new BagHapticCooldown();
            Assert.That(cooldown.TryStart(Handedness.Left,1,.25f),Is.True);
            Assert.That(cooldown.TryStart(Handedness.Left,1.1f,.25f),Is.False);
            Assert.That(cooldown.TryStart(Handedness.Right,1.1f,.25f),Is.True);
            Assert.That(cooldown.TryStart(Handedness.Left,1.25f,.25f),Is.True);
        }
        [TestCase(BagSize.Small,.38f,0)] [TestCase(BagSize.Medium,.38f,2)] [TestCase(BagSize.Large,.38f,2)]
        [TestCase(BagSize.Small,.52f,0)] [TestCase(BagSize.Medium,.52f,0)] [TestCase(BagSize.Large,.52f,2)]
        public void SameUprightRouteProducesRealSizeDependentContacts(BagSize size,float gap,int expected)
        {
            bag.TrySetSize(size);
            Target(new Vector3(-gap*.5f-.1f,.25f,0),new Vector3(.2f,1,.45f));
            Target(new Vector3(gap*.5f+.1f,.25f,0),new Vector3(.2f,1,.45f));
            Hold();Scan();Assert.That(sensor.ContactCount,Is.EqualTo(expected));
        }
        [Test] public void GateCombinesBlockersWithoutDroppingExistingHold()
        {
            var gate=bag.GetComponent<BagInteractionGate>();InvokeLifecycle(gate,"OnEnable");
            gate.SetMenuOpen(true);Assert.That(bag.TryBeginDesktopSimulation(Handedness.Left),Is.False);
            gate.SetTutorialOpen(true);gate.SetMenuOpen(false);Assert.That(bag.AcquisitionAllowed,Is.False);
            gate.SetTutorialOpen(false);Hold();gate.SetTeleportAiming(true);Assert.That(bag.IsHeld,Is.True);
            bag.EndDesktopSimulation();Assert.That(bag.TryBeginDesktopSimulation(Handedness.Right),Is.False);
            gate.SetTeleportAiming(false);Assert.That(bag.AcquisitionAllowed,Is.True);
        }

        [Test] public void MultipleRenderersReceiveOverlayWithoutDuplicatingGeometryOnReentry()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var child=GameObject.CreatePrimitive(PrimitiveType.Cube);child.transform.SetParent(target.transform,false);
            target.Configure(new[]{target.GetComponent<Collider>()},new[]{target.GetComponent<Renderer>(),child.GetComponent<Renderer>()},red);
            Hold();Scan();Assert.That(target.OverlayRenderers.Count,Is.EqualTo(2));
            bag.transform.position=Origin+Vector3.right*3;Scan();
            foreach(var overlay in target.OverlayRenderers)Assert.That(overlay.enabled,Is.False);
            bag.transform.position=Origin;Scan();Assert.That(target.OverlayRenderers.Count,Is.EqualTo(2));
        }

        [Test] public void TwoBagSourcesKeepWarningUntilBothHaveReleased()
        {
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");
            var otherRoot=Object.Instantiate(prefab);roots.Add(otherRoot);SceneManager.MoveGameObjectToScene(otherRoot,scene);otherRoot.transform.position=Origin;
            var otherBag=otherRoot.GetComponent<ShoppingBag>();otherBag.TrySetSize(BagSize.Medium);
            var otherSensor=otherRoot.GetComponentInChildren<BagContactSensor>();InvokeLifecycle(otherSensor,"OnEnable");
            Hold();Assert.That(otherBag.TryBeginDesktopSimulation(Handedness.Right),Is.True);
            Scan();otherSensor.ScanNow();Assert.That(target.WarningSourceCount,Is.EqualTo(2));
            bag.EndDesktopSimulation();Assert.That(target.WarningActive,Is.True);Assert.That(target.WarningSourceCount,Is.EqualTo(1));
            otherBag.EndDesktopSimulation();Assert.That(target.WarningActive,Is.False);InvokeLifecycle(otherSensor,"OnDisable");
        }

        [Test] public void RedOverlayActuallyRendersOverHoverAndRestoresHoverAfterExit()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline!=null)
                Assert.Ignore("Camera.Render image check requires Built-in pipeline; use the project's SRP render test path instead.");
            var target=Target(new Vector3(0,.2f,0),Vector3.one*.1f);
            var renderer=target.GetComponent<Renderer>();
            var baseMaterial=new Material(Shader.Find("Unlit/Color"));renderer.sharedMaterial=baseMaterial;
            target.gameObject.layer=31;
            var hover=new MaterialPropertyBlock();hover.SetColor("_Color",Color.cyan);renderer.SetPropertyBlock(hover);
            var cameraObject=new GameObject("BagFeedbackCapture");roots.Add(cameraObject);SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.scene=scene;
            camera.transform.position=Origin+new Vector3(0,.2f,-2);camera.orthographic=true;camera.orthographicSize=.75f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=(1<<31)|(1<<2);
            var rt=new RenderTexture(128,128,24);var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;
                Hold();Scan();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                var warning=image.GetPixel(64,64);
                Assert.That(warning.r,Is.GreaterThan(.8f));Assert.That(warning.g,Is.LessThan(.2f));
                var directory=System.IO.Path.Combine(Application.dataPath,"../Logs/ShoppingBagPhaseB-20261015");System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"overlay-warning.png"),image.EncodeToPNG());
                bag.EndDesktopSimulation();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                var restored=image.GetPixel(64,64);
                Assert.That(restored.r,Is.LessThan(.2f));Assert.That(restored.g,Is.GreaterThan(.8f));Assert.That(restored.b,Is.GreaterThan(.8f));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"overlay-restored-hover.png"),image.EncodeToPNG());
            }
            finally {camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(baseMaterial);}
        }
    }
}
