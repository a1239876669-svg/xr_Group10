using Oculus.Interaction.Input;

namespace XRGroup10.ShoppingBag
{
    // No Unity lifecycle or frame cache: every candidate query may add a blocker in the same frame.
    public sealed class BagGripRearmState
    {
        private sealed class HandState
        {
            public bool Armed;
            public int BlockedFrame = -1, NeutralFrame = -1;
            public void Block(int frame)
            {
                Armed = false;
                BlockedFrame = System.Math.Max(BlockedFrame, frame);
                NeutralFrame = -1;
            }
            public void Observe(int frame, bool blocked, bool tracked, bool pressed)
            {
                if (blocked || !tracked) { Block(frame); return; }
                if (!pressed && !Armed && frame > BlockedFrame)
                { Armed = true; NeutralFrame = frame; }
            }
            // A release and a select in the same frame cannot rearm a queued SDK selection.
            public bool Allows(int frame) => Armed && frame > BlockedFrame && frame > NeutralFrame;
        }

        private readonly HandState left = new HandState(), right = new HandState();
        private int newestFrame = -1;
        public void BlockAll(int frame) { left.Block(frame); right.Block(frame); }
        public void BlockHand(Handedness hand, int frame)
        {
            if (hand == Handedness.Left) left.Block(frame);
            else if (hand == Handedness.Right) right.Block(frame);
        }
        public void Observe(BagGripInputSnapshot sample)
        {
            // Reject stale/out-of-order samples instead of allowing them to rearm either hand.
            if (sample.Frame < newestFrame) { BlockAll(newestFrame); return; }
            newestFrame = sample.Frame;
            bool blocked = sample.Blockers != BagInteractionBlockers.None;
            left.Observe(sample.Frame, blocked, sample.LeftTracked, sample.LeftGrip);
            right.Observe(sample.Frame, blocked, sample.RightTracked, sample.RightGrip);
        }
        public bool CanAcquire(Handedness hand, int frame)
        {
            if (frame != newestFrame) return false;
            return hand == Handedness.Left ? left.Allows(frame) :
                hand == Handedness.Right && right.Allows(frame);
        }
    }
}
