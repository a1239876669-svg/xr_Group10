using System;
using UnityEngine;

// Observed endpoints only: teleporting across a room never visits the room in between.
public sealed class ApartmentTourProgress
{
    readonly bool[] entered, exited;
    int current = -1, candidate = -2;
    float stableTime;
    public ApartmentTourProgress(int rooms) { entered = new bool[rooms]; exited = new bool[rooms]; }
    public bool Complete { get { if (exited.Length == 0) return false; foreach (bool done in exited) if (!done) return false; return true; } }
    public bool Entered(int room) => entered[room];
    public bool Exited(int room) => exited[room];
    public void Suspend() { current = -1; candidate = -2; stableTime = 0; }
    public void Observe(int room, float deltaTime)
    {
        if (room < -1 || room >= entered.Length || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0) return;
        if (candidate != room) { candidate = room; stableTime = 0; }
        // A long/stalled frame cannot satisfy dwell by itself.
        stableTime += Mathf.Min(deltaTime, .1f);
        if (stableTime < .5f || current == room) return;
        if (current >= 0) exited[current] = entered[current];
        current = room;
        if (room >= 0) entered[room] = true;
    }
}

[Serializable]
public sealed class ApartmentVisitRegion
{
    public string roomName;
    public Bounds[] volumes;
    public bool Contains(Vector3 point, float padding = 0)
    {
        if (volumes == null) return false;
        foreach (var volume in volumes)
        {
            var bounds = volume; bounds.Expand(new Vector3(padding * 2, 0, padding * 2));
            if (bounds.Contains(point)) return true;
        }
        return false;
    }
}

// Fixed, non-physical volumes; they cannot obstruct rays, walking, doors or teleporting.
[DefaultExecutionOrder(10500)]
public sealed class ApartmentTourTracker : MonoBehaviour
{
    public ApartmentMenuController menu;
    public Transform space;
    public ApartmentVisitRegion[] rooms;
    public ApartmentTourProgress Progress { get; private set; }
    public bool NotificationShown { get; set; }
    public bool Complete => Progress != null && Progress.Complete;
    int previousRegion = -1;
    void Awake() { Progress = new ApartmentTourProgress(rooms == null ? 0 : rooms.Length); NotificationShown = false; }
    void OnEnable() { if (menu != null && menu.locomotion != null) menu.locomotion.Teleported += OnRelocated; }
    void OnDisable() { if (menu != null && menu.locomotion != null) menu.locomotion.Teleported -= OnRelocated; Suspend(); }
    void OnApplicationFocus(bool focused) { if (!focused) Suspend(); }
    void OnApplicationPause(bool paused) { if (paused) Suspend(); }
    void OnRelocated(Vector3 _)
    {
        // A recovery reset must not fake a room departure, or sweep through intermediate rooms.
        if (menu.locomotion.LastRelocationReason == ApartmentRelocationReason.SafetyReset) Suspend();
    }
    void Suspend() { Progress?.Suspend(); previousRegion = -1; }
    public int FindRegion(Vector3 point, int previous = -1)
    {
        if (rooms == null || !float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return -1;
        if (previous >= 0 && previous < rooms.Length && rooms[previous].Contains(point, .08f)) return previous;
        for (int i = 0; i < rooms.Length; i++) if (rooms[i].Contains(point)) return i;
        return -1;
    }
    void LateUpdate()
    {
        if (menu == null || space == null || menu.interactor == null || !menu.interactor.RuntimeReady)
        { Suspend(); return; }
        if (menu.HasOpenPanel || (menu.tutorial != null && menu.tutorial.Session.Step != 3)) return;
        var point = space.InverseTransformPoint(menu.locomotion.rig.centerEyeAnchor.position);
        previousRegion = FindRegion(point, previousRegion);
        Progress.Observe(previousRegion, Time.unscaledDeltaTime);
    }
    void OnDrawGizmosSelected()
    {
        if (rooms == null || space == null) return;
        Gizmos.matrix = space.localToWorldMatrix; Gizmos.color = new Color(.1f, .8f, .7f, .7f);
        foreach (var room in rooms) if (room.volumes != null) foreach (var b in room.volumes) Gizmos.DrawWireCube(b.center, b.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
