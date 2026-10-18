using System;
using UnityEngine;

namespace QuestGuidance.Review
{
    [Serializable]
    public class ReviewRoomArea
    {
        public string roomId;
        public Bounds bounds;
    }

    /// <summary>Ordered regions in map-local coordinates; no trigger physics or player position dependency.</summary>
    public sealed class ReviewRoomMap : MonoBehaviour
    {
        public ReviewRoomArea[] areas = Array.Empty<ReviewRoomArea>();
        public string Resolve(Vector3 worldPoint)
        {
            var point = transform.InverseTransformPoint(worldPoint);
            foreach (var area in areas)
                if (area != null && area.bounds.Contains(point)) return area.roomId;
            return "Unassigned";
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            foreach (var area in areas)
                if (area != null) Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }
    }
}
