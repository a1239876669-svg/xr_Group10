using Oculus.Interaction.Input;
using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Editor mouse panel only. No new controller, keyboard, locomotion, camera or input bindings.
    public sealed class BagDesktopTestDriver : MonoBehaviour
    {
        [SerializeField] private ShoppingBag bag;
        [SerializeField] private BagContactSensor sensor;
        private Vector3 home;
        private void Start() { if (bag != null) home = bag.transform.position; }
        private void OnDisable() { if (bag != null) bag.EndDesktopSimulation(); }
        private void OnApplicationFocus(bool focused) { if (!focused && bag != null) bag.EndDesktopSimulation(); }
        private void OnGUI()
        {
            if (!Application.isEditor || bag == null) return;
            GUILayout.BeginArea(new Rect(16, 190, 360, 300), GUI.skin.box);
            GUILayout.Label("Phase B / Desktop simulation (not VR acceptance)");
            GUILayout.Label("Fixed route: A gap 0.38m, B gap 0.52m.");
            GUILayout.Label("Small clears both; Medium contacts A; Large A+B.");
            GUILayout.Label("Keep body centred, upright; fixed route along Z.");
            if (!bag.IsHeld)
            {
                if (GUILayout.Button("Simulate LEFT hold")) bag.TryBeginDesktopSimulation(Handedness.Left);
                if (GUILayout.Button("Simulate RIGHT hold")) bag.TryBeginDesktopSimulation(Handedness.Right);
            }
            else if (bag.DesktopSimulationActive)
            {
                if (GUILayout.Button("Release simulation")) bag.EndDesktopSimulation();
                float z = GUILayout.HorizontalSlider(bag.transform.position.z, 1.2f, 4.1f);
                bag.transform.position = new Vector3(0, .75f, z);
                Physics.SyncTransforms(); if (sensor != null) sensor.ScanNow();
            }
            if (GUILayout.Button("Reset position (release first)") && !bag.IsHeld)
            { bag.transform.SetPositionAndRotation(home, Quaternion.identity); }
            GUILayout.Label($"Contacts: {(sensor != null ? sensor.ContactCount : 0)} | owner: {bag.HeldHand}");
            GUILayout.Label("Enable Scene Gizmos; select ContactSensor to see bounds.");
            GUILayout.Label("No hardware haptics are sent by simulated holds.");
            GUILayout.EndArea();
        }
    }
}
