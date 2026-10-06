using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
public class JoystickLocomotion : MonoBehaviour
{
    [SerializeField] float speed = 2.5f;
    [SerializeField] float deadZone = 0.15f;
    [SerializeField] XRNode moveHand = XRNode.LeftHand;

    CharacterController cc;
    InputDevice device;
    Transform head;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(moveHand);
        if (!head && Camera.main) head = Camera.main.transform;
        if (!device.isValid || !head) return;

        if (!device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return;
        if (stick.magnitude < deadZone) return;

        Vector3 forward = head.forward;
        Vector3 right = head.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * stick.y + right * stick.x;
        cc.SimpleMove(Vector3.ClampMagnitude(move, 1f) * speed);
    }
}
