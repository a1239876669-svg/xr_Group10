// Room doors share the already tested hinge, collision and input-loss behavior of cabinet doors.
public sealed class ApartmentRoomDoor : ApartmentCabinetDoor
{
    public override ApartmentInteractionKind HintCategory => ApartmentInteractionKind.RoomDoor;
}
