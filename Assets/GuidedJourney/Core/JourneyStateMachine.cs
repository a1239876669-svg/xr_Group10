using System;

namespace Group10.GuidedJourney
{
    public enum JourneyStage
    {
        WelcomeMovement, EntranceDoor, LivingExplore, BalconyDoor, CabinetPractice,
        ReturnEntrance, BagSelectPickup, CarryKitchen, KitchenDrop, Measurement,
        Feedback, FreeExploration
    }

    public enum JourneyRoom { Unknown, Entrance, Living, Kitchen, Balcony }
    public enum JourneyStageOutcome { Pending, Completed, Skipped }

    /// <summary>A snapshot of real scene state; revision values must only increase after real actions.</summary>
    public struct JourneyObservation
    {
        public bool Focused, TrackingValid, DiscontinuousMove, AimedTeleport;
        public double DeltaTime, PositionX, PositionZ;
        public bool UseReportedWalkDistance;
        public double ReportedWalkDistance;
        public JourneyRoom Room;
        public bool NearBalcony, BasicsFinished;
        public bool EntranceDoorOpen, BalconyDoorOpen, CabinetOpen, CabinetClosed;
        public bool BagHeld, BagInsideDropZone;
        public long SizeSelectionRevision, MeasurementRevision, FeedbackSavedRevision;
    }

    public sealed class JourneyConfig
    {
        public double LivingExplorationMetres = 2.0;
        public double CarryMetres = 0.5;
        public double MinimumMovementMetres = 0.005;
        public double MaximumContinuousStepMetres = 1.0;
        public double MaximumContinuousSpeed = 6.0;
        public double MaximumSampleSeconds = 0.5;

        internal JourneyConfig ValidatedCopy()
        {
            if (!Positive(LivingExplorationMetres) || !Positive(CarryMetres) ||
                !Positive(MinimumMovementMetres) || !Positive(MaximumContinuousStepMetres) ||
                !Positive(MaximumContinuousSpeed) || !Positive(MaximumSampleSeconds) ||
                MinimumMovementMetres >= MaximumContinuousStepMetres)
                throw new ArgumentOutOfRangeException("config", "Journey movement thresholds must be finite and positive.");
            return (JourneyConfig)MemberwiseClone();
        }

        private static bool Positive(double value) { return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value); }
    }

    /// <summary>
    /// Pure state machine: no scene search, XR input, persistence, physics or Unity dependency.
    /// Each Tick can complete at most one stage. Stage 8 is an explicit kitchen drop task,
    /// not a claim that a bag physically fits inside the cabinet.
    /// </summary>
    public sealed class JourneyStateMachine
    {
        private readonly JourneyConfig _config;
        private readonly JourneyStageOutcome[] _outcomes = new JourneyStageOutcome[12];
        private JourneyStage _stage;
        private bool _hasPosition, _previousHeld, _livingVisited, _carrySawLiving;
        private bool _cabinetSawClosed, _cabinetOpenedAfterEntry, _sizeSelected, _dropSawHeld;
        private double _previousX, _previousZ, _sampleDisplacement;
        private long _sizeBaseline, _measurementBaseline, _feedbackBaseline;

        public JourneyStateMachine(JourneyConfig config = null)
        {
            _config = (config ?? new JourneyConfig()).ValidatedCopy();
        }

        public JourneyStage CurrentStage { get { return _stage; } }
        public bool IsFinished { get { return _stage == JourneyStage.FreeExploration; } }
        public bool WasSkipped { get; private set; }
        public bool HelpVisible { get; private set; }
        public bool TutorialCompleted { get { return IsFinished && !WasSkipped; } }
        public bool SizeSelectedThisStage { get { return _sizeSelected; } }
        public bool CabinetOpenedThisStage { get { return _cabinetOpenedAfterEntry; } }
        public double LivingDistance { get; private set; }
        public double CarryDistance { get; private set; }
        public string LastTransitionReason { get; private set; }
        public JourneyStageOutcome StageOutcome(JourneyStage stage) { return _outcomes[(int)stage]; }
        public void ShowHelp() { HelpVisible = true; }
        public void HideHelp() { HelpVisible = false; }

        public bool Tick(JourneyObservation observation)
        {
            if (!observation.Focused || !observation.TrackingValid)
            {
                _hasPosition = false;
                IgnoreUnfocusedRevisions(observation);
                return false;
            }
            double distance = SampleDistance(observation);
            if (IsFinished) return false;
            bool complete = false;
            string reason = null;
            switch (_stage)
            {
                case JourneyStage.WelcomeMovement:
                    complete = observation.BasicsFinished;
                    reason = "Movement basics finished";
                    break;
                case JourneyStage.EntranceDoor:
                    // A visitor already inside should not have to walk back outside.
                    complete = observation.EntranceDoorOpen ||
                        observation.Room == JourneyRoom.Living || observation.Room == JourneyRoom.Kitchen ||
                        observation.Room == JourneyRoom.Balcony;
                    reason = "Entrance opened or visitor already inside";
                    break;
                case JourneyStage.LivingExplore:
                    if (observation.Room == JourneyRoom.Living)
                    {
                        _livingVisited = true;
                        LivingDistance += distance;
                    }
                    complete = _livingVisited && (LivingDistance >= _config.LivingExplorationMetres || observation.NearBalcony);
                    reason = "Living room explored or balcony naturally approached";
                    break;
                case JourneyStage.BalconyDoor:
                    complete = observation.BalconyDoorOpen;
                    reason = "Real balcony sliding door open";
                    break;
                case JourneyStage.CabinetPractice:
                    if (observation.Room == JourneyRoom.Kitchen)
                    {
                        if (observation.CabinetClosed && !observation.CabinetOpen)
                        {
                            if (_cabinetOpenedAfterEntry) complete = true;
                            _cabinetSawClosed = true;
                        }
                        else if (observation.CabinetOpen && !observation.CabinetClosed && _cabinetSawClosed) _cabinetOpenedAfterEntry = true;
                    }
                    reason = "Kitchen cabinet opened and closed during this practice";
                    break;
                case JourneyStage.ReturnEntrance:
                    complete = observation.Room == JourneyRoom.Entrance;
                    reason = "Visitor actually returned to entrance";
                    break;
                case JourneyStage.BagSelectPickup:
                    if (observation.SizeSelectionRevision > _sizeBaseline) _sizeSelected = true;
                    complete = _sizeSelected && observation.BagHeld;
                    reason = "Real size selection followed by real bag hold";
                    break;
                case JourneyStage.CarryKitchen:
                    if (observation.BagHeld)
                    {
                        if (observation.Room == JourneyRoom.Living) _carrySawLiving = true;
                        if (_previousHeld) CarryDistance += distance;
                    }
                    complete = observation.BagHeld && _carrySawLiving &&
                        (CarryDistance >= _config.CarryMetres || (observation.AimedTeleport && observation.DiscontinuousMove &&
                        _previousHeld && _sampleDisplacement >= _config.CarryMetres)) && observation.Room == JourneyRoom.Kitchen;
                    reason = "Bag carried through living room to kitchen";
                    break;
                case JourneyStage.KitchenDrop:
                    // Deliberately independent of the earlier cabinet open/close exercise.
                    if (observation.BagHeld) _dropSawHeld = true;
                    complete = _dropSawHeld && !observation.BagHeld && observation.BagInsideDropZone &&
                        observation.Room == JourneyRoom.Kitchen;
                    reason = "Bag released in the designated kitchen drop area";
                    break;
                case JourneyStage.Measurement:
                    complete = observation.MeasurementRevision > _measurementBaseline;
                    reason = "New valid two-point measurement created";
                    break;
                case JourneyStage.Feedback:
                    // Adapter increments this only after a new marker has complete saved feedback.
                    complete = observation.FeedbackSavedRevision > _feedbackBaseline;
                    reason = "New marker feedback successfully saved";
                    break;
            }
            _previousHeld = observation.BagHeld;
            if (!complete) return false;
            CompleteCurrentStage(observation, reason);
            return true;
        }

        public bool SkipCurrentStage(JourneyObservation observation)
        {
            if (IsFinished) return false;
            _outcomes[(int)_stage] = JourneyStageOutcome.Skipped;
            WasSkipped = true;
            Enter((JourneyStage)((int)_stage + 1), observation, "Practice explicitly skipped");
            return true;
        }

        public void SkipAll(JourneyObservation observation)
        {
            for (int i = (int)_stage; i < (int)JourneyStage.FreeExploration; i++)
                if (_outcomes[i] == JourneyStageOutcome.Pending) _outcomes[i] = JourneyStageOutcome.Skipped;
            WasSkipped = true;
            Enter(JourneyStage.FreeExploration, observation, "Tutorial explicitly skipped; free exploration remains available");
        }

        public void Reset(JourneyObservation observation)
        {
            Array.Clear(_outcomes, 0, _outcomes.Length);
            WasSkipped = false;
            HelpVisible = false;
            LivingDistance = CarryDistance = 0;
            Enter(JourneyStage.WelcomeMovement, observation, "Tutorial restarted");
        }

        private void CompleteCurrentStage(JourneyObservation observation, string reason)
        {
            _outcomes[(int)_stage] = JourneyStageOutcome.Completed;
            Enter((JourneyStage)((int)_stage + 1), observation, reason);
        }

        private void Enter(JourneyStage stage, JourneyObservation observation, string reason)
        {
            _stage = stage;
            LastTransitionReason = reason;
            _sizeBaseline = observation.SizeSelectionRevision;
            _measurementBaseline = observation.MeasurementRevision;
            _feedbackBaseline = observation.FeedbackSavedRevision;
            _livingVisited = false;
            _carrySawLiving = false;
            _cabinetSawClosed = observation.CabinetClosed && !observation.CabinetOpen;
            _cabinetOpenedAfterEntry = false;
            _sizeSelected = false;
            _dropSawHeld = observation.BagHeld;
            _previousHeld = observation.BagHeld;
            if (stage == JourneyStage.LivingExplore) LivingDistance = 0;
            if (stage == JourneyStage.CarryKitchen) CarryDistance = 0;
            _previousX = observation.PositionX;
            _previousZ = observation.PositionZ;
            _hasPosition = ValidPosition(observation) && observation.Focused && observation.TrackingValid;
        }

        private double SampleDistance(JourneyObservation observation)
        {
            bool valid = ValidPosition(observation);
            double dx = observation.PositionX - _previousX;
            double dz = observation.PositionZ - _previousZ;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            _sampleDisplacement = _hasPosition && valid && Finite(distance) ? distance : 0;
            // Prefer locomotion-reported CharacterController.Move displacement: rig compensation
            // during turning and physical head movement must never count as walking.
            if (observation.UseReportedWalkDistance) distance = observation.ReportedWalkDistance;
            bool continuous = _hasPosition && valid && !observation.DiscontinuousMove &&
                observation.DeltaTime > 0 && observation.DeltaTime <= _config.MaximumSampleSeconds && Finite(distance) &&
                distance >= _config.MinimumMovementMetres && distance <= _config.MaximumContinuousStepMetres &&
                distance <= _config.MaximumContinuousSpeed * observation.DeltaTime;
            _previousX = observation.PositionX;
            _previousZ = observation.PositionZ;
            _hasPosition = valid;
            return continuous ? distance : 0;
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        private static bool ValidPosition(JourneyObservation observation)
        {
            return !double.IsNaN(observation.PositionX) && !double.IsInfinity(observation.PositionX) &&
                   !double.IsNaN(observation.PositionZ) && !double.IsInfinity(observation.PositionZ);
        }

        private void IgnoreUnfocusedRevisions(JourneyObservation observation)
        {
            // An action occurring while the headset is unavailable cannot silently finish a task on return.
            if (_stage == JourneyStage.KitchenDrop) _dropSawHeld = false;
            if (_stage == JourneyStage.CabinetPractice)
            {
                _cabinetOpenedAfterEntry = false;
                _cabinetSawClosed = observation.CabinetClosed && !observation.CabinetOpen;
            }
            if (_stage == JourneyStage.BagSelectPickup && !_sizeSelected)
                _sizeBaseline = Math.Max(_sizeBaseline, observation.SizeSelectionRevision);
            if (_stage == JourneyStage.Measurement)
                _measurementBaseline = Math.Max(_measurementBaseline, observation.MeasurementRevision);
            if (_stage == JourneyStage.Feedback)
                _feedbackBaseline = Math.Max(_feedbackBaseline, observation.FeedbackSavedRevision);
        }
    }
}