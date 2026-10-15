using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Development-only Inspector/mouse entry point. Never reads Quest controller buttons.
    public sealed class BagSizeTestControls : MonoBehaviour
    {
        [SerializeField] private ShoppingBag bag;
        [SerializeField] private bool showDesktopPanel = true;
        public void Small() => Select(BagSize.Small);
        public void Medium() => Select(BagSize.Medium);
        public void Large() => Select(BagSize.Large);
        private void Select(BagSize size)
        {
            if (bag != null && !bag.TrySetSize(size)) Debug.Log("Size unchanged: release bag and check profile/root scale.", this);
        }
        [ContextMenu("Test size/Small")] private void TestSmall() => Small();
        [ContextMenu("Test size/Medium")] private void TestMedium() => Medium();
        [ContextMenu("Test size/Large")] private void TestLarge() => Large();
        private void OnGUI()
        {
            if (!showDesktopPanel || bag == null) return;
            GUILayout.BeginArea(new Rect(16, 16, 340, 160), GUI.skin.box);
            GUILayout.Label("Shopping Bag / Phase A (desktop test only)");
            GUILayout.Label($"{bag.CurrentSize} | {(bag.IsHeld ? "Held - release to resize" : "Released")}");
            bool previous = GUI.enabled;
            GUI.enabled = previous && !bag.IsHeld;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Small")) Small();
            if (GUILayout.Button("Medium")) Medium();
            if (GUILayout.Button("Large")) Large();
            GUILayout.EndHorizontal();
            GUI.enabled = previous;
            GUILayout.Label("Use existing left/right Grip near the handle.");
            GUILayout.EndArea();
        }
    }
}
