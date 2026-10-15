using UnityEngine;

namespace QuestGuidance.Review
{
    /// <summary>Replaceable input adapter. Future menu can disable shortcuts and supply its own ray.</summary>
    [RequireComponent(typeof(ReviewTools))]
    public sealed class ReviewPointerInput : MonoBehaviour
    {
        [SerializeField] private OVRCameraRig rig;
        [SerializeField] private Transform[] targetRoots;
        [SerializeField] private Material pointerMaterial;
        [Tooltip("Temporary shortcuts: left Y switches tools; right B cancels. Disable when the menu is connected.")]
        [SerializeField] private bool developmentShortcuts = true;
        [SerializeField] private float maximumDistance = 30f;
        [SerializeField] private LayerMask surfaceLayers = Physics.DefaultRaycastLayers;
        private ReviewTools tools;
        private LineRenderer pointer;
        private bool inputBlocked;

        public void SetInputBlocked(bool blocked) { inputBlocked = blocked; }

        private void Awake()
        {
            tools = GetComponent<ReviewTools>();
            var visual = new GameObject("Review pointer");
            visual.layer = 2;
            visual.transform.SetParent(transform, false);
            pointer = visual.AddComponent<LineRenderer>();
            pointer.sharedMaterial = pointerMaterial;
            pointer.startColor = pointer.endColor = Color.cyan;
            pointer.startWidth = pointer.endWidth = 0.003f;
            pointer.positionCount = 2;
            pointer.enabled = false;
        }

        private void Update()
        {
            pointer.enabled = false;
            tools.PreviewPoint(default, false);
            if (inputBlocked) return;
            bool xr = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
            if (!xr || rig == null || rig.rightControllerAnchor == null) return;
            if (developmentShortcuts)
            {
                if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
                    tools.SetMode((ReviewMode)(((int)tools.Mode + 1) % 3));
                if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
                {
                    if (tools.HasPendingPoint) tools.CancelPending();
                    else
                    {
                        if (tools.SelectedId != null) tools.CancelMarker(tools.SelectedId);
                        tools.StopTool();
                    }
                }
            }
            var source = rig.rightControllerAnchor;
            Ray ray = new Ray(source.position, source.forward);
            bool confirm = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            RaycastHit hit;
            bool valid = TryGetSurface(ray, out hit);
            string markerId = FindMarkerOnRay(ray, valid ? hit.distance : maximumDistance);
            if (confirm && markerId != null && tools.Mode != ReviewMode.Measure) { tools.SelectMarker(markerId); return; }
            if (tools.Mode == ReviewMode.None && markerId == null) return;
            pointer.enabled = true;
            pointer.startColor = pointer.endColor = valid ? Color.cyan : Color.gray;
            pointer.SetPosition(0, ray.origin);
            pointer.SetPosition(1, valid ? hit.point : ray.GetPoint(maximumDistance));
            tools.PreviewPoint(hit.point, valid);
            if (valid && confirm && tools.Mode != ReviewMode.None) tools.ConfirmPoint(hit.point, hit.transform);
        }

        public string FindMarkerOnRay(Ray ray, float surfaceDistance)
        {
            return tools.FindMarkerOnRay(ray, surfaceDistance, maximumDistance);
        }

        public bool TryGetSurface(Ray ray, out RaycastHit result)
        {
            result = default;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, maximumDistance, surfaceLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= nearest || !IsTarget(hit.transform)) continue;
                nearest = hit.distance;
                result = hit;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private bool IsTarget(Transform target)
        {
            if (target.IsChildOf(transform) || (rig != null && target.IsChildOf(rig.transform))) return false;
            if (targetRoots == null || targetRoots.Length == 0) return false;
            foreach (var root in targetRoots)
                if (root != null && target.IsChildOf(root)) return true;
            return false;
        }

        private void OnDisable()
        {
            if (pointer != null) pointer.enabled = false;
            if (tools != null) tools.PreviewPoint(default, false);
        }
    }
}
