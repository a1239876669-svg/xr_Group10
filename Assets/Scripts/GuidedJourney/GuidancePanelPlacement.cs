using UnityEngine;

// Only moves a tutorial Canvas. Never writes to a camera, rig, player or controller.
public sealed class GuidancePanelPlacement
{
    bool placed;
    Vector3 placedHead, placedForward;
    float nextRecenter;
    public void Reset() { placed = false; }
    public static float DistanceForWidth(float metres) => Mathf.Clamp(metres / (2f * Mathf.Tan(17f * Mathf.Deg2Rad)), 1.3f, 1.6f);
    public static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    public static Quaternion FaceViewer(Vector3 head, Vector3 panel) => Quaternion.LookRotation((panel-head).normalized, Vector3.up);
    public bool Place(Transform head, RectTransform panel, Vector3 fallback, float now, bool force = false)
    {
        if (head == null || panel == null || !Finite(head.position) || !Finite(head.forward)) return false;
        Vector3 direction = panel.position-head.position;
        bool moved = (head.position-placedHead).sqrMagnitude > .45f*.45f;
        bool turned = Vector3.Angle(head.forward,placedForward) > 30f;
        bool unsafeDistance = direction.magnitude < .85f || direction.magnitude > 2.1f;
        bool behind = placed && Vector3.Dot(head.forward,direction.normalized) < .2f;
        if (placed && !force && (now < nextRecenter || !(moved || turned || unsafeDistance || behind))) return false;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector3.ProjectOnPlane(fallback,Vector3.up).normalized;
        if (forward.sqrMagnitude < .5f) forward = Vector3.forward;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(head.forward.y,-1,1))*Mathf.Rad2Deg,-35f,35f);
        forward = Quaternion.AngleAxis(-pitch,Vector3.Cross(Vector3.up,forward)) * forward;
        float distance = DistanceForWidth(panel.sizeDelta.x*.001f);
        Vector3 position = head.position+forward*distance-Vector3.up*.10f;
        // Undo only this Canvas's inherited scale/rotation, never its owner or XR hierarchy.
        if (panel.parent != null) panel.SetParent(null,true);
        panel.localScale = Vector3.one*.001f;
        panel.SetPositionAndRotation(position,FaceViewer(head.position,position));
        placed = true; placedHead = head.position; placedForward = head.forward; nextRecenter = now+.6f;
        var canvas=panel.GetComponent<Canvas>(); if(canvas!=null)canvas.worldCamera=head.GetComponent<Camera>();
        return true;
    }
}
