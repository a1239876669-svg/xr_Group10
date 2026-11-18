#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRGroup10.ShoppingBag;

// Explicit scene-instance operation called by Phase3SpatialStabilization after its baseline checks.
// Bounds are prototype design choices, not building-code compliance claims. Never saves a scene.
public static class Phase3SpatialDoorAndRoute
{
    public static readonly List<string> LastChecks = new List<string>();
    static T Find<T>(long id) where T : Component => Phase3SpatialStabilization.Find<T>(id);
    static Bounds B(Vector3 min, Vector3 max) => new Bounds((min + max) * .5f, max - min);

    public static void Apply()
    {
        var entrance = Find<ApartmentRoomDoor>(171350835);
        var bath = Find<ApartmentRoomDoor>(1576583254);
        var bed = Find<ApartmentRoomDoor>(899562392);
        if (entrance.gameObject.scene.path != Phase3SpatialStabilization.ScenePath || Application.isPlaying)
            throw new InvalidOperationException("Only the isolated detailed scene in Edit mode is supported.");
        foreach (var door in new[] { entrance, bath, bed }) door.ResetClosed();

        // Widen only the entrance's right jamb: it also clears the existing shoe-cabinet corner.
        SetWorldBounds(Find<Transform>(527504760), B(new Vector3(3.90f, 2.10f, 0), new Vector3(4.90f, 2.80f, .24f)));
        SetWorldBounds(Find<Transform>(962122311), B(new Vector3(4.90f, 0, 0), new Vector3(5.46f, 2.80f, .24f)));
        ConfigureDoor(entrance, new Vector3(3.903f, 1.05f, -.025f), 90,
            B(new Vector3(3.906f, .003f, -.0475f), new Vector3(4.897f, 2.097f, -.0025f)));
        SetWorldBounds(Find<Transform>(1897844607), B(new Vector3(3.85f, -.02f, -.33f), new Vector3(4.95f, 0, .01f)));

        // Enlarge the bathroom north return by 100 mm; trim both tiled returns to the same opening.
        SetWorldBounds(Find<Transform>(331741831), B(new Vector3(3.08f, 2.10f, 3.13f), new Vector3(3.28f, 2.80f, 4.05f)));
        SetWorldBounds(Find<Transform>(846168696), B(new Vector3(3.08f, 0, 4.05f), new Vector3(3.28f, 2.80f, 4.99f)));
        SetWorldBounds(Find<Transform>(1373652525), B(new Vector3(3.045f, 0, 3.0428f), new Vector3(3.075f, 2.20f, 3.13f)));
        SetWorldBounds(Find<Transform>(1030523185), B(new Vector3(3.045f, 0, 4.05f), new Vector3(3.075f, 2.20f, 4.72035f)));
        // Recess the hinge to the finished room face. The earlier x=3.075 candidate hit Tile_E2.
        ConfigureDoor(bath, new Vector3(3.025f, 1.05f, 3.155f), -90,
            B(new Vector3(3.005f, .015f, 3.133f), new Vector3(3.045f, 2.097f, 4.047f)));

        bed.openAngle = -90;
        EditorUtility.SetDirty(bed);
        bed.ResetClosed();
        // These exact, local surfaces participate independently; no whole-house or search registration.
        foreach (long id in new long[] { 527504760, 962122311, 1747743412, 331741831, 846168696, 1373652525, 1030523185, 470823404 })
            RegisterContactSurface(Find<Transform>(id));
        Physics.SyncTransforms();
    }

    public static void SetWorldBounds(Transform target, Bounds wanted)
    {
        if (Quaternion.Angle(target.rotation, Quaternion.identity) > .01f || target.GetComponentsInChildren<MeshFilter>(true).Length != 1)
            throw new InvalidOperationException("Expected one unrotated scene mesh: " + target.name);
        Bounds current = Phase3SpatialStabilization.VisualBounds(target);
        if (current.size.x < .00001f || current.size.y < .00001f || current.size.z < .00001f || wanted.size.x <= 0 || wanted.size.y <= 0 || wanted.size.z <= 0)
            throw new InvalidOperationException("Invalid bounds: " + target.name);
        target.localScale = Vector3.Scale(target.localScale, new Vector3(wanted.size.x / current.size.x, wanted.size.y / current.size.y, wanted.size.z / current.size.z));
        target.position += wanted.center - Phase3SpatialStabilization.VisualBounds(target).center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
        var actual = Phase3SpatialStabilization.VisualBounds(target);
        if (Vector3.Distance(actual.min, wanted.min) > .0001f || Vector3.Distance(actual.max, wanted.max) > .0001f)
            throw new InvalidOperationException("World bounds fitting failed: " + target.name);
        Physics.SyncTransforms();
    }

    static void ConfigureDoor(ApartmentRoomDoor door, Vector3 hinge, float angle, Bounds leafBounds)
    {
        door.transform.position = hinge;
        door.closedLocalRotation = Quaternion.identity;
        door.transform.localRotation = Quaternion.identity;
        var leaf = door.doorCollider.transform;
        leaf.localRotation = Quaternion.identity;
        SetWorldBounds(leaf, leafBounds);
        var mesh = leaf.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var local = new Bounds(vertices[0], Vector3.zero);
        foreach (var point in vertices) local.Encapsulate(point);
        door.doorCollider.center = local.center;
        door.doorCollider.size = local.size;
        var aim = leaf.GetComponent<ApartmentDoorAimSurface>();
        if (aim == null || aim.surface == null || !aim.surface.isTrigger)
            throw new InvalidOperationException("Missing original door aiming surface: " + door.name);
        aim.surface.center = local.center;
        aim.surface.size = local.size;
        door.openAngle = angle;
        door.ResetClosed();
        foreach (var item in new UnityEngine.Object[] { door, door.transform, leaf, door.doorCollider, aim.surface }) EditorUtility.SetDirty(item);
        if (door.ConfigurationError() != null) throw new InvalidOperationException(door.name + ": " + door.ConfigurationError());
    }

    static void RegisterContactSurface(Transform target)
    {
        var solids = target.GetComponents<Collider>().Where(c => c.enabled && !c.isTrigger).ToArray();
        var renderer = target.GetComponent<Renderer>();
        if (solids.Length != 1 || renderer == null) throw new InvalidOperationException("Expected one explicit solid/renderer: " + target.name);
        var all = Phase3SpatialStabilization.All<BagContactTarget>();
        var component = target.GetComponent<BagContactTarget>();
        foreach (var existing in all.Where(t => t != component))
        {
            var data = new SerializedObject(existing).FindProperty("furnitureColliders");
            for (int i = 0; i < data.arraySize; i++)
                if (data.GetArrayElementAtIndex(i).objectReferenceValue == solids[0])
                    throw new InvalidOperationException("Surface already belongs to a different contact target: " + target.name);
        }
        Material warning = all.Select(t => new SerializedObject(t).FindProperty("warningMaterial").objectReferenceValue as Material).FirstOrDefault(m => m != null);
        if (warning == null) throw new InvalidOperationException("No existing shopping-bag warning material.");
        if (component == null) component = Undo.AddComponent<BagContactTarget>(target.gameObject);
        component.Configure(solids, new[] { renderer }, warning);
        EditorUtility.SetDirty(component);
    }

    public static List<string> CheckDoors()
    {
        LastChecks.Clear();
        var failures = new List<string>();
        var doors = Phase3SpatialStabilization.All<ApartmentCabinetDoor>();
        foreach (var door in doors) door.ResetClosed();
        foreach (var door in doors)
        {
            GameObject obstruction = null;
            try
            {
                string error = door.ConfigurationError();
                if (error != null) throw new InvalidOperationException(error);
                if (!door.TryInteract(out error)) throw new InvalidOperationException(error);
                Advance(door);
                if (door.State != ApartmentCabinetState.Open) throw new InvalidOperationException("Opening stopped: " + door.State + " " + door.BlockedReason);
                if (door.name == "Door_Entrance_Hinge")
                {
                    float clear = Find<Collider>(962122315).bounds.min.x - Mathf.Max(Find<Collider>(258165237).bounds.max.x, door.doorCollider.bounds.max.x);
                    if (clear < .85f) throw new InvalidOperationException("Entrance open clearance below .85 m: " + clear);
                    LastChecks.Add("Entrance opened solid clearance=" + clear.ToString("F6") + "m");
                }
                if (door.name == "Door_Bath_Hinge")
                {
                    float lower = Mathf.Max(Find<Collider>(1373652529).bounds.max.z, Find<Collider>(93272002).bounds.max.z, door.doorCollider.bounds.max.z);
                    float upper = Mathf.Min(Find<Collider>(1030523189).bounds.min.z, Find<Collider>(846168700).bounds.min.z);
                    float clear = upper - lower;
                    if (clear < .85f) throw new InvalidOperationException("Bathroom open clearance below .85 m: " + clear);
                    LastChecks.Add("Bathroom opened finished clearance=" + clear.ToString("F6") + "m");
                }
                if (!door.TryInteract(out error)) throw new InvalidOperationException(error);
                Advance(door);
                if (door.State != ApartmentCabinetState.Closed) throw new InvalidOperationException("Closing stopped: " + door.State + " " + door.BlockedReason);
                LastChecks.Add(door.name + " real AdvanceMotion complete open and close");

                // A temporary same-scene blocker at a future leaf centre checks stop/retry semantics.
                Quaternion pivot = (door.transform.parent != null ? door.transform.parent.rotation : Quaternion.identity) * door.closedLocalRotation * Quaternion.AngleAxis(Mathf.Sign(door.openAngle) * 12f, Vector3.up);
                Quaternion rotation = pivot * door.doorCollider.transform.localRotation;
                Vector3 future = door.transform.position + pivot * door.doorCollider.transform.localPosition + rotation * Vector3.Scale(door.doorCollider.center, door.doorCollider.transform.lossyScale);
                obstruction = new GameObject("Phase3 temporary door stop probe");
                if (obstruction.scene != door.gameObject.scene) SceneManager.MoveGameObjectToScene(obstruction, door.gameObject.scene);
                obstruction.transform.position = future;
                obstruction.AddComponent<BoxCollider>().size = Vector3.one * .06f;
                Physics.SyncTransforms();
                if (!door.TryInteract(out error)) throw new InvalidOperationException(error);
                Advance(door);
                if (door.State != ApartmentCabinetState.Blocked) throw new InvalidOperationException("Temporary obstruction did not block door.");
                UnityEngine.Object.DestroyImmediate(obstruction); obstruction = null;
                Physics.SyncTransforms();
                if (!door.TryInteract(out error)) throw new InvalidOperationException(error);
                Advance(door);
                if (door.State != ApartmentCabinetState.Open) throw new InvalidOperationException("Door did not resume after blocker removal: " + door.BlockedReason);
                LastChecks.Add(door.name + " temporary collider blocks; fresh TryInteract resumes");
            }
            catch (Exception exception) { failures.Add(door.name + ": " + exception.Message); }
            finally
            {
                if (obstruction != null) UnityEngine.Object.DestroyImmediate(obstruction);
                door.ResetClosed();
                Physics.SyncTransforms();
            }
        }
        return failures;
    }

    static void Advance(ApartmentCabinetDoor door)
    {
        for (int i = 0; i < 1000 && door.IsMoving; i++) door.AdvanceMotion(.02f, Vector3.zero, false);
    }
}
#endif
