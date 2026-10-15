#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using TMPro;
using Object = UnityEngine.Object;

// The saved apartment is only queried. Animated geometry is cloned into an unsaved scratch scene.
public static class ApartmentFurnitureChecks
{
    [Serializable] class Report
    {
        public string utc;
        public int passed;
        public string failure = "";
        public bool questLinkVerified;
        public string limits = "Editor logic, cloned geometry and mono UI render only; actual Quest input/stereo/comfort not verified.";
        public List<string> checks = new List<string>();
    }
    static Report report;
    [MenuItem("Apartment Phase 2/M3 Furniture/Run Kitchen Interaction Checks %#&k")]
    public static void Run()
    {
        report = new Report { utc = DateTime.UtcNow.ToString("o") }; Scene original = default, scratch = default;
        try
        {
            original = ApartmentFurnitureSetup.RequireScene();
            if (original.isDirty) throw new InvalidOperationException("Save the scene before testing.");
            ApartmentFurnitureSetup.ValidateOrThrow(original); Assert(true, "saved scene configuration");
            var source = ApartmentFurnitureSetup.All<ApartmentFurnitureInteractor>(original).Single();
            CheckTrigger(); CheckNavigation(source);
            var root = original.GetRootGameObjects().Single(o => o.name == "ApartmentRoot");
            scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
            var clone = Object.Instantiate(root); SceneManager.MoveGameObjectToScene(clone, scratch);
            foreach (var surface in clone.GetComponentsInChildren<NavMeshSurface>()) { surface.RemoveData(); surface.enabled = false; }
            clone.transform.position += Vector3.right * 1000f;
            var player = new GameObject("Temporary interaction player"); player.transform.position = new Vector3(1000, -20, 0);
            var proxy = player.AddComponent<CharacterController>(); proxy.radius = .3f; proxy.height = 1.8f;
            proxy.center = Vector3.up * .9f; proxy.skinWidth = .03f; proxy.enabled = false;
            var owner = player.AddComponent<ApartmentFurnitureInteractor>(); owner.locomotion = source.locomotion; owner.hintFont = source.hintFont;
            var doors = clone.GetComponentsInChildren<ApartmentCabinetDoor>(); owner.doors = doors;
            Assert(doors.Length == 6, "six cloned doors");
            foreach (var door in doors) { door.inputOwner = owner; door.ResetClosed(); }
            Physics.SyncTransforms();
            foreach (var door in doors)
            {
                Vector3 closedPosition = door.doorCollider.transform.position; Quaternion closedRotation = door.doorCollider.transform.rotation;
                Assert(door.TryInteract(out _), door.name + " accepts open"); CompleteMotion(door);
                Assert(door.State == ApartmentCabinetState.Open && Mathf.Abs(door.CurrentAngle - door.openAngle) < .001f,
                    door.name + " reaches 90 degrees without stationary-obstacle penetration: " + door.BlockedReason);
                Assert(door.InteractionPrompt == ApartmentCabinetDoor.ClosePrompt, door.name + " open prompt");
                Assert(door.TryInteract(out _), door.name + " accepts close"); CompleteMotion(door);
                Assert(door.State == ApartmentCabinetState.Closed, door.name + " closes safely: " + door.BlockedReason);
                Assert(Vector3.Distance(closedPosition, door.doorCollider.transform.position) < .00001f &&
                    Quaternion.Angle(closedRotation, door.doorCollider.transform.rotation) < .001f, door.name + " returns to original closed geometry");
                Assert(door.InteractionPrompt == ApartmentCabinetDoor.OpenPrompt, door.name + " closed prompt");
            }
            var sample = doors.First(d => d.name == "Door_1_Hinge" && d.transform.parent.name == "CounterNorth");
            CheckPauseAndBody(sample, proxy); CheckQueries(owner, sample); CheckHighlight(owner, sample, doors.First(d => d != sample));
            foreach (var door in doors) { door.ResetClosed(); door.TryInteract(out _); }
            for (int i = 0; i < 25; i++) foreach (var door in doors) door.AdvanceMotion(.05f, Vector3.zero, false);
            Assert(doors.Any(d => d.State == ApartmentCabinetState.Blocked), "simultaneous neighbouring doors stop at interference");
            foreach (var a in doors) foreach (var b in doors)
                if (a != b) Assert(!Physics.ComputePenetration(a.doorCollider, a.doorCollider.transform.position, a.doorCollider.transform.rotation,
                    b.doorCollider, b.doorCollider.transform.position, b.doorCollider.transform.rotation, out _, out float depth) || depth < .0001f,
                    a.transform.parent.name + "/" + a.name + " does not penetrate " + b.transform.parent.name + "/" + b.name);
            RenderHints(owner);
        }
        catch (Exception e) { report.failure = e.ToString(); }
        finally
        {
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
            Physics.SyncTransforms(); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/FurnitureChecks.json", JsonUtility.ToJson(report, true));
            if (report.failure == "") Debug.Log("M3 checks PASS: " + report.passed + " assertions. Logs/FurnitureChecks.json; Quest hardware NOT verified.");
            else Debug.LogError("M3 checks FAILED after " + report.passed + ": " + report.failure);
        }
    }
    static void CompleteMotion(ApartmentCabinetDoor door)
    { for (int i = 0; i < 25 && door.IsMoving; i++) door.AdvanceMotion(.05f, Vector3.zero, false); }
    static void CheckTrigger()
    {
        var input = new ApartmentInteractionTriggerLatch();
        Assert(!input.Sample(1f, true), "startup held trigger is disarmed");
        Assert(!input.Sample(.25f, true), "release threshold rearms without action");
        Assert(!input.Sample(.74f, true), "partial trigger does not click");
        Assert(input.Sample(.75f, true), "press threshold clicks once");
        Assert(!input.Sample(1f, true) && !input.Sample(.5f, true) && !input.Sample(1f, true), "hold and partial release do not repeat");
        input.Sample(0f, true); Assert(input.Sample(1f, true), "full release allows next click");
        Assert(!input.Sample(0f, false) && !input.Sample(1f, true), "teleport/focus suppression requires new release");
        input.Sample(0f, true); Assert(input.Sample(1f, true), "recovery neutral rearm");
        Assert(!input.Sample(float.NaN, true) && !input.Sample(1f, true), "nonfinite input fails closed");
        input.Reset(); Assert(!input.Sample(1f, true), "target cannot activate from previously held press");
    }
    static void CheckNavigation(ApartmentFurnitureInteractor source)
    {
        var filter = new NavMeshQueryFilter { agentTypeID = source.locomotion.teleportSurface.agentTypeID, areaMask = 1 };
        var kitchen = ApartmentFurnitureSetup.Find(source.gameObject.scene, ApartmentFurnitureSetup.KitchenPath);
        foreach (string name in new[] { "CounterNorth", "CounterWest", "CounterSouth" })
        {
            var cabinet = kitchen.Find(name); var size = cabinet.Find("CabinetBody").localScale;
            for (int x = 1; x < 4; x++) for (int z = 1; z < 4; z++)
            {
                Vector3 p = cabinet.position + new Vector3(size.x * (x/4f - .5f), 0, size.z * (z/4f - .5f));
                Assert(!NavMesh.SamplePosition(p, out _, .02f, filter), name + " footprint cannot be a teleport destination " + x + "/" + z);
            }
        }
        foreach (string name in new[] { "CounterNorth", "CounterSouth" })
            Assert(ApartmentFurnitureInteractor.FindInteractable(kitchen.Find(name + "/Door_0").GetComponent<Collider>()) == null,
                name + " corner panel remains noninteractive");
        Assert(!source.RuntimeReady, "Editor testing never pretends to be tracked Quest play");
    }
    static void CheckPauseAndBody(ApartmentCabinetDoor door, CharacterController player)
    {
        door.ResetClosed(); door.TryInteract(out _); door.AdvanceMotion(.1f, Vector3.zero, false);
        Assert(!door.TryInteract(out _), "moving door ignores repeated press");
        float before = door.CurrentAngle; door.PauseMotion(); door.AdvanceMotion(.05f, Vector3.zero, false);
        Assert(door.State == ApartmentCabinetState.Paused && door.CurrentAngle == before, "tracking loss freezes angle and target");
        Assert(door.TryInteract(out _), "fresh press resumes paused opening"); CompleteMotion(door);
        Assert(door.State == ApartmentCabinetState.Open, "paused opening completes");
        Vector3 openCenter = door.doorCollider.bounds.center; door.ResetClosed();
        player.transform.position = new Vector3(openCenter.x, 0, openCenter.z); player.enabled = true; Physics.SyncTransforms();
        door.TryInteract(out _); CompleteMotion(door);
        Assert(door.State == ApartmentCabinetState.Blocked && Mathf.Abs(door.CurrentAngle) < 90f, "CharacterController stops door without being pushed");
        before = door.CurrentAngle; Vector3 playerBefore = player.transform.position; door.AdvanceMotion(.05f, Vector3.zero, false);
        Assert(door.CurrentAngle == before && player.transform.position == playerBefore, "blocked door waits without auto-resume or moving player");
        player.enabled = false; Assert(door.TryInteract(out _), "fresh press resumes blocked door"); CompleteMotion(door);
        Assert(door.State == ApartmentCabinetState.Open, "clearing blocker restores full opening");
        door.TryInteract(out _); door.AdvanceMotion(.05f, door.doorCollider.bounds.center, true);
        Assert(door.State == ApartmentCabinetState.Blocked, "tracked head safety sphere stops closing door");
        door.ResetClosed();
    }
    static void CheckQueries(ApartmentFurnitureInteractor owner, ApartmentCabinetDoor door)
    {
        Vector3 front = door.transform.parent.name == "CounterNorth" ? Vector3.back : Vector3.forward;
        // The U-shaped kitchen aisle is only ~1m deep: 1.5m starts inside the opposite cabinet.
        Vector3 center = door.doorCollider.bounds.center; Ray ray = new Ray(center + front * .65f, -front);
        Physics.SyncTransforms(); Assert(owner.TryFirstHit(ray, out var hit) && hit.collider == door.doorCollider, "right ray selects nearest cabinet leaf");
        Assert(ReferenceEquals(ApartmentFurnitureInteractor.FindInteractable(hit.collider), door), "leaf collider resolves to hinge interface");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Temporary ray occluder";
        wall.transform.position = center + front * .35f; wall.transform.localScale = new Vector3(.8f, .9f, .03f); Physics.SyncTransforms();
        Assert(owner.TryFirstHit(ray, out hit) && hit.collider.gameObject == wall && ApartmentFurnitureInteractor.FindInteractable(hit.collider) == null,
            "wall blocks interactive door behind it");
        Assert(!owner.TryFirstHit(new Ray(wall.transform.position, -front), out _), "hand inside solid cannot interact through wall");
        wall.SetActive(false); Physics.SyncTransforms();
        Vector3 isolated = new Vector3(1100f, 1f, 100f); var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.position = isolated + Vector3.forward * 2.5f; box.transform.localScale = Vector3.one * .1f; Physics.SyncTransforms();
        Assert(!owner.TryFirstHit(new Ray(isolated, Vector3.forward), out _), "target beyond two metres rejected");
        box.transform.position = isolated + Vector3.forward; box.GetComponent<BoxCollider>().isTrigger = true; Physics.SyncTransforms();
        Assert(!owner.TryFirstHit(new Ray(isolated, Vector3.forward), out _), "trigger-only fixtures do not occlude or select");
        box.SetActive(false); Physics.SyncTransforms();
    }
    static void CheckHighlight(ApartmentFurnitureInteractor owner, ApartmentCabinetDoor door, ApartmentCabinetDoor other)
    {
        var renderer = door.doorRenderer; Color shared = renderer.sharedMaterial.GetColor("_Color");
        var block = new MaterialPropertyBlock(); block.SetFloat("_Glossiness", .77f); block.SetColor("_Color", new Color(.7f,.3f,.5f)); renderer.SetPropertyBlock(block);
        typeof(ApartmentFurnitureInteractor).GetField("hover", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, door);
        typeof(ApartmentFurnitureInteractor).GetMethod("ApplyHighlight", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        Assert(renderer.sharedMaterial.GetColor("_Color") == shared, "highlight never edits shared material");
        var otherBlock = new MaterialPropertyBlock(); other.doorRenderer.GetPropertyBlock(otherBlock);
        Assert(otherBlock.isEmpty, "other doors are not highlighted");
        typeof(ApartmentFurnitureInteractor).GetMethod("ClearHover", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        renderer.GetPropertyBlock(block); Assert(Mathf.Abs(block.GetFloat("_Glossiness") - .77f) < .001f && block.GetColor("_Color") == new Color(.7f,.3f,.5f),
            "clearing hover restores original property overrides"); renderer.SetPropertyBlock(null);
    }
    static void RenderHints(ApartmentFurnitureInteractor owner)
    {
        typeof(ApartmentFurnitureInteractor).GetMethod("EnsureVisuals", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        var hint = owner.GetComponentsInChildren<Canvas>(true).Single(); hint.gameObject.SetActive(true);
        var text = hint.GetComponentInChildren<TextMeshProUGUI>();
        foreach (string prompt in new[] { ApartmentCabinetDoor.OpenPrompt, ApartmentCabinetDoor.ClosePrompt,
            ApartmentCabinetDoor.OpeningPrompt, ApartmentCabinetDoor.ClosingPrompt, ApartmentCabinetDoor.BlockedPrompt,
            ApartmentCabinetDoor.PausedPrompt })
        {
            text.text = prompt; text.ForceMeshUpdate();
            Assert(text.textInfo.characterCount == prompt.Length && text.textInfo.meshInfo[0].vertexCount > 0, "Chinese prompt mesh: " + prompt);
        }
        hint.transform.position = new Vector3(1100, 5, 100); hint.transform.rotation = Quaternion.identity;
        var cameraObject = new GameObject("Temporary hint preview camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = hint.transform.position + Vector3.back; camera.transform.rotation = Quaternion.identity;
        camera.orthographic = true; camera.orthographicSize = .14f; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f,.12f,.16f); camera.cullingMask = 1 << 5;
        var target = new RenderTexture(1024, 256, 24); var image = new Texture2D(1024, 256, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target; hint.worldCamera = camera; text.text = ApartmentCabinetDoor.BlockedPrompt;
            text.ForceMeshUpdate(); UnityEngine.Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0,0,1024,256), 0, 0); image.Apply(); Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/FurnitureHintPreview.png", image.EncodeToPNG()); Assert(true, "mono Chinese hint preview rendered");
        }
        finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
    }
    static void Assert(bool result, string label)
    { if (!result) throw new InvalidOperationException(label); report.passed++; report.checks.Add(label); }
}
#endif
