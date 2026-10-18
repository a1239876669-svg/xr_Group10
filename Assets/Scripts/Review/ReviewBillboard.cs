using UnityEngine;

namespace QuestGuidance.Review
{
    // Presentation only: the stored point, measurement endpoints and marker root never move.
    public sealed class ReviewBillboard : MonoBehaviour
    {
        private Vector3 anchor;
        private bool hasAnchor;

        public bool SetAnchor(Vector3 actualPointOrMidpoint)
        {
            if (!Finite(actualPointOrMidpoint)) return false;
            anchor = actualPointOrMidpoint;
            hasAnchor = true;
            return true;
        }

        public bool PlaceForViewer(Transform viewer)
        {
            if (viewer == null || !Finite(viewer.position)) return false;
            if (!hasAnchor)
            {
                // Existing callers without an anchor keep their original orientation-only behavior.
                transform.rotation = viewer.rotation;
                return true;
            }

            Vector3 towardViewer = viewer.position - anchor;
            Vector3 offset = towardViewer.sqrMagnitude > .000001f
                ? towardViewer.normalized * .06f : Vector3.zero;
            Vector3 position = anchor + offset + Vector3.up * .08f;
            if (!Finite(position)) return false;
            Vector3 facing = position - viewer.position;
            if (facing.sqrMagnitude < .000001f) facing = viewer.forward;
            if (!Finite(facing) || facing.sqrMagnitude < .000001f) return false;
            // TMP's readable front faces opposite its forward axis, matching the previous billboard.
            Vector3 up = Mathf.Abs(Vector3.Dot(facing.normalized, Vector3.up)) > .99f
                ? Vector3.forward : Vector3.up;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing, up));
            return true;
        }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) PlaceForViewer(camera.transform);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
