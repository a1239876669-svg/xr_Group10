using System;

namespace XRGroup10.ShoppingBag
{
    [Flags]
    public enum BagInteractionBlockers
    {
        None = 0,
        Menu = 1 << 0,
        Tutorial = 1 << 1,
        Teleport = 1 << 2,
        Tracking = 1 << 3,
        InputFocus = 1 << 4,
        VrFocus = 1 << 5,
        TextEditing = 1 << 6,
        ApplicationFocus = 1 << 7,
        Paused = 1 << 8,
        MenuIntent = 1 << 9,
        TeleportIntent = 1 << 10,
        ContextUnavailable = 1 << 11,
        InputUnavailable = 1 << 12,
        Disabled = 1 << 13
    }

    // Implement on the apartment side. Read current state synchronously, not a LateUpdate cache.
    // Tutorial means !AllowInteraction; Menu means BlocksWorldInput, including neutral recovery.
    public interface IBagInteractionContext
    {
        BagInteractionBlockers Blockers { get; }
    }

    public struct BagGripInputSnapshot
    {
        public int Frame;
        public BagInteractionBlockers Blockers;
        public bool LeftTracked, RightTracked;
        public bool LeftGrip, RightGrip;
    }

    // Test seam and input adapter contract. Unknown data must be blocked, never a neutral sample.
    public interface IBagGripInputSource
    {
        BagGripInputSnapshot Read();
    }
}
