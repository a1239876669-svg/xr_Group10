using System;
using System.Reflection;
using Group10.GuidedJourney;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TMPro;
using Object=UnityEngine.Object;
namespace Group10.GuidedJourney.Tests
{
    public sealed class ToolHintDisplayTests
    {
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        GameObject go;Type guideType,toolsType,language;Component guide,tools;object originalJourney;
        void Call(string method,params object[] args)=>guideType.GetMethod(method,Any).Invoke(guide,args);
        string VisibleText(){string result="";foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude))if(text.transform.root.name=="Current review tool (no collider)" || text.transform.IsChildOf(go.transform))result+=text.text+"\n";return result;}
        [SetUp]public void Begin()
        {
            language=Type.GetType("ApartmentLanguage, Assembly-CSharp",true);language.GetMethod("BeginRun",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            go=new GameObject("Isolated tool HUD");var menuType=Type.GetType("ApartmentMenuController, Assembly-CSharp",true);var menu=go.AddComponent(menuType);
            menuType.GetField("font").SetValue(menu,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Apartment/Fonts/VRStandaloneUI-SC.asset"));menuType.GetField("panelShader").SetValue(menu,Shader.Find("Apartment/TutorialOverlay"));menuType.GetField("textOverlayShader").SetValue(menu,Shader.Find("TextMeshPro/Distance Field Overlay"));
            toolsType=Type.GetType("QuestGuidance.Review.ReviewTools, Assembly-CSharp",true);tools=go.AddComponent(toolsType);guideType=Type.GetType("ApartmentGuidedJourney, Assembly-CSharp",true);guide=go.AddComponent(guideType);guideType.GetField("menu").SetValue(guide,menu);guideType.GetField("review").SetValue(guide,tools);originalJourney=guideType.GetProperty("Journey").GetValue(guide);
        }
        [TearDown]public void End(){Object.DestroyImmediate(go);language.GetMethod("BeginRun",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);}
        [Test] public void EntryCardRequiresAcknowledgementAndConsumesItsOwnRay()
        {
            var cardType = Type.GetType("JourneyTaskCard, Assembly-CSharp", true);
            int rearms = 0;
            var menu = guideType.GetField("menu").GetValue(guide);
            var instance = Activator.CreateInstance(cardType, Any, null,
                new object[] { go.transform, menu, (Action)(() => rearms++), (Action)(() => Assert.Fail("Entrance must not dismiss the guide")) }, null);
            using ((IDisposable)instance)
            {
                var panel = cardType.GetField("panel", Any).GetValue(instance);
                var root = (RectTransform)panel.GetType().GetProperty("Root").GetValue(panel);
                void Draw() => cardType.GetMethod("Draw", Any).Invoke(instance, new object[] {
                    go.transform, Vector3.forward, "2 / 11", "Explore the living room.", "Explore." });
                bool Point(Vector3 point, bool press) => (bool)cardType.GetMethod("Point", Any).Invoke(instance,
                    new object[] { new Ray(go.transform.position, (point-go.transform.position).normalized), 2f, press, Vector3.zero });
                string State() => cardType.GetProperty("Current", Any).GetValue(instance).ToString();
                Draw();
                Assert.IsFalse(Point(root.position, true), "The entrance card has no fold interaction");
                cardType.GetMethod("Introduce", Any).Invoke(instance, null);
                Draw();
                Assert.IsTrue(Point(root.position, true), "Introduction consumes clicks away from the button");
                Assert.AreEqual("Introduction", State());
                var confirm = (RectTransform)root.Find("Acknowledge");
                Assert.IsTrue(Point(confirm.TransformPoint(confirm.rect.center), true));
                Assert.AreEqual("Compact", State());
                Assert.IsTrue(Point(root.position, true), "Animating card absorbs a repeat press");
                cardType.GetField("started", Any).SetValue(instance, Time.unscaledTime - 1f);
                Draw();
                Vector3 offset = root.position - go.transform.position;
                Assert.That(offset.x, Is.EqualTo(0f).Within(.001f));
                Assert.That(Mathf.Atan2(offset.y, offset.z) * Mathf.Rad2Deg, Is.EqualTo(10f).Within(.1f));
                Assert.Less(root.localScale.x, .0008f);
                Assert.IsFalse(Point(root.position + root.right, false), "World input passes outside the compact card");
                Assert.IsTrue(Point(root.position, false));
                Draw();
                var edge = root.Find("Border 0").GetComponent("Image");
                var tint = (Color)edge.GetType().GetProperty("color").GetValue(edge);
                Assert.Greater(tint.g, .9f);
                Assert.IsTrue(Point(root.position, true));
                Assert.AreEqual("Expanded", State());
                Assert.IsTrue(Point(root.position + root.right, false), "Expanded UI blocks world clicks");
                cardType.GetField("started", Any).SetValue(instance, Time.unscaledTime - 1f);
                Draw();
                Assert.That(root.position.y - go.transform.position.y, Is.EqualTo(-.10f).Within(.001f));
                Assert.GreaterOrEqual(rearms, 3);
            }
        }

        [TestCase(false, 0)]
        [TestCase(true, 0)]
        [TestCase(false, 1)]
        [TestCase(true, 1)]
        public void FinalCardCanBeDismissedAndStaysHiddenAcrossMenuToggle(bool expand, int languageIndex)
        {
            var choice = Type.GetType("ApartmentDisplayLanguage, Assembly-CSharp", true);
            language.GetMethod("SetLanguage").Invoke(null, new[] { Enum.ToObject(choice, languageIndex) });
            var session = new JourneyStateMachine();
            while (!session.IsFinished) session.SkipCurrentStage(new JourneyObservation());
            guideType.GetField("<Journey>k__BackingField", Any).SetValue(guide, session);
            Call("EnsureCard");
            var card = guideType.GetField("card", Any).GetValue(guide);
            var cardType = card.GetType();
            var panel = cardType.GetField("panel", Any).GetValue(card);
            var root = (RectTransform)panel.GetType().GetProperty("Root").GetValue(panel);
            string copy = (string)guideType.GetMethod("ShortInstruction", Any).Invoke(guide, null);
            string title = (string)guideType.GetProperty("StageTitle").GetValue(guide);
            void Draw()
            {
                cardType.GetMethod("ShowFinalTask", Any).Invoke(card, null);
                cardType.GetMethod("Draw", Any).Invoke(card, new object[] { go.transform, Vector3.forward, title, copy, copy });
            }
            bool Point(Vector3 point, bool press) => (bool)guideType.GetMethod("PointAtTaskCard").Invoke(guide,
                new object[] { new Ray(go.transform.position, point - go.transform.position), 2f, press, Vector3.zero });
            Draw();
            Assert.AreEqual("Compact", cardType.GetProperty("Current", Any).GetValue(card).ToString());
            if (expand)
            {
                Assert.IsTrue(Point(root.position, true));
                cardType.GetField("started", Any).SetValue(card, Time.unscaledTime - 1f);
                Draw();
                Assert.AreEqual("Expanded", cardType.GetProperty("Current", Any).GetValue(card).ToString());
            }
            bool chinese = languageIndex == 1;
            Assert.AreEqual(chinese ? "11 / 11 - 自由探索" : "11 / 11 - Free exploration", root.Find("Stage").GetComponent<TMP_Text>().text);
            Assert.AreEqual(chinese ? "你可以继续自由探索。想退出时，按右手 B 键打开菜单，选择「退出游戏」。" :
                "You can keep exploring. To leave, press B on the right controller and choose “Exit” from the menu.",
                root.Find("Current task").GetComponent<TMP_Text>().text);
            Assert.AreEqual(chinese ? "知道了" : "Got it", root.Find("Acknowledge label").GetComponent<TMP_Text>().text);
            Assert.IsFalse(root.Find("Card controls").gameObject.activeSelf);
            var button = (RectTransform)root.Find("Acknowledge");
            Vector3 target = button.TransformPoint(button.rect.center);
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(Point(target, false));
            Assert.IsTrue(root.gameObject.activeSelf);
            Assert.IsTrue(Point(target, true), "The confirmation must consume this trigger press.");
            Assert.IsFalse(root.gameObject.activeSelf);
            Assert.IsFalse((bool)guideType.GetProperty("TaskCardModal").GetValue(guide));

            var menu = guideType.GetField("menu").GetValue(guide);
            var menuType = menu.GetType();
            menuType.GetProperty("InputReady").SetValue(menu, true);
            var toggle = menuType.GetMethod("Toggle");
            toggle.Invoke(menu, null);
            Assert.IsTrue((bool)menuType.GetProperty("IsOpen").GetValue(menu));
            toggle.Invoke(menu, null);
            Assert.IsFalse((bool)menuType.GetProperty("HasOpenPanel").GetValue(menu));
            Assert.IsFalse((bool)guideType.GetField("showTaskCard").GetValue(guide));
            Assert.IsFalse(Point(target, false), "World controls can receive input again after the menu closes.");
        }

        [Test] public void GuidedToolCompletionClosesOnlyTheCompletedPractice()
        {
            var observation = new JourneyObservation { Focused = true, TrackingValid = true };
            var session = new JourneyStateMachine();
            while (session.CurrentStage != JourneyStage.Measurement) session.SkipCurrentStage(observation);
            guideType.GetField("<Journey>k__BackingField", Any).SetValue(guide, session);
            void Tick() => Call("AdvanceJourney", observation);
            void Tool(string action) => toolsType.GetMethod(action).Invoke(tools, null);
            string Mode() => toolsType.GetProperty("Mode").GetValue(tools).ToString();
            Tool("BeginMeasurement");
            Tool("CancelPending"); Tick();
            Assert.AreEqual(JourneyStage.Measurement, session.CurrentStage);
            Tool("StopTool"); Tick();
            Assert.AreEqual(JourneyStage.Measurement, session.CurrentStage);
            Tool("BeginMeasurement"); observation.MeasurementRevision++; Tick();
            Assert.AreEqual(JourneyStage.Feedback, session.CurrentStage);
            Assert.AreEqual("None", Mode());

            Tool("BeginMarking");
            var menu = guideType.GetField("menu").GetValue(guide);
            var active = menu.GetType().GetProperty("ActivePanel");
            active.SetValue(menu, Enum.Parse(active.PropertyType, "Menu"));
            menu.GetType().GetProperty("Page").SetValue(menu, 1);
            Tick();
            Assert.AreEqual("Menu", active.GetValue(menu).ToString(), "No successful save yet");
            Assert.AreEqual(JourneyStage.Feedback, session.CurrentStage);
            observation.FeedbackSavedRevision++; Tick();
            Assert.AreEqual(JourneyStage.FreeExploration, session.CurrentStage);
            Assert.AreEqual("Closed", active.GetValue(menu).ToString());
            Assert.AreEqual("None", Mode());
            Tool("BeginMeasurement"); observation.MeasurementRevision++; Tick();
            Assert.AreEqual("Measure", Mode(), "Measure continuously after the guide");
        }

        [Test]public void ActiveMeasurementRetainsSmallReadableHintAndLanguageRefresh()
        {
            var toolPlacement = guideType.GetField("toolPlacement", Any).GetValue(guide);
            Assert.That(toolPlacement.GetType().GetProperty("AboveGaze").GetValue(toolPlacement), Is.True);
            toolsType.GetMethod("BeginMeasurement").Invoke(tools, null);
            Call("UpdateToolHint", true);
            StringAssert.Contains("Use the right trigger to select the start and end points.", VisibleText());

            var choice = Type.GetType("ApartmentDisplayLanguage, Assembly-CSharp", true);
            language.GetMethod("SetLanguage").Invoke(null, new[] { Enum.ToObject(choice, 1) });
            StringAssert.Contains("用右手食指扳机依次选择起点和终点。", VisibleText());
            Assert.AreSame(originalJourney, guideType.GetProperty("Journey").GetValue(guide));
        }
        [Test]public void MarkerAndToolStopChangeOnlyPresentationAndKeepJourney()
        {
            toolsType.GetMethod("BeginMarking").Invoke(tools,null);Call("UpdateToolHint",true);StringAssert.Contains("Place a marker and submit your feedback.",VisibleText());
            Call("UpdateToolHint",false);Assert.AreEqual("",VisibleText());toolsType.GetMethod("StopTool").Invoke(tools,null);Call("UpdateToolHint",true);Assert.AreEqual("",VisibleText());Assert.AreSame(originalJourney,guideType.GetProperty("Journey").GetValue(guide));
        }
    }
}
