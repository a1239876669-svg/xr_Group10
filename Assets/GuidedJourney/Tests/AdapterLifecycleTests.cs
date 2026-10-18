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
