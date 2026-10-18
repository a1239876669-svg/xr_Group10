using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Group10.GuidedJourney.Tests
{
    public sealed class LocalizationTests
    {
        Type language,choice;string key;
        object Call(string name,params object[] args)=>language.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);
        string Text(string id)=>(string)Call("Text",id);
        string Display(string text)=>(string)Call("Display",text);
        void Set(int value)=>Call("SetLanguage",Enum.ToObject(choice,value));
        [SetUp] public void Setup()
        {
            language=Type.GetType("ApartmentLanguage, Assembly-CSharp",true);choice=Type.GetType("ApartmentDisplayLanguage, Assembly-CSharp",true);
            key="Group10.Tests.Language."+Guid.NewGuid().ToString("N");Call("BeginRun");
        }
        [TearDown] public void Teardown(){PlayerPrefs.DeleteKey(key);Call("BeginRun");}
        [TestCase(1)] [TestCase(99)]
        public void NewSessionIgnoresAndDoesNotDeleteStoredPreferences(int legacyValue)
        {
            // Never write the participant's real legacy key or any unrelated preference.
            PlayerPrefs.SetInt(key,legacyValue);Set(1);Call("BeginRun");
            Assert.AreEqual("Settings",Display("设置"));
            Assert.AreEqual(legacyValue,PlayerPrefs.GetInt(key),"A new session must not clear saved settings");
        }
        [Test] public void ChineseAndEnglishToggleImmediately(){Set(1);Assert.AreEqual("设置",Display("设置"));Assert.AreEqual("保存反馈",Display("Save feedback"));Set(0);Assert.AreEqual("Save feedback",Display("保存反馈"));}
        [Test] public void EveryNewSessionStartsInEnglishAfterAnEarlierChineseSession()
        {
            for(int run=0;run<3;run++)
            {
                Set(1);Assert.IsTrue((bool)language.GetProperty("IsChinese").GetValue(null));
                Call("BeginRun");Assert.AreEqual("Settings",Display("设置"));
            }
        }
        [Test] public void SessionResetIsRegisteredForDisabledDomainReload()
        {
            var method=language.GetMethod("BeginRun",BindingFlags.Static|BindingFlags.NonPublic);
            var attribute=method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration,attribute.loadType);
        }
        [Test] public void SessionResetRefreshesAndPreservesExistingLiveSubscribers()
        {
            int notifications=0;Action listener=()=>notifications++;
            var changed=language.GetEvent("Changed");changed.AddEventHandler(null,listener);
            try
            {
                Set(1);Assert.AreEqual(1,notifications);
                Call("BeginRun");Assert.AreEqual(2,notifications);Assert.AreEqual("Settings",Display("设置"));
                Set(1);Assert.AreEqual(3,notifications,"A no-domain-reload reset must retain live panel subscriptions");
                Set(1);Assert.AreEqual(3,notifications,"Reapplying the same language must not rebuild UI unnecessarily");
            }
            finally {changed.RemoveEventHandler(null,listener);}
        }
        [Test] public void InvalidLanguageIsRejectedWithoutChangingCurrentSession()
        {
            Set(1);
            var error=Assert.Throws<TargetInvocationException>(()=>Set(99));
            Assert.IsInstanceOf<ArgumentOutOfRangeException>(error.InnerException);
            Assert.IsTrue((bool)language.GetProperty("IsChinese").GetValue(null));
        }
        [Test] public void SourceMarkupAndNumbersArePreserved(){Set(0);Assert.AreEqual("●  Smooth movement\n<size=75%>Walk with the left stick</size>",Display("●  平滑移动\n<size=75%>使用左摇杆行走</size>"));}
        [Test] public void RecoveryCopyMatchesRealButtonsInBothLanguages()
        {
            foreach(int value in new[]{0,1}){Set(value);string help=Text("recovery.help");StringAssert.Contains(Text("recovery.undo"),help);StringAssert.Contains(Text("review.cancelpoint"),help);StringAssert.Contains(Text("review.canceldraft"),help);StringAssert.Contains(Text("safe.button"),help);}
        }
        [Test] public void AuthoredEnglishCopyHasChineseCounterpart(){Set(1);Assert.AreEqual("柜门练习",Display("Cabinet practice"));Assert.AreEqual("小号",Text("bag.small"));Assert.AreEqual("中号",Text("bag.medium"));}

        [Test] public void SettingsPageDefaultsToEnglishAndRefreshesChineseImmediately()
        {
            Set(0);var menuType=Type.GetType("ApartmentMenuController, Assembly-CSharp",true);var go=new GameObject("Language menu test");
            try
            {
                var menu=go.AddComponent(menuType);
                var fontType=Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro",true);
                menuType.GetField("font").SetValue(menu,UnityEditor.AssetDatabase.LoadAssetAtPath("Assets/Apartment/Fonts/VRStandaloneUI-SC.asset",fontType));
                menuType.GetField("panelShader").SetValue(menu,Shader.Find("Apartment/TutorialOverlay"));
                menuType.GetField("textOverlayShader").SetValue(menu,Shader.Find("TextMeshPro/Distance Field Overlay"));
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                menuType.GetProperty("ActivePanel").GetSetMethod(true).Invoke(menu,new[]{Enum.ToObject(Type.GetType("ApartmentPanelState, Assembly-CSharp",true),1)});
                menuType.GetProperty("InputReady").GetSetMethod(true).Invoke(menu,new object[]{true});menuType.GetProperty("Page").GetSetMethod(true).Invoke(menu,new object[]{4});
                menuType.GetMethod("Rebuild",flags).Invoke(menu,new object[]{false});
                var textType=Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro",true);
                foreach(var label in go.GetComponentsInChildren(textType,true))
                {
                    string text=(string)textType.GetProperty("text").GetValue(label);
                    if(text=="简体中文")continue;
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,@"[\u4e00-\u9fff]"),text);
                }
                Set(1);
                var labels=go.GetComponentsInChildren(textType,true);bool found=false;
                foreach(var label in labels)if((string)textType.GetProperty("text").GetValue(label)=="语言")found=true;
                Assert.IsTrue(found,"The currently rendered Language label must refresh without reloading the scene");
                // Closing and reopening the menu is not a new runtime session.
                menuType.GetMethod("Toggle").Invoke(menu,null);menuType.GetMethod("Toggle").Invoke(menu,null);
                Assert.IsTrue((bool)language.GetProperty("IsChinese").GetValue(null));
                bool reopenedChinese=false;
                foreach(var label in go.GetComponentsInChildren(textType,true))
                    if((string)textType.GetProperty("text").GetValue(label)=="语言")reopenedChinese=true;
                Assert.IsTrue(reopenedChinese,"Menu reopening must retain this session's language choice");
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void LoadingUnrecognisedParticipantFeedbackDoesNotReplaceTheirWords()
        {
            var type=Type.GetType("ApartmentReviewController, Assembly-CSharp",true);var recordType=Type.GetType("QuestGuidance.Review.ReviewRecord, Assembly-CSharp",true);var go=new GameObject("Original feedback test");
            try
            {
                var controller=go.AddComponent(type);var record=Activator.CreateInstance(recordType);
                recordType.GetField("id").SetValue(record,"original-user-record");recordType.GetField("observation").SetValue(record,"Kitchen feels cramped 原文");recordType.GetField("everydayImpact").SetValue(record,"My original free text");
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                type.GetMethod("LoadEditor",flags).Invoke(controller,new[]{record});Set(1);
                Assert.AreEqual("Kitchen feels cramped 原文",type.GetField("observation",flags).GetValue(controller));
                Assert.AreEqual("My original free text",type.GetField("impact",flags).GetValue(controller));
                Assert.AreEqual("Kitchen feels cramped 原文",recordType.GetField("observation").GetValue(record));
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void UnknownTextIsNeverInventedOrTranslated(){Set(1);const string original="Resident 938 sample original xyz";Assert.AreEqual(original,Display(original));}
    }
}
