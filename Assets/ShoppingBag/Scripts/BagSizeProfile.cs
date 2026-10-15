using UnityEngine;

namespace XRGroup10.ShoppingBag
{
    public enum BagSize { Small, Medium, Large }

    [CreateAssetMenu(menuName = "Shopping Bag/Size Profile")]
    public sealed class BagSizeProfile : ScriptableObject
    {
        [Tooltip("Bag body width, height and depth in metres; handle is additional height.")]
        [SerializeField] private Vector3 bodySize = new Vector3(.45f, .40f, .22f);
        [Min(.01f), SerializeField] private float handleHeight = .12f;
        [Min(.005f), SerializeField] private float handleThickness = .02f;

        public Vector3 BodySize => bodySize;
        public float HandleHeight => handleHeight;
        public float HandleThickness => handleThickness;
        public bool IsValid => Positive(bodySize.x) && Positive(bodySize.y) && Positive(bodySize.z)
            && Positive(handleHeight) && Positive(handleThickness)
            && handleThickness < bodySize.x * .5f && handleThickness < handleHeight;

        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
