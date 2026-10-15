using UnityEngine;

namespace QuestGuidance.Review
{
    public sealed class ReviewBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
