using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TMPro;
using Object=UnityEngine.Object;

namespace Group10.GuidedJourney.Tests
{
    public sealed class SessionVisualsTests
    {
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        Type toolsType,sessionType,recordType;
        GameObject first,second;
        Component tools;
        Material material;
        string directory,path;
        static int events;
        static void Count<T>(T record) {events++;}
        object Call(Component target,string name,params object[] args)=>toolsType.GetMethod(name,Any).Invoke(target,args);
        object Value(Component target,string name)=>toolsType.GetProperty(name).GetValue(target);
        object[] Records(Component target,string property)=>((IEnumerable)Value(target,property)).Cast<object>().ToArray();
        static object Field(object target,string name)=>target.GetType().GetField(name).GetValue(target);
        static void Put(object target,string name,object value)=>target.GetType().GetField(name).SetValue(target,value);
        Component Create(GameObject go)
        {
            var result=go.AddComponent(toolsType);
            toolsType.GetField("autoSave",Any).SetValue(result,false);
            toolsType.GetField("visualMaterial",Any).SetValue(result,material);
            toolsType.GetProperty("DisplayFont").SetValue(result,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Apartment/Fonts/VRStandaloneUI-SC.asset"));
            Call(result,"ConfigureStorageForTests",path);
            return result;
        }
        object Record(string id,string kind,int number,int state,Vector3 point)
        {
            var result=Activator.CreateInstance(recordType);
            Put(result,"id",id);Put(result,"kind",kind);Put(result,"number",number);Put(result,"scene",first.scene.path);
            Put(result,"point",point);Put(result,"endPoint",kind=="measurement"?point+Vector3.right:point);
            Put(result,"distanceMetres",kind=="measurement"?1f:0f);Put(result,"state",Enum.ToObject(recordType.GetField("state").FieldType,state));
            Put(result,"observation",state==1?"Original resident text 原文":"");Put(result,"everydayImpact",state==1?"Original impact":"");
            return result;
        }
        void Seed()
        {
            var session=Activator.CreateInstance(sessionType);Put(session,"scenePath",first.scene.path);
            var list=(IList)Field(session,"records");
            list.Add(Record("old-measure","measurement",1,1,Vector3.zero));
            var marker=Record("old-feedback","marker",2,1,new Vector3(0,0,2));Put(marker,"measurementId","old-measure");list.Add(marker);
            list.Add(Record("old-draft","marker",3,0,new Vector3(0,0,4)));
            Put(session,"nextNumber",4);
            File.WriteAllText(path,JsonUtility.ToJson(session,true));
        }
        string AddMarker(Component target)
        {
            Call(target,"BeginMarking");Assert.IsTrue((bool)Call(target,"ConfirmPoint",new Vector3(3,0,2),null));
            return (string)Field(Records(target,"ActiveRecords").Last(),"id");
        }
        string AddMeasurement(Component target)
        {
            Call(target,"BeginMeasurement");Assert.IsTrue((bool)Call(target,"ConfirmPoint",Vector3.zero,null));
            Assert.IsTrue((bool)Call(target,"ConfirmPoint",Vector3.right,null));
            return (string)Field(Records(target,"ActiveRecords").Last(),"id");
        }
        [SetUp] public void Setup()
        {
            toolsType=Type.GetType("QuestGuidance.Review.ReviewTools, Assembly-CSharp",true);
            sessionType=Type.GetType("QuestGuidance.Review.ReviewSession, Assembly-CSharp",true);
            recordType=Type.GetType("QuestGuidance.Review.ReviewRecord, Assembly-CSharp",true);
            directory=Path.Combine(Path.GetTempPath(),"Group10-SessionTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"current.json");
            material=new Material(Shader.Find("Unlit/Color"));first=new GameObject("Isolated session test");tools=Create(first);events=0;
        }
        [TearDown] public void Teardown()
        {
            if(first!=null)Object.DestroyImmediate(first);if(second!=null)Object.DestroyImmediate(second);if(material!=null)Object.DestroyImmediate(material);
            string full=Path.GetFullPath(directory);string root=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("Group10-SessionTests-"))Directory.Delete(full,true);
        }
        [Test] public void RestoreKeepsHistoryButCreatesNoVisualsOrCompletionEvents()
        {
            Seed();var before=File.ReadAllBytes(path);
            foreach(string name in new[]{"RecordCreated","FeedbackSaved"})
            {
                var e=toolsType.GetEvent(name);var handler=typeof(SessionVisualsTests).GetMethod("Count",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(recordType);
                e.AddEventHandler(tools,Delegate.CreateDelegate(e.EventHandlerType,handler));
            }
            Assert.IsTrue((bool)Call(tools,"RestoreSession"));Assert.AreEqual(3,Records(tools,"Records").Length);Assert.AreEqual(0,Records(tools,"ActiveRecords").Length);
            Assert.AreEqual(0,first.transform.childCount);Assert.AreEqual(0,events);Assert.AreEqual(0L,Value(tools,"FeedbackSaveRevision"));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void HiddenHistoricalDraftDoesNotBlockNewMarkerOrCaptureRay()
        {
            Seed();Call(tools,"RestoreSession");string id=AddMarker(tools);
            Assert.AreNotEqual("old-draft",id);Assert.AreEqual(4,Records(tools,"Records").Length);Assert.IsNull(Call(tools,"FindMarkerOnRay",new Ray(Vector3.zero,Vector3.forward),10f,30f));
        }
        [Test] public void CancelUnfinishedMeasurementCreatesNoRecord()
        {
            Seed();Call(tools,"RestoreSession");Call(tools,"BeginMeasurement");Call(tools,"ConfirmPoint",Vector3.zero,null);Assert.IsTrue((bool)Value(tools,"HasPendingPoint"));
            Call(tools,"CancelPending");Assert.IsFalse((bool)Value(tools,"HasPendingPoint"));Assert.AreEqual(3,Records(tools,"Records").Length);Assert.AreEqual(0,Records(tools,"ActiveRecords").Length);
        }
        [Test] public void UndoMeasurementOnlyHidesAndRetainsLinkedHistoryAndDisk()
        {
            Seed();Call(tools,"RestoreSession");string id=AddMeasurement(tools);Call(tools,"SaveSession");var before=File.ReadAllBytes(path);
            Assert.IsTrue((bool)Call(tools,"UndoLastMeasurementDisplay"));Assert.IsFalse((bool)Call(tools,"IsRecordVisible",id));Assert.IsNotNull(Call(tools,"FindRecord",id));
            Assert.AreEqual("old-measure",Field(Call(tools,"FindRecord","old-feedback"),"measurementId"));CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
        }
        [Test] public void HideAllMeasurementDisplaysKeepsCurrentMarkerAndAllData()
        {
            Seed();Call(tools,"RestoreSession");AddMeasurement(tools);string marker=AddMarker(tools);AddMeasurement(tools);string before=(string)Call(tools,"ToJson");
            Call(tools,"HideMeasurementDisplays");Assert.AreEqual(before,Call(tools,"ToJson"));Assert.AreEqual(1,Records(tools,"ActiveRecords").Length);Assert.IsTrue((bool)Call(tools,"IsRecordVisible",marker));
        }
        [Test] public void CancelCurrentDraftCannotDeleteSavedOrHistoricalFeedback()
        {
            Seed();Call(tools,"RestoreSession");Assert.IsFalse((bool)Call(tools,"CancelCurrentDraft","old-feedback"));Assert.IsFalse((bool)Call(tools,"CancelCurrentDraft","old-draft"));
            string id=AddMarker(tools);Assert.IsTrue((bool)Call(tools,"CancelCurrentDraft",id));Assert.IsNull(Call(tools,"FindRecord",id));Assert.AreEqual(3,Records(tools,"Records").Length);
        }
        [Test] public void SaveNewFeedbackPreservesHistoryAndNextRunStartsClean()
        {
            Seed();Call(tools,"RestoreSession");string id=AddMarker(tools);var importance=Enum.ToObject(recordType.GetField("importance").FieldType,1);
            Assert.IsTrue((bool)Call(tools,"SubmitFeedback",id,"My unchanged text 原文","Daily impact",importance,"old-measure"));
            Assert.AreEqual(1L,Value(tools,"FeedbackSaveRevision"));second=new GameObject("Next isolated run");var next=Create(second);Call(next,"RestoreSession");
            Assert.AreEqual(4,Records(next,"Records").Length);Assert.AreEqual(0,Records(next,"ActiveRecords").Length);Assert.AreEqual("My unchanged text 原文",Field(Call(next,"FindRecord",id),"observation"));
            AddMeasurement(next);Call(next,"SaveSession");Assert.AreEqual(5,Records(next,"Records").Length);
        }
        [Test] public void ExplicitShowIsIdempotentAndUndoDoesNotDeleteSavedMarker()
        {
            Seed();Call(tools,"RestoreSession");Call(tools,"ShowRecord","old-feedback");Call(tools,"ShowRecord","old-feedback");Assert.AreEqual(1,Records(tools,"ActiveRecords").Length);
            string json=(string)Call(tools,"ToJson");Call(tools,"UndoLast");Assert.AreEqual(json,Call(tools,"ToJson"));Assert.AreEqual(0,Records(tools,"ActiveRecords").Length);
        }
        Component ExitMenu()
        {
            var menu = first.AddComponent(Type.GetType("ApartmentMenuController, Assembly-CSharp", true));
            var review = first.AddComponent(Type.GetType("ApartmentReviewController, Assembly-CSharp", true));
            Put(review, "tools", tools);
            Put(review, "menu", menu);
            Put(menu, "review", review);
            Put(menu, "font", Value(tools, "DisplayFont"));
            Put(menu, "panelShader", Shader.Find("Apartment/TutorialOverlay"));
            Put(menu, "textOverlayShader", Shader.Find("TextMeshPro/Distance Field Overlay"));
            review.GetType().GetMethod("OnEnable", Any).Invoke(review, null);
            menu.GetType().GetProperty("InputReady").SetValue(menu, true);
            menu.GetType().GetMethod("BuildPreview").Invoke(menu, new object[] { 2 });
            ExitClick(menu, "Tab 5");
            return menu;
        }

        static RectTransform ExitRoot(Component menu) => (RectTransform)menu.GetType().GetProperty("PanelRect").GetValue(menu);

        static void ExitClick(Component menu, string name)
        {
            var button = ExitRoot(menu).Find(name) as RectTransform;
            Assert.IsNotNull(button, name);
            var centre = button.TransformPoint(button.rect.center);
            var ray = new Ray(centre - button.forward, button.forward);
            menu.GetType().GetMethod("ProcessPointer").Invoke(menu, new object[] { ray, 3f, true, Vector3.zero, 0 });
        }

        [Test] public void ExitChecklistSeparatesHistorySkipsCancelledAndReturnsFromLocationAndEditing()
        {
            Seed();
            Call(tools, "RestoreSession");
            string removed = AddMarker(tools);
            Call(tools, "CancelCurrentDraft", removed);
            string measure = AddMeasurement(tools);
            string draft = AddMarker(tools);
            var menu = ExitMenu();
            string Copy(string name) => ExitRoot(menu).Find(name).GetComponent<TMP_Text>().text;
            StringAssert.Contains("#5", Copy("Record details"));
            StringAssert.Contains("2", Copy("Record counts"));
            ExitClick(menu, "Next record");
            StringAssert.Contains("#6", Copy("Record details"));
            ExitClick(menu, "Edit reviewed marker");
            Assert.IsNull(ExitRoot(menu).Find("Rating 0"));
            ExitClick(menu, "Save feedback");
            Assert.AreEqual("Draft", Field(Call(tools, "FindRecord", draft), "state").ToString());
            ExitClick(menu, "Aspect 1");
            ExitClick(menu, "Rating 0");
            ExitClick(menu, "Aspect 2");
            ExitClick(menu, "Save feedback");
            Assert.AreEqual("Draft", Field(Call(tools, "FindRecord", draft), "state").ToString());
            ExitClick(menu, "Rating 2");
            ExitClick(menu, "Save feedback");
            Assert.AreEqual("家具摆放", Field(Call(tools, "FindRecord", draft), "observation"));
            Assert.AreEqual("合理", Field(Call(tools, "FindRecord", draft), "everydayImpact"));
            StringAssert.Contains("#6", Copy("Record details"));
            Assert.AreEqual("Submitted", Field(Call(tools, "FindRecord", draft), "state").ToString());
            ExitClick(menu, "Earlier visits");
            ExitClick(menu, "Next record");
            StringAssert.Contains("Original resident text", Copy("Record details"));
            StringAssert.Contains("#1", Copy("Record details"));
            var before = (string)Call(tools, "ToJson");
            ExitClick(menu, "Inspect location");
            Assert.IsNotNull(first.transform.Find("Inspect #2"));
            menu.GetType().GetMethod("Toggle").Invoke(menu, null);
            Assert.IsNull(first.transform.Find("Inspect #2"));
            Assert.IsFalse((bool)Call(tools, "IsRecordVisible", "old-feedback"));
            Assert.AreEqual(before, Call(tools, "ToJson"));
            Assert.IsTrue((bool)Call(tools, "WasCreatedThisRun", measure));
        }

        [TestCase(0, "Records: 1", "This visit")]
        [TestCase(1, "共 1 条记录", "本次新增")]
        public void ExitChecklistUsesCurrentLanguageAndCountsHiddenMeasurements(int language, string count, string scope)
        {
            var catalog = Type.GetType("ApartmentLanguage, Assembly-CSharp", true);
            var previous = catalog.GetProperty("Current").GetValue(null);
            var set = catalog.GetMethod("SetLanguage");
            try
            {
                set.Invoke(null, new[] {Enum.ToObject(previous.GetType(), language)});
                string id = AddMeasurement(tools);
                Call(tools, "HideRecord", id);
                var menu = ExitMenu();
                StringAssert.Contains(count, ExitRoot(menu).Find("Record counts").GetComponent<TMP_Text>().text);
                Assert.AreEqual(scope, ExitRoot(menu).Find("This visit label").GetComponent<TMP_Text>().text);
            }
            finally { set.Invoke(null, new[] {previous}); }
        }

        [Test] public void ExitChecklistOffersRetryUntilDiskWriteSucceedsAndReceiptPrecedesQuit()
        {
            AddMeasurement(tools);
            var menu = ExitMenu();
            Directory.CreateDirectory(path); // A directory cannot be atomically replaced as a file.
            ExitClick(menu, "Save and exit");
            Assert.IsNotNull(ExitRoot(menu).Find("Save failure"));
            Assert.IsNull(ExitRoot(menu).Find("Confirm quit"));
            Directory.Delete(path);
            ExitClick(menu, "Save and exit");
            Assert.IsTrue(File.Exists(path));
            Assert.IsNotNull(ExitRoot(menu).Find("Save receipt"));
            second = new GameObject("Reader");
            var reader = Create(second);
            Assert.IsTrue((bool)Call(reader, "RestoreSession"));
            Assert.AreEqual(1, Records(reader, "Records").Length);
            ExitClick(menu, "Confirm quit");
            Assert.IsNull(ExitRoot(menu));
        }

        [Test] public void ExportRetainsSubmittedHistoryAndOmitsDrafts()
        {
            Seed();Call(tools,"RestoreSession");Call(tools,"ExportSession");string exported=(string)Value(tools,"LastExportPath");Assert.IsTrue(exported.StartsWith(directory));
            string json=File.ReadAllText(exported);StringAssert.Contains("old-feedback",json);StringAssert.Contains("old-measure",json);StringAssert.DoesNotContain("old-draft",json);
        }
        [Test] public void PreviewReadoutUsesRealPointsWithoutCreatingOrChangingARecord()
        {
            Seed();Call(tools,"RestoreSession");string before=(string)Call(tools,"ToJson");Call(tools,"BeginMeasurement");Call(tools,"ConfirmPoint",Vector3.zero,null);
            Call(tools,"PreviewPoint",new Vector3(0,0,2.5f),true);StringAssert.Contains("2.50",(string)Value(tools,"CurrentToolReadout"));
            Assert.AreEqual(before,Call(tools,"ToJson"));Call(tools,"PreviewPoint",Vector3.zero,false);StringAssert.DoesNotContain("2.50",(string)Value(tools,"CurrentToolReadout"));
            Call(tools,"CancelPending");Assert.AreEqual(before,Call(tools,"ToJson"));
        }
        [Test] public void FinishedDistanceStaysOnTheLineAndInRecordsOnly()
        {
            Call(tools, "BeginMeasurement");
            string pickStart = (string)Value(tools, "CurrentToolReadout");
            string id = AddMeasurement(tools);
            Assert.AreEqual(pickStart, Value(tools, "CurrentToolReadout"));
            Assert.AreEqual(1f, Field(Call(tools, "FindRecord", id), "distanceMetres"));
            Assert.IsTrue(first.GetComponentsInChildren<TMP_Text>().Any(label => label.text == "#1  1.00 m"));
            Call(tools, "UndoLastMeasurementDisplay");
            Assert.AreEqual(pickStart, Value(tools, "CurrentToolReadout"));
            Assert.IsNotNull(Call(tools, "FindRecord", id));
        }

        [Test] public void StartDotAcceptsOneFreshPressAndLeavesNearbyEndpointsMeasurable()
        {
            string id = AddMeasurement(tools);
            var savedDot = first.GetComponentsInChildren<Renderer>().First(renderer => renderer.name == "Point");
            var properties = new MaterialPropertyBlock();
            Color DotColor(Renderer renderer)
            {
                renderer.GetPropertyBlock(properties);
                return properties.GetColor("_Color");
            }
            string before = (string)Call(tools, "ToJson");
            string pickStart = (string)Value(tools, "CurrentToolReadout");
            var latch = Activator.CreateInstance(Type.GetType("ApartmentInteractionTriggerLatch, Assembly-CSharp", true));
            var sample = latch.GetType().GetMethod("Sample");
            bool Trigger(float value) => (bool)sample.Invoke(latch, new object[] { value, true });
            bool Point(Vector3 origin, Vector3 end, bool pressed) =>
                (bool)Call(tools, "PointAtPendingStart", origin, end, pressed);

            Vector3 start = Vector3.forward;
            Trigger(0f);
            Assert.IsTrue(Trigger(1f));
            Call(tools, "ConfirmPoint", start, null);
            var startDot = first.transform.Find("Pending measurement/Point").GetComponent<Renderer>();
            Assert.AreEqual(Color.cyan, DotColor(startDot));
            Assert.IsTrue(Point(Vector3.zero, start, Trigger(1f)));
            Assert.AreEqual(Color.red, DotColor(startDot));
            Assert.AreEqual(Color.cyan, DotColor(savedDot));
            Assert.IsTrue((bool)Value(tools, "HasPendingPoint"));
            var language = Type.GetType("ApartmentLanguage, Assembly-CSharp", true);
            Assert.AreEqual(language.GetMethod("Text").Invoke(null, new object[] { "tool.cancel.start" }),
                Value(tools, "CurrentToolReadout"));
            Assert.IsFalse(Point(Vector3.zero, start * .95f, false), "A nearer surface occludes the dot");
            Call(tools, "PreviewPoint", start * .95f, true);
            Assert.AreEqual(Color.cyan, DotColor(startDot));
            StringAssert.Contains("0.05", (string)Value(tools, "CurrentToolReadout"));
            Trigger(0f);
            Assert.IsTrue(Point(Vector3.zero, start, Trigger(1f)));
            Assert.IsFalse((bool)Value(tools, "HasPendingPoint"));
            Assert.AreEqual(pickStart, Value(tools, "CurrentToolReadout"));
            Assert.AreEqual(before, Call(tools, "ToJson"));
            Assert.IsTrue((bool)Call(tools, "IsRecordVisible", id));

            Call(tools, "ConfirmPoint", start, null);
            Vector3 offset = .013f * Vector3.right;
            Assert.IsFalse(Point(offset, start + offset, true), "Just outside the 12.5 mm radius");
            Call(tools, "PreviewPoint", start + offset, true);
            Assert.IsTrue((bool)Call(tools, "ConfirmPoint", start + offset, null));
            Assert.AreEqual(.013f, (float)Field(Records(tools, "Records").Last(), "distanceMetres"), .00001f);
        }
        [Test] public void MarkerHoverUsesNearestVisibleDotAndReusesOneReadOnlyCard()
        {
            Seed();
            Call(tools, "RestoreSession");
            Call(tools, "ShowRecord", "old-draft");
            Call(tools, "ShowRecord", "old-feedback");
            var submitted = first.transform.Find("marker 2/Point").GetComponent<Renderer>();
            var draft = first.transform.Find("marker 3/Point").GetComponent<Renderer>();
            Color ColorOf(Renderer dot)
            {
                var properties = new MaterialPropertyBlock();
                dot.GetPropertyBlock(properties);
                return properties.GetColor("_Color");
            }
            bool Point(Vector3 origin, Vector3 end, bool pressed = false) =>
                (bool)Call(tools, "PointAtMarker", origin, end, pressed);
            string before = (string)Call(tools, "ToJson");
            Color savedColor = ColorOf(submitted);
            Assert.IsFalse(Point(Vector3.zero, Vector3.forward * 1.98f), "Nearer surface stops the ray");
            Assert.IsTrue(Point(Vector3.zero, Vector3.forward * 6f));
            Assert.AreEqual(Color.green, ColorOf(submitted));
            Assert.AreEqual(Color.gray, ColorOf(draft));
            var card = first.transform.Find("Marker preview");
            var label = card.GetComponentInChildren<TextMeshProUGUI>();
            StringAssert.Contains("#2", label.text);
            StringAssert.Contains("Original resident text 原文", label.text);
            StringAssert.Contains("#1  1.00 m", label.text);
            Assert.IsFalse(label.raycastTarget);
            Assert.IsEmpty(card.GetComponentsInChildren<Collider>());
            Assert.IsNull(Value(tools, "SelectedId"), "Hover does not enter the editor");
            Assert.AreEqual(before, Call(tools, "ToJson"));

            Assert.IsTrue(Point(Vector3.forward * 3f, Vector3.forward * 6f));
            Assert.AreEqual(savedColor, ColorOf(submitted));
            Assert.AreEqual(Color.green, ColorOf(draft));
            StringAssert.Contains("#3", label.text);
            var language = Type.GetType("ApartmentLanguage, Assembly-CSharp", true);
            string Copy(string key) => (string)language.GetMethod("Text").Invoke(null, new object[] { key });
            StringAssert.Contains(Copy("review.draft"), label.text);
            StringAssert.Contains(Copy("review.pendingfeedback"), label.text);
            Assert.AreEqual(1, first.GetComponentsInChildren<Canvas>(true).Length);
            var offset = Vector3.right * .013f;
            Assert.IsFalse(Point(offset, Vector3.forward * 6f + offset), "Just outside the visible dots");
            Assert.IsFalse(card.gameObject.activeSelf);
            Assert.AreEqual(Color.gray, ColorOf(draft));

            var selected = toolsType.GetEvent("MarkerSelected");
            var handler = typeof(SessionVisualsTests).GetMethod("Count", BindingFlags.Static | BindingFlags.NonPublic).MakeGenericMethod(recordType);
            selected.AddEventHandler(tools, Delegate.CreateDelegate(selected.EventHandlerType, handler));
            Call(tools, "BeginMarking");
            Assert.IsTrue(Point(Vector3.zero, Vector3.forward * 2f, true));
            Assert.AreEqual(1, events);
            Assert.AreEqual("old-feedback", Value(tools, "SelectedId"));
            Assert.IsFalse(card.gameObject.activeSelf);
            Assert.IsTrue(Point(Vector3.zero, Vector3.forward * 2f));
            Call(tools, "BeginMeasurement");
            Assert.IsFalse(card.gameObject.activeSelf);
            Assert.AreEqual(savedColor, ColorOf(submitted));
            Assert.IsFalse(Point(Vector3.zero, Vector3.forward * 2f, true));
            Assert.IsTrue((bool)Call(tools, "ConfirmPoint", Vector3.forward * 2f, null));
            Assert.IsTrue((bool)Value(tools, "HasPendingPoint"));
            Assert.AreEqual(1, events);
        }

        [Test] public void FreeTextMarkerShowsAnExcerptAndPagesThroughTheFullOriginal()
        {
            Seed();
            Call(tools, "RestoreSession");
            var record = Call(tools, "FindRecord", "old-feedback");
            string longText = string.Concat(Enumerable.Repeat("Kitchen 太小 <tag> ", 35)) + "LAST";
            Put(record, "observation", longText);
            Call(tools, "ShowRecord", "old-feedback");
            Call(tools, "PointAtMarker", Vector3.zero, Vector3.forward * 2f, false);
            var preview = first.transform.Find("Marker preview").GetComponentInChildren<TextMeshProUGUI>();
            StringAssert.Contains("Kitchen 太小 <tag>", preview.text);
            StringAssert.DoesNotContain("LAST", preview.text);
            Assert.IsFalse(preview.richText);

            var controllerType = Type.GetType("ApartmentReviewController, Assembly-CSharp", true);
            var controller = first.AddComponent(controllerType);
            Put(controller, "tools", tools);
            var panelType = Type.GetType("ApartmentVRPanel, Assembly-CSharp", true);
            var font = (TMP_FontAsset)Value(tools, "DisplayFont");
            using (var panel = (IDisposable)Activator.CreateInstance(panelType, first.transform, "Preview test editor",
                font, Shader.Find("UI/Default"), font.material.shader, 1100f, 720f))
            {
                controllerType.GetMethod("BuildMarkerPage").Invoke(controller, new object[] { panel });
                var root = (RectTransform)panelType.GetProperty("Root").GetValue(panel);
                var body = root.Find("User saved feedback").GetComponent<TextMeshProUGUI>();
                Assert.AreEqual(longText + "\nOriginal impact", body.text);
                Assert.IsFalse(body.richText);
                Assert.Greater(body.textInfo.pageCount, 1);
                Assert.IsNotNull(root.Find("Next feedback page"));
            }
        }

        [Test] public void MarkerReadoutUsesCurrentVisibleSelectionAndNeverHistoricalHiddenRecord()
        {
            Seed();Call(tools,"RestoreSession");Call(tools,"BeginMarking");StringAssert.DoesNotContain("#2",(string)Value(tools,"CurrentToolReadout"));
            string id=AddMarker(tools);StringAssert.Contains("#4",(string)Value(tools,"CurrentToolReadout"));string before=(string)Call(tools,"ToJson");
            Call(tools,"HideRecord",id);StringAssert.DoesNotContain("#4",(string)Value(tools,"CurrentToolReadout"));Assert.AreEqual(before,Call(tools,"ToJson"));
        }
        [Test] public void ShortcutsSwitchAndCancelWithoutLosingMeasurementsOrDrafts()
        {
            var controllerType = Type.GetType("ApartmentReviewController, Assembly-CSharp", true);
            var controller = first.AddComponent(controllerType);
            Put(controller, "tools", tools);
            var toggle = controllerType.GetMethod("ToggleTool");
            var modeType = toggle.GetParameters()[0].ParameterType;
            void Press(string mode) => toggle.Invoke(controller, new[] { Enum.Parse(modeType, mode) });

            Press("Measure");
            Call(tools, "ConfirmPoint", Vector3.zero, null);
            Call(tools, "ConfirmPoint", Vector3.right, null);
            string saved = (string)Call(tools, "ToJson");
            Call(tools, "ConfirmPoint", Vector3.up, null);
            Press("Measure");
            Assert.AreEqual("None", Value(tools, "Mode").ToString());
            Assert.IsFalse((bool)Value(tools, "HasPendingPoint"));
            Press("Measure");
            Call(tools, "ConfirmPoint", Vector3.up, null);
            Press("Mark");
            Assert.AreEqual("Mark", Value(tools, "Mode").ToString());
            Assert.IsFalse((bool)Value(tools, "HasPendingPoint"));
            Assert.AreEqual(saved, Call(tools, "ToJson"));
            Press("Mark");
            Assert.AreEqual("None", Value(tools, "Mode").ToString());
            Press("Mark");
            Call(tools, "ConfirmPoint", Vector3.forward, null);
            string draft = (string)Value(tools, "SelectedId");
            Press("Measure");
            Call(tools, "ConfirmPoint", Vector3.up, null);
            Press("Mark");
            Assert.AreEqual(draft, Value(tools, "SelectedId"));
            Assert.AreEqual("None", Value(tools, "Mode").ToString());
            Assert.IsFalse((bool)Value(tools, "HasPendingPoint"));
            Assert.AreEqual(2, Records(tools, "Records").Length);
        }

        [Test] public void TestStorageRejectsNonTemporaryPersistentPath()
        {
            var error=Assert.Throws<TargetInvocationException>(()=>Call(tools,"ConfigureStorageForTests",Path.Combine(Application.dataPath,"forbidden.json")));
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
    }
}
