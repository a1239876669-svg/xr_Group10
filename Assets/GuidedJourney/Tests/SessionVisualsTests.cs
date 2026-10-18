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
        [Test] public void ReadoutFollowsVisibleMeasurementUndoAndHideWithoutErasingData()
        {
            Seed();Call(tools,"RestoreSession");AddMeasurement(tools);Call(tools,"BeginMeasurement");Call(tools,"ConfirmPoint",Vector3.zero,null);Call(tools,"ConfirmPoint",Vector3.right*3,null);
            StringAssert.Contains("3.00",(string)Value(tools,"CurrentToolReadout"));string before=(string)Call(tools,"ToJson");Call(tools,"UndoLastMeasurementDisplay");
            StringAssert.Contains("1.00",(string)Value(tools,"CurrentToolReadout"));Call(tools,"HideMeasurementDisplays");StringAssert.DoesNotContain("1.00",(string)Value(tools,"CurrentToolReadout"));Assert.AreEqual(before,Call(tools,"ToJson"));
        }
        [Test] public void MarkerReadoutUsesCurrentVisibleSelectionAndNeverHistoricalHiddenRecord()
        {
            Seed();Call(tools,"RestoreSession");Call(tools,"BeginMarking");StringAssert.DoesNotContain("#2",(string)Value(tools,"CurrentToolReadout"));
            string id=AddMarker(tools);StringAssert.Contains("#4",(string)Value(tools,"CurrentToolReadout"));string before=(string)Call(tools,"ToJson");
            Call(tools,"HideRecord",id);StringAssert.DoesNotContain("#4",(string)Value(tools,"CurrentToolReadout"));Assert.AreEqual(before,Call(tools,"ToJson"));
        }
        [Test] public void TestStorageRejectsNonTemporaryPersistentPath()
        {
            var error=Assert.Throws<TargetInvocationException>(()=>Call(tools,"ConfigureStorageForTests",Path.Combine(Application.dataPath,"forbidden.json")));
            Assert.IsInstanceOf<ArgumentException>(error.InnerException);
        }
    }
}
