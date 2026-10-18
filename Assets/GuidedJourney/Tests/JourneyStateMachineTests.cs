using System;
using NUnit.Framework;
using Group10.GuidedJourney;

namespace Group10.GuidedJourney.Tests
{
    public sealed class JourneyStateMachineTests
    {
        private static JourneyObservation Observation()
        {
            return new JourneyObservation { Focused = true, TrackingValid = true, DeltaTime = 0.1, Room = JourneyRoom.Entrance, CabinetClosed = true };
        }

        private static JourneyStateMachine At(JourneyStage stage, ref JourneyObservation observation)
        {
            var machine = new JourneyStateMachine();
            while (machine.CurrentStage != stage) machine.SkipCurrentStage(observation);
            return machine;
        }

        [Test] public void StartsAtWelcomeWithoutCompletingAnything()
        {
            var machine = new JourneyStateMachine();
            Assert.AreEqual(JourneyStage.WelcomeMovement, machine.CurrentStage);
            Assert.AreEqual(JourneyStageOutcome.Pending, machine.StageOutcome(JourneyStage.WelcomeMovement));
            Assert.IsFalse(machine.IsFinished);
        }

        [Test] public void WelcomeRequiresExplicitBasicsCompletion()
        {
            var observation = Observation(); var machine = new JourneyStateMachine();
            Assert.IsFalse(machine.Tick(observation));
            observation.BasicsFinished = true;
            Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.EntranceDoor, machine.CurrentStage);
        }

        [Test] public void OneSnapshotNeverCompletesMultipleStages()
        {
            var observation = Observation(); var machine = new JourneyStateMachine();
            observation.BasicsFinished = observation.EntranceDoorOpen = observation.BalconyDoorOpen = true;
            observation.Room = JourneyRoom.Living; observation.NearBalcony = true;
            machine.Tick(observation);
            Assert.AreEqual(JourneyStage.EntranceDoor, machine.CurrentStage);
            machine.Tick(observation);
            Assert.AreEqual(JourneyStage.LivingExplore, machine.CurrentStage);
        }

        [Test] public void EntranceRequiresRealDoorOrActualInsideRoom()
        {
            var observation = Observation(); var machine = At(JourneyStage.EntranceDoor, ref observation);
            Assert.IsFalse(machine.Tick(observation));
            observation.EntranceDoorOpen = true;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void AlreadyInsideVisitorIsNotForcedOutside()
        {
            var observation = Observation(); var machine = At(JourneyStage.EntranceDoor, ref observation);
            observation.Room = JourneyRoom.Kitchen;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void LivingStationaryRootAndHeadRotationDoNotCount()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living;
            for (int i = 0; i < 100; i++) machine.Tick(observation);
            Assert.AreEqual(0, machine.LivingDistance);
            Assert.AreEqual(JourneyStage.LivingExplore, machine.CurrentStage);
        }

        [Test] public void LivingMicroJitterDoesNotCount()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living;
            for (int i = 0; i < 100; i++) { observation.PositionX = i % 2 == 0 ? 0 : 0.001; machine.Tick(observation); }
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void LivingRealRootMovementCompletesExploration()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living;
            for (int i = 0; i < 5; i++) { observation.PositionX += 0.5; machine.Tick(observation); }
            Assert.AreEqual(JourneyStage.BalconyDoor, machine.CurrentStage);
            Assert.GreaterOrEqual(machine.LivingDistance, 2);
        }

        [Test] public void NearBalconyNaturallyCompletesWithoutDistanceGrinding()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living; observation.NearBalcony = true;
            Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void NearBalconyOutsideLivingDoesNotFakeAnExploration()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.NearBalcony = true;
            Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void TeleportAndResetDoNotInflateDistance()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living; observation.DiscontinuousMove = true; observation.PositionX = 10;
            machine.Tick(observation);
            observation.DiscontinuousMove = false; observation.PositionX = 30;
            machine.Tick(observation);
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void InvalidCoordinatesAndLongPauseDoNotInflateDistance()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living; observation.PositionX = double.NaN; machine.Tick(observation);
            observation.PositionX = 10; machine.Tick(observation);
            observation.DeltaTime = 2; observation.PositionX += 0.5; machine.Tick(observation);
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void MovementOutsideLivingDoesNotCount()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            for (int i = 0; i < 5; i++) { observation.PositionX += 0.5; machine.Tick(observation); }
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void BalconyRequiresOpenDoorRatherThanProximity()
        {
            var observation = Observation(); var machine = At(JourneyStage.BalconyDoor, ref observation);
            observation.NearBalcony = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.BalconyDoorOpen = true;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void CabinetRequiresRealOpenThenCloseInKitchen()
        {
            var observation = Observation(); var machine = At(JourneyStage.CabinetPractice, ref observation);
            observation.Room = JourneyRoom.Kitchen;
            Assert.IsFalse(machine.Tick(observation));
            observation.CabinetOpen = true; observation.CabinetClosed = false; Assert.IsFalse(machine.Tick(observation));
            Assert.IsTrue(machine.CabinetOpenedThisStage);
            observation.CabinetOpen = false; observation.CabinetClosed = true; Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.ReturnEntrance, machine.CurrentStage);
        }

        [Test] public void CabinetAlreadyOpenMustCloseThenOpenAgain()
        {
            var observation = Observation(); observation.CabinetOpen = true; observation.CabinetClosed = false;
            var machine = At(JourneyStage.CabinetPractice, ref observation); observation.Room = JourneyRoom.Kitchen;
            machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true; Assert.IsFalse(machine.Tick(observation));
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true; Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void CabinetStateOutsideKitchenDoesNotCompletePractice()
        {
            var observation = Observation(); var machine = At(JourneyStage.CabinetPractice, ref observation);
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true; Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void ReturnEntranceUsesActualRoomNotARecoveryButton()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen;
            var machine = At(JourneyStage.ReturnEntrance, ref observation);
            observation.DiscontinuousMove = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.Room = JourneyRoom.Entrance;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void OldSelectionAndDefaultSizeDoNotCompletePickup()
        {
            var observation = Observation(); observation.SizeSelectionRevision = 5;
            var machine = At(JourneyStage.BagSelectPickup, ref observation);
            observation.BagHeld = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.SizeSelectionRevision = 6;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void SelectionAndHeldAreBothRequired()
        {
            var observation = Observation(); var machine = At(JourneyStage.BagSelectPickup, ref observation);
            observation.SizeSelectionRevision++;
            Assert.IsFalse(machine.Tick(observation));
            Assert.IsTrue(machine.SizeSelectedThisStage);
            observation.BagHeld = true;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void DroppedBagCanBePickedUpWithoutRestartingSelection()
        {
            var observation = Observation(); var machine = At(JourneyStage.BagSelectPickup, ref observation);
            observation.SizeSelectionRevision++; machine.Tick(observation);
            observation.BagHeld = false; machine.Tick(observation);
            observation.BagHeld = true; machine.Tick(observation);
            Assert.AreEqual(JourneyStage.CarryKitchen, machine.CurrentStage);
        }

        [Test] public void KitchenArrivalWithoutCarryingThroughLivingDoesNotComplete()
        {
            var observation = Observation(); observation.BagHeld = true;
            var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.Room = JourneyRoom.Kitchen; observation.DiscontinuousMove = true;
            Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void CarryUsesHeldDistanceAndLivingToKitchenRoute()
        {
            var observation = Observation(); observation.BagHeld = true;
            var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.Room = JourneyRoom.Living; observation.PositionX = 0.5;
            Assert.IsFalse(machine.Tick(observation));
            observation.Room = JourneyRoom.Kitchen; observation.PositionX = 1;
            Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.KitchenDrop, machine.CurrentStage);
        }

        [Test] public void WalkingWithoutBagDoesNotCountAndPickupCanRecover()
        {
            var observation = Observation(); var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.Room = JourneyRoom.Living;
            for (int i = 0; i < 5; i++) { observation.PositionX += 0.5; machine.Tick(observation); }
            Assert.AreEqual(0, machine.CarryDistance);
            observation.BagHeld = true; machine.Tick(observation);
            observation.PositionX += 0.5; machine.Tick(observation);
            observation.Room = JourneyRoom.Kitchen; observation.PositionX += 0.5;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void KitchenDropRequiresRealHoldAndReleaseInCorrectVolume()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen;
            var machine = At(JourneyStage.KitchenDrop, ref observation);
            observation.BagInsideDropZone = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.BagHeld = true; machine.Tick(observation);
            observation.BagInsideDropZone = false; observation.BagHeld = false;
            Assert.IsFalse(machine.Tick(observation));
            observation.BagInsideDropZone = true;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void EarlierCabinetPracticeNeverCompletesKitchenDrop()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen;
            var machine = At(JourneyStage.KitchenDrop, ref observation);
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true;
            Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void OldMeasurementIsNotReusedAndFreshRevisionCompletes()
        {
            var observation = Observation(); observation.MeasurementRevision = 7;
            var machine = At(JourneyStage.Measurement, ref observation);
            Assert.IsFalse(machine.Tick(observation));
            observation.MeasurementRevision = 6; Assert.IsFalse(machine.Tick(observation));
            observation.MeasurementRevision = 8; Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.Feedback, machine.CurrentStage);
            Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void FeedbackRequiresNewSuccessfulSaveRevision()
        {
            var observation = Observation(); observation.FeedbackSavedRevision = 3;
            var machine = At(JourneyStage.Feedback, ref observation);
            Assert.IsFalse(machine.Tick(observation));
            observation.MeasurementRevision++; Assert.IsFalse(machine.Tick(observation));
            observation.FeedbackSavedRevision++;
            Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.FreeExploration, machine.CurrentStage);
        }

        [Test] public void UnfocusedSnapshotDoesNotAdvanceOrCountMovement()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Focused = false; observation.Room = JourneyRoom.Living;
            observation.PositionX = 10; observation.NearBalcony = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.NearBalcony = false; observation.Focused = true; observation.PositionX = 20;
            Assert.IsFalse(machine.Tick(observation)); Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void TrackingLossNeverAdvancesRealAction()
        {
            var observation = Observation(); var machine = At(JourneyStage.BalconyDoor, ref observation);
            observation.TrackingValid = false; observation.BalconyDoorOpen = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.TrackingValid = true;
            Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void EventDuringFocusLossDoesNotAutoCompleteAfterReturn()
        {
            var observation = Observation(); var machine = At(JourneyStage.Measurement, ref observation);
            observation.Focused = false; observation.MeasurementRevision = 1; machine.Tick(observation);
            observation.Focused = true; Assert.IsFalse(machine.Tick(observation));
            observation.MeasurementRevision = 2; Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void SkipIsRecordedAsSkippedRatherThanCompleted()
        {
            var observation = Observation(); var machine = new JourneyStateMachine();
            machine.SkipCurrentStage(observation);
            Assert.AreEqual(JourneyStageOutcome.Skipped, machine.StageOutcome(JourneyStage.WelcomeMovement));
            machine.SkipAll(observation);
            Assert.IsTrue(machine.IsFinished); Assert.IsTrue(machine.WasSkipped); Assert.IsFalse(machine.TutorialCompleted);
            Assert.AreEqual(JourneyStageOutcome.Skipped, machine.StageOutcome(JourneyStage.Feedback));
        }

        [Test] public void FreeExplorationDoesNotRepeatOrDisableAnything()
        {
            var observation = Observation(); var machine = new JourneyStateMachine(); machine.SkipAll(observation);
            Assert.IsFalse(machine.Tick(observation)); Assert.IsFalse(machine.SkipCurrentStage(observation));
            machine.ShowHelp(); Assert.IsTrue(machine.HelpVisible);
            machine.HideHelp(); Assert.IsFalse(machine.HelpVisible);
            Assert.AreEqual(JourneyStage.FreeExploration, machine.CurrentStage);
        }

        [Test] public void RestartClearsSkippedAndDistanceState()
        {
            var observation = Observation(); var machine = At(JourneyStage.LivingExplore, ref observation);
            observation.Room = JourneyRoom.Living; observation.PositionX = 0.5; machine.Tick(observation);
            machine.Reset(observation);
            Assert.AreEqual(JourneyStage.WelcomeMovement, machine.CurrentStage);
            Assert.IsFalse(machine.WasSkipped); Assert.AreEqual(0, machine.LivingDistance);
            Assert.AreEqual(JourneyStageOutcome.Pending, machine.StageOutcome(JourneyStage.EntranceDoor));
        }

        [Test] public void FullRealSequenceCompletesWithoutAnySkip()
        {
            var observation = Observation(); var machine = new JourneyStateMachine();
            observation.BasicsFinished = true; machine.Tick(observation);
            observation.EntranceDoorOpen = true; machine.Tick(observation);
            observation.Room = JourneyRoom.Living; observation.NearBalcony = true; machine.Tick(observation);
            observation.BalconyDoorOpen = true; machine.Tick(observation);
            observation.Room = JourneyRoom.Kitchen; observation.CabinetOpen = false; observation.CabinetClosed = true; machine.Tick(observation);
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true; machine.Tick(observation);
            observation.Room = JourneyRoom.Entrance; machine.Tick(observation);
            observation.SizeSelectionRevision = 1; observation.BagHeld = true; machine.Tick(observation);
            observation.Room = JourneyRoom.Living; observation.PositionX = 0.5; machine.Tick(observation);
            observation.Room = JourneyRoom.Kitchen; observation.PositionX = 1; machine.Tick(observation);
            observation.BagHeld = false; observation.BagInsideDropZone = true; machine.Tick(observation);
            observation.MeasurementRevision = 1; machine.Tick(observation);
            observation.FeedbackSavedRevision = 1; machine.Tick(observation);
            Assert.IsTrue(machine.TutorialCompleted); Assert.IsFalse(machine.WasSkipped);
            for (int i = 0; i < 11; i++) Assert.AreEqual(JourneyStageOutcome.Completed, machine.StageOutcome((JourneyStage)i));
        }

        [Test] public void AllTeleportCarryRouteUsesRealLivingAndKitchenEndpoints()
        {
            var observation = Observation(); observation.BagHeld = true;
            var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.DiscontinuousMove = observation.AimedTeleport = true;
            observation.Room = JourneyRoom.Living; observation.PositionX = 3; machine.Tick(observation);
            Assert.AreEqual(0, machine.CarryDistance);
            observation.Room = JourneyRoom.Kitchen; observation.PositionX = 6;
            Assert.IsTrue(machine.Tick(observation)); Assert.AreEqual(0, machine.CarryDistance);
        }

        [Test] public void AimedTeleportDirectToKitchenWithoutLivingDoesNotComplete()
        {
            var observation = Observation(); observation.BagHeld = true;
            var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.DiscontinuousMove = observation.AimedTeleport = true;
            observation.Room = JourneyRoom.Kitchen; observation.PositionX = 6;
            Assert.IsFalse(machine.Tick(observation));
        }

        [Test] public void SafetyResetNeverSubstitutesForTheCarryingRoute()
        {
            var observation = Observation(); observation.BagHeld = true;
            var machine = At(JourneyStage.CarryKitchen, ref observation);
            observation.Room = JourneyRoom.Living; machine.Tick(observation);
            observation.DiscontinuousMove = true; observation.AimedTeleport = false;
            observation.Room = JourneyRoom.Kitchen; observation.PositionX = 6;
            Assert.IsFalse(machine.Tick(observation)); Assert.AreEqual(0, machine.CarryDistance);
        }

        [Test] public void FocusRecoveryDoesNotCountAnAutomaticBagReleaseAsDrop()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen; observation.BagHeld = true;
            var machine = At(JourneyStage.KitchenDrop, ref observation);
            observation.Focused = false; observation.BagHeld = false; observation.BagInsideDropZone = true;
            machine.Tick(observation);
            observation.Focused = true; Assert.IsFalse(machine.Tick(observation));
            observation.BagHeld = true; machine.Tick(observation);
            observation.BagHeld = false; Assert.IsTrue(machine.Tick(observation));
        }

        [Test] public void FocusRecoveryDoesNotCountAnUnobservedCabinetClose()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen;
            var machine = At(JourneyStage.CabinetPractice, ref observation);
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.Focused = false; observation.CabinetOpen = false; observation.CabinetClosed = true; machine.Tick(observation);
            observation.Focused = true; Assert.IsFalse(machine.Tick(observation));
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true; Assert.IsTrue(machine.Tick(observation));
        }
        [Test] public void ReportedZeroIgnoresTurningCompensationAndHeadMovement()
        {
            var observation = Observation(); observation.UseReportedWalkDistance = true;
            var machine = At(JourneyStage.LivingExplore, ref observation); observation.Room = JourneyRoom.Living;
            for (int i = 0; i < 10; i++) { observation.PositionX += 0.5; machine.Tick(observation); }
            Assert.AreEqual(0, machine.LivingDistance);
            Assert.AreEqual(JourneyStage.LivingExplore, machine.CurrentStage);
        }

        [Test] public void ValidReportedWalkDistanceCountsRatherThanPoseMotion()
        {
            var observation = Observation(); observation.UseReportedWalkDistance = true;
            var machine = At(JourneyStage.LivingExplore, ref observation); observation.Room = JourneyRoom.Living;
            observation.ReportedWalkDistance = 0.2; machine.Tick(observation);
            Assert.AreEqual(0.2, machine.LivingDistance, 0.000001);
        }

        [Test] public void InvalidReportedWalkDistanceIsRejected()
        {
            var observation = Observation(); observation.UseReportedWalkDistance = true;
            var machine = At(JourneyStage.LivingExplore, ref observation); observation.Room = JourneyRoom.Living;
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1, 10 })
            {
                observation.ReportedWalkDistance = invalid; observation.PositionX += 0.1; machine.Tick(observation);
            }
            Assert.AreEqual(0, machine.LivingDistance);
        }

        [Test] public void TeleportCannotAddEvenReportedWalkingDistance()
        {
            var observation = Observation(); observation.UseReportedWalkDistance = true;
            var machine = At(JourneyStage.LivingExplore, ref observation); observation.Room = JourneyRoom.Living;
            observation.ReportedWalkDistance = 0.5; observation.DiscontinuousMove = true; observation.AimedTeleport = true;
            observation.PositionX = 5; machine.Tick(observation);
            Assert.AreEqual(0, machine.LivingDistance);
        }
        [Test] public void CabinetOpeningClosingPausedOrBlockedNeverPretendToBeClosed()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen;
            var machine = At(JourneyStage.CabinetPractice, ref observation);
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            // These real controller states are neither fully open nor fully closed.
            observation.CabinetOpen = false; observation.CabinetClosed = false;
            for (int i = 0; i < 5; i++) Assert.IsFalse(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.CabinetPractice, machine.CurrentStage);
            observation.CabinetClosed = true;
            Assert.IsTrue(machine.Tick(observation));
            Assert.AreEqual(JourneyStage.ReturnEntrance, machine.CurrentStage);
        }

        [Test] public void CabinetUnknownInitialStateIsNotAnObservedClosedState()
        {
            var observation = Observation(); observation.Room = JourneyRoom.Kitchen; observation.CabinetClosed = false;
            var machine = At(JourneyStage.CabinetPractice, ref observation);
            observation.CabinetOpen = true; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true;
            Assert.IsFalse(machine.Tick(observation));
            observation.CabinetOpen = true; observation.CabinetClosed = false; machine.Tick(observation);
            observation.CabinetOpen = false; observation.CabinetClosed = true;
            Assert.IsTrue(machine.Tick(observation));
        }
        [Test] public void InvalidConfigFailsEarlyInsteadOfBreakingProgress()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new JourneyStateMachine(new JourneyConfig { CarryMetres = double.NaN }));
        }
    }
}