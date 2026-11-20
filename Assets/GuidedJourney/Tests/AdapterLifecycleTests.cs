using System;
using System.Reflection;
using Group10.GuidedJourney;
using NUnit.Framework;
using UnityEngine;

namespace Group10.GuidedJourney.Tests
{
    // Reflection keeps the test/core assembly independent of the default apartment assembly.
    // These require Unity EditMode; they are not executed by the offline pure-core runner.
    public sealed class AdapterLifecycleTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        Type Adapter => Type.GetType("ApartmentGuidedJourney, Assembly-CSharp", true);
        static JourneyObservation Ready() => new JourneyObservation { Focused = true, TrackingValid = true,
            DeltaTime = .1, Room = JourneyRoom.Kitchen, CabinetClosed = true };
        static JourneyStateMachine At(JourneyStage target, JourneyObservation observation)
        {
            var result = new JourneyStateMachine();
            while (result.CurrentStage != target) result.SkipCurrentStage(observation);
            return result;
        }
        [Test] public void NumberedTaskTransitionsUseOneShortCenteredCueAndSkipAllIsSilent()
        {
            var go = new GameObject("Task cue test");
            try
            {
                var adapter = go.AddComponent(Adapter);
                var observation = Ready();
                var session = At(JourneyStage.Measurement, observation);
                Adapter.GetField("<Journey>k__BackingField", Private).SetValue(adapter, session);
                Adapter.GetField("previousStage", Private).SetValue(adapter, session.CurrentStage);
                var present = Adapter.GetMethod("PresentStageChange", Private);
                void Refresh() => present.Invoke(adapter, new object[] { observation });
                Refresh();
                Assert.IsNull(go.GetComponent<AudioSource>(), "Showing or redrawing the initial task is silent.");

                observation.MeasurementRevision++;
                Assert.IsTrue(session.Tick(observation));
                Refresh();
                var source = go.GetComponent<AudioSource>();
                Assert.IsNotNull(source);
                Assert.IsFalse(source.playOnAwake);
                Assert.That(source.spatialBlend, Is.Zero);
                Assert.That(source.panStereo, Is.Zero);
                Assert.IsNotNull(source.clip);
                Assert.That(source.clip.channels, Is.EqualTo(1));
                Assert.That(source.clip.length, Is.EqualTo(.2f).Within(.005f));
                observation.FeedbackSavedRevision++;
                Assert.IsTrue(session.Tick(observation));
                Refresh();
                Assert.That(go.GetComponents<AudioSource>(), Is.EqualTo(new[] { source }));

                UnityEngine.Object.DestroyImmediate(source);
                Refresh();
                Assert.IsNull(go.GetComponent<AudioSource>(), "A second frame on the new task is silent.");
                session.Reset(observation);
                Refresh();
                session.SkipCurrentStage(observation);
                observation.TrackingValid = false;
                Refresh();
                observation.TrackingValid = true;
                Refresh();
                Assert.IsNull(go.GetComponent<AudioSource>(), "Tracking recovery must not replay an earlier update.");
                session.SkipCurrentStage(observation);
                Refresh();
                Assert.IsNotNull(go.GetComponent<AudioSource>(), "Skipping one task announces the next one.");
                UnityEngine.Object.DestroyImmediate(go.GetComponent<AudioSource>());
                Adapter.GetField("skippedFromWelcome", Private).SetValue(adapter, true);
                session.SkipAll(observation);
                Refresh();
                Assert.IsNull(go.GetComponent<AudioSource>());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void ApplicationFocusCallbackInvalidatesHeldDropProof()
        {
            var go = new GameObject("Guide lifecycle test");
            try
            {
                var adapter = go.AddComponent(Adapter);
                var observation = Ready(); observation.BagHeld = true;
                var journey = At(JourneyStage.KitchenDrop, observation); journey.Tick(observation);
                Adapter.GetField("<Journey>k__BackingField", Private).SetValue(adapter, journey);
                Adapter.GetField("lastObservation", Private).SetValue(adapter, observation);
                Adapter.GetMethod("OnApplicationFocus", Private).Invoke(adapter, new object[] { false });
                observation.BagHeld = false; observation.BagInsideDropZone = true;
                Assert.IsFalse(journey.Tick(observation));
                Assert.AreEqual(JourneyStage.KitchenDrop, journey.CurrentStage);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void DisablingObserverInvalidatesHalfCompletedCabinetCycle()
        {
            var go = new GameObject("Guide cabinet lifecycle test");
            try
            {
                var adapter = (Behaviour)go.AddComponent(Adapter);
                var observation = Ready(); var journey = At(JourneyStage.CabinetPractice, observation);
                observation.CabinetClosed = false; observation.CabinetOpen = true; journey.Tick(observation);
                Adapter.GetField("<Journey>k__BackingField", Private).SetValue(adapter, journey);
                Adapter.GetField("lastObservation", Private).SetValue(adapter, observation);
                adapter.enabled = false;
                // EditMode may not dispatch lifecycle callbacks for a non-ExecuteAlways component.
                Adapter.GetMethod("OnDisable", Private).Invoke(adapter, null);
                observation.CabinetOpen = false; observation.CabinetClosed = true;
                adapter.enabled = true;
                Assert.IsFalse(journey.Tick(observation));
                Assert.AreEqual(JourneyStage.CabinetPractice, journey.CurrentStage);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
