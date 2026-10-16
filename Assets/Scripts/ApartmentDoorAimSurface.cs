using UnityEngine;

// Full visible door outline remains opaque to pointing/teleport rays. The separate solid probe
// fits the authored doorway, whose decorative mesh overlaps its lintel/hinge jamb.
public sealed class ApartmentDoorAimSurface : MonoBehaviour
{
    public BoxCollider surface;
    public static bool IsAimSolid(Collider c) => c != null && (!c.isTrigger || c.GetComponent<ApartmentDoorAimSurface>()?.surface == c);
}
