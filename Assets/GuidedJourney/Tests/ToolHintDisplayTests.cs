using System;
using System.Reflection;
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
        [Test]public void ActiveMeasurementRetainsSmallReadableHintAndLanguageRefresh()
        {
            toolsType.GetMethod("BeginMeasurement").Invoke(tools,null);Call("UpdateToolHint",true);StringAssert.Contains("Select two points to measure a distance.",VisibleText());
            var choice=Type.GetType("ApartmentDisplayLanguage, Assembly-CSharp",true);language.GetMethod("SetLanguage").Invoke(null,new[]{Enum.ToObject(choice,1)});StringAssert.Contains("选择两个点测量距离",VisibleText());Assert.AreSame(originalJourney,guideType.GetProperty("Journey").GetValue(guide));
        }
        [Test]public void MarkerAndToolStopChangeOnlyPresentationAndKeepJourney()
        {
            toolsType.GetMethod("BeginMarking").Invoke(tools,null);Call("UpdateToolHint",true);StringAssert.Contains("Place a marker and submit your feedback.",VisibleText());
            Call("UpdateToolHint",false);Assert.AreEqual("",VisibleText());toolsType.GetMethod("StopTool").Invoke(tools,null);Call("UpdateToolHint",true);Assert.AreEqual("",VisibleText());Assert.AreSame(originalJourney,guideType.GetProperty("Journey").GetValue(guide));
        }
    }
}
