using System;
using System.Linq;
using Group10.GuidedJourney;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGroup10.ShoppingBag;
using Object = UnityEngine.Object;

// Run with -batchmode -executeMethod KitchenStorageChecks.Verify. All poses are transient.
public static class KitchenStorageChecks
{
    const string ScenePath = "Assets/Scenes/Apartment_Detailed_VR.unity";

    public static void Verify()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch Editor.");
        EditorSceneManager.OpenScene(ScenePath);
        var guide = Object.FindFirstObjectByType<ApartmentGuidedJourney>();
        var kitchen = GameObject.Find("DetailedEnvironment/House/Fixtures/Kitchen").transform;
        var fixtures = kitchen.Find("West counter - twin hob and inset sink");
        Require(fixtures && fixtures.GetComponentsInChildren<Renderer>().Count(r => r.name == "Burner crown") == 2,
            "Expected two burners.");
        Physics.SyncTransforms();
        Require(Physics.Raycast(new Vector3(.59f, 1.12f, 6.8f), Vector3.down, out var hit, .5f, ~0,
            QueryTriggerInteraction.Ignore) && hit.collider.name == "Basin bottom", "Basin opening is obstructed.");
        var doors = kitchen.GetComponentsInChildren<ApartmentCabinetDoor>();
        Require(doors.Length == 6, "Expected six operable cabinet doors.");
        foreach (var door in doors)
        {
            door.ResetClosed();
            ToggleDoor(door, ApartmentCabinetState.Open);
            ToggleDoor(door, ApartmentCabinetState.Closed);
        }
        Require(guide.adjacentStorageDoor && guide.adjacentStorageDoor != guide.practiceCabinet &&
            guide.adjacentStorageDoor.transform.parent == guide.practiceCabinet.transform.parent, "Invalid storage door wiring.");
        var previousMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (BagSize size in Enum.GetValues(typeof(BagSize))) VerifyDelivery(guide, size);
        }
        finally { Physics.simulationMode = previousMode; }
    }

    static void VerifyDelivery(ApartmentGuidedJourney guide, BagSize size)
    {
        var bag = guide.bagBridge.bag;
        Require(bag && bag.TrySetSize(size), "Could not apply scene bag profile " + size);
        bag.transform.SetPositionAndRotation(new Vector3(1.80f, .16f, 5.7f), Quaternion.identity);
        Physics.SyncTransforms();
        ToggleDoor(guide.practiceCabinet, ApartmentCabinetState.Open);
        bool bothDoors = size != BagSize.Small;
        if (bothDoors) ToggleDoor(guide.adjacentStorageDoor, ApartmentCabinetState.Open);

        var state = new JourneyStateMachine();
        var input = new JourneyObservation { Focused = true, TrackingValid = true, Room = JourneyRoom.Kitchen,
            BagHeld = true, CabinetOpen = true };
        while (state.CurrentStage != JourneyStage.KitchenDrop) state.SkipCurrentStage(input);

        // Small uses the left leaf. Larger bags use both leaves and the centre of the cupboard.
        float x = bothDoors ? 1.81f : 1.575f;
        for (int sample = 0; sample <= 54; sample++)
        {
            bag.transform.position = new Vector3(x, .14f, 5.81f + sample * .02f);
            Physics.SyncTransforms();
            CheckClearance(bag);
        }
        bag.transform.position = new Vector3(x, .16f, 6.885f);
        var rigidbody = bag.GetComponent<Rigidbody>();
        Require(!rigidbody.isKinematic && rigidbody.useGravity, "Released bag must use gravity.");
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        rigidbody.WakeUp();
        Physics.SyncTransforms();
        float insideTime = 0;
        for (int tick = 0; tick < 60; tick++)
        {
            Physics.Simulate(.02f);
            insideTime = guide.BagInsideStorageVolume() ? insideTime + .02f : 0f;
        }
        Require(insideTime >= .35f && rigidbody.linearVelocity.magnitude < .03f, "Bag did not settle: " + size);
        Physics.SyncTransforms();
        input.BagHeld = false;
        input.BagInsideDropZone = guide.BagInsideStorageVolume();
        Require(input.BagInsideDropZone && !state.Tick(input) && state.BagReadyToClose, "Release gate failed: " + size);

        ToggleDoor(guide.practiceCabinet, ApartmentCabinetState.Closed);
        input.CabinetOpen = false;
        if (bothDoors)
        {
            input.CabinetClosed = guide.StorageDoorsClosed;
            Require(!input.CabinetClosed && !state.Tick(input), "Second door was still open: " + size);
            ToggleDoor(guide.adjacentStorageDoor, ApartmentCabinetState.Closed);
        }
        input.CabinetClosed = guide.StorageDoorsClosed;
        input.BagInsideDropZone = guide.BagInsideStorageVolume();
        Require(input.BagInsideDropZone && state.Tick(input) && state.CurrentStage == JourneyStage.Measurement,
            "Final close did not start measurement: " + size);
        Debug.Log("G08 VERIFIED " + size + " | 55 approach samples, cabinet fit, door closure and measurement transition");
    }

    static void CheckClearance(ShoppingBag bag)
    {
        var body = bag.BodyCollider;
        foreach (var other in Physics.OverlapBox(body.bounds.center, body.bounds.extents, Quaternion.identity,
            ~0, QueryTriggerInteraction.Ignore))
        {
            if (other.transform.IsChildOf(bag.transform)) continue;
            if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation, other,
                other.transform.position, other.transform.rotation, out _, out float depth) && depth > .001f)
                throw new InvalidOperationException(bag.CurrentSize + " intersects " + other.name + " at " + body.bounds.center);
        }
    }

    static void ToggleDoor(ApartmentCabinetDoor door, ApartmentCabinetState expected)
    {
        Require(door.TryInteract(out string reason), door.name + ": " + reason);
        for (int step = 0; step < 70 && door.IsMoving; step++) door.AdvanceMotion(.05f, Vector3.zero, false);
        Require(door.State == expected, door.name + ": " + door.State + " " + door.BlockedReason);
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
