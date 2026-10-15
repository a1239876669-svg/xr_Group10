using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    // Integration entry points only: does not inspect or change the apartment's input owners.
    public sealed class BagInteractionGate : MonoBehaviour
    {
        [SerializeField] private ShoppingBag bag;
        private bool menu, tutorial, teleport, lostFocus, paused;
        private void OnEnable() { if (bag == null) bag = GetComponent<ShoppingBag>(); Apply(); }
        private void OnDisable() { if (bag != null) bag.SetAcquisitionAllowed(false); }
        public void SetMenuOpen(bool value) { menu=value; Apply(); }
        public void SetTutorialOpen(bool value) { tutorial=value; Apply(); }
        public void SetTeleportAiming(bool value) { teleport=value; Apply(); }
        private void OnApplicationFocus(bool value) { lostFocus=!value; Apply(); }
        private void OnApplicationPause(bool value) { paused=value; Apply(); }
        private void Apply() { if (bag != null) bag.SetAcquisitionAllowed(isActiveAndEnabled && !(menu || tutorial || teleport || lostFocus || paused)); }
    }
}
