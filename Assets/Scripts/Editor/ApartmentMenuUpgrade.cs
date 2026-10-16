#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

public static class ApartmentMenuUpgrade
{
    const string FontPath="Assets/Apartment/Fonts/VRMenu-SC.asset";
    static readonly string[] Protected={"Assets/Scenes/Apartment.unity","Assets/Scenes/ApartmentPhase2_VR.unity","Assets/Scenes/ApartmentPhase2.unity","Assets/Scenes/ApartmentPhase2_Furnished.unity"};
    public static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static string Hash(string path) { using(var sha=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
    static Scene RequireScene()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||
            scene.path!=ApartmentExperienceUpgrade.ScenePath||scene.isDirty||SceneManager.sceneCount!=1)
            throw new InvalidOperationException("请退出 Play，保存精细 VR 副本并等待编译完成。不会丢弃未保存修改。");
        return scene;
    }
    public static string RequiredCharacters() => new string((ApartmentExperienceUpgrade.RequiredCharacters()+ApartmentMenuController.AllText).Where(c=>!char.IsControl(c)).Distinct().ToArray());
    public static TMP_FontAsset EnsureFont(string path = FontPath)
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(font!=null)
        {
            foreach(char c in RequiredCharacters()) if(!font.HasCharacter(c))throw new InvalidOperationException("菜单字体缺少："+c);
            return font;
        }
        var source=AssetDatabase.LoadAssetAtPath<Font>(ApartmentFurnitureSetup.FontSource);
        font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
        if(font==null||!font.TryAddCharacters(RequiredCharacters(),out string missing))throw new InvalidOperationException("菜单中文字形生成失败。");
        font.name=Path.GetFileNameWithoutExtension(path);font.atlasPopulationMode=AtlasPopulationMode.Static;font.hideFlags=HideFlags.None;font.material.hideFlags=HideFlags.None;
        AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var texture in font.atlasTextures){texture.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(texture,font);EditorUtility.SetDirty(texture);}
        EditorUtility.SetDirty(font);EditorUtility.SetDirty(font.material);AssetDatabase.SaveAssets();return font;
    }
    [MenuItem("Apartment Phase 2/M8 Standalone UI/Apply Standalone UI Font %#&m")]
    public static void ApplyStandaloneUI()
    {
        try
        {
            var scene=RequireScene();var hashes=Protected.Select(Hash).ToArray();
            var menu=All<ApartmentMenuController>(scene).Single();
            const string path="Assets/Apartment/Fonts/VRStandaloneUI-SC.asset";
            if(AssetDatabase.GetAssetPath(menu.font)==path){Validate(scene);Debug.Log("M8 already configured; no changes.");return;}
            string backup="Logs/M8Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            File.Copy(scene.path,backup+"/Apartment_Detailed_VR.unity");
            var font=EnsureFont(path);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Configure standalone apartment panels");
            Undo.RecordObjects(new Object[]{menu,menu.tutorial,menu.interactor},"Update standalone UI Chinese font");
            menu.font=font;menu.tutorial.font=font;menu.interactor.hintFont=font;
            foreach(var component in new Component[]{menu,menu.tutorial,menu.interactor})EditorUtility.SetDirty(component);
            Validate(scene);
            for(int i=0;i<Protected.Length;i++)if(Hash(Protected[i])!=hashes[i])throw new InvalidOperationException("Protected scene changed: "+Protected[i]);
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("M8 standalone panels configured; only three font references changed. Backup: "+backup);
        }
        catch(Exception e){Debug.LogError("M8 stopped: "+e+"\nExisting edits preserved; no Git operations.");}
    }
    [MenuItem("Apartment Phase 2/M6 Menu/Install Menu and Optional Tutorial %#&5")]
    public static void Install()
    {
        try
        {
            var scene=RequireScene();var hashes=Protected.Select(Hash).ToArray();
            if(All<ApartmentMenuController>(scene).Length>0){Validate(scene);Debug.Log("M6 already installed; no duplicate objects created.");return;}
            string backup="Logs/M6Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            File.Copy(scene.path,backup+"/Apartment_Detailed_VR.unity");
            var font=EnsureFont();var mover=All<ApartmentVRLocomotion>(scene).Single();
            var tutorial=All<ApartmentTutorialController>(scene).Single();var owner=All<ApartmentFurnitureInteractor>(scene).Single();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install apartment menu and optional tutorial");
            Undo.RecordObjects(new Object[]{mover,tutorial,owner},"Connect shared menu input");
            var go=new GameObject("ApartmentMenu");Undo.RegisterCreatedObjectUndo(go,"Create menu owner");go.transform.SetParent(mover.transform,false);
            var menu=Undo.AddComponent<ApartmentMenuController>(go);
            var point=new GameObject("SafeResetPoint");Undo.RegisterCreatedObjectUndo(point,"Create fixed safe reset point");
            point.transform.position=mover.transform.position;point.transform.rotation=mover.transform.rotation;
            menu.locomotion=mover;menu.interactor=owner;menu.tutorial=tutorial;menu.safeResetPoint=point.transform;
            menu.font=font;menu.panelShader=tutorial.panelShader;menu.textOverlayShader=tutorial.textOverlayShader;
            mover.menu=menu;owner.menu=menu;tutorial.menu=menu;tutorial.font=font;owner.hintFont=font;
            foreach(var component in new Component[]{mover,owner,tutorial,menu})EditorUtility.SetDirty(component);
            Validate(scene);
            for(int i=0;i<Protected.Length;i++)if(Hash(Protected[i])!=hashes[i])throw new InvalidOperationException("Protected scene changed: "+Protected[i]);
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("M6 installed and saved: optional tutorial, B menu, three movement modes, fixed safety reset. Backup: "+backup);
        }
        catch(Exception e){Debug.LogError("M6 install stopped: "+e+"\nScene edits remain inspectable and undoable; no Git operations.");}
    }
    public static void Validate(Scene scene)
    {
        ApartmentExperienceUpgrade.Validate(scene);
        var menu=All<ApartmentMenuController>(scene).Single();
        if(menu.locomotion==null||menu.interactor==null||menu.tutorial==null||menu.safeResetPoint==null||
            menu.locomotion.menu!=menu||menu.interactor.menu!=menu||menu.tutorial.menu!=menu||
            menu.safeResetPoint.IsChildOf(menu.locomotion.transform)||menu.safeResetPoint.gameObject.scene!=scene||
            menu.font==null||menu.panelShader==null||menu.textOverlayShader==null||menu.locomotion.debugMode)
            throw new InvalidOperationException("菜单、教学、固定安全点或输入引用无效。");
        if(All<ApartmentVRLocomotion>(scene).Length!=1||All<OVRCameraRig>(scene).Length!=1)throw new InvalidOperationException("Duplicate movement owner or camera rig.");
        foreach(char c in RequiredCharacters())if(!menu.font.HasCharacter(c))throw new InvalidOperationException("Missing menu glyph: "+c);
    }
    [Serializable] public sealed class Report { public int passed;public List<string> failures=new List<string>();public bool questVerified;public string[] previews; }
    static Report report;
    static void Assert(bool value,string description){if(value)report.passed++;else report.failures.Add(description);}
    [MenuItem("Apartment Phase 2/M6 Menu/Run Menu Checks %#&4")]
    public static void Check()
    {
        report=new Report();Scene scratch=default,original=default;
        try
        {
            original=RequireScene();string before=File.ReadAllText(original.path);Validate(original);
            CheckProgress();CheckInput();
            var menu=All<ApartmentMenuController>(original).Single();
            foreach(char c in RequiredCharacters())Assert(menu.font.HasCharacter(c),"glyph "+c);
            scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            CheckSafety(menu);
            CheckViews(menu);
            Assert(File.ReadAllText(original.path)==before,"check changed saved scene");
        }
        catch(Exception e){report.failures.Add(e.ToString());}
        finally
        {
            if(original.IsValid())SceneManager.SetActiveScene(original);
            if(scratch.IsValid())EditorSceneManager.CloseScene(scratch,true);
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/M6MenuChecks.json",JsonUtility.ToJson(report,true));
            if(report.failures.Count==0)Debug.Log("M6 checks PASS: "+report.passed+" assertions; Quest hardware not verified.");
            else Debug.LogError("M6 checks FAIL: "+string.Join(" | ",report.failures));
        }
    }
    static void CheckProgress()
    {
        var skip=new ApartmentTutorialProgress(true);
        Assert(skip.Welcome&&!skip.AllowMove&&!skip.AllowInteraction,"welcome blocks world input");
        skip.Confirm();Assert(skip.Welcome,"generic OK cannot dismiss welcome");skip.ChooseTutorial(false);skip.ObserveNeutral(true);
        Assert(skip.Skipped&&skip.AllowMove&&skip.AllowTurn&&skip.AllowTeleport&&skip.AllowInteraction,"skip unlocks exploration");
        foreach(ApartmentInteractionKind kind in Enum.GetValues(typeof(ApartmentInteractionKind)))Assert(skip.HasSeen(kind)&&!skip.RequestKind(kind),"skip suppresses "+kind);
        skip.Suspend();skip.ObserveNeutral(true);Assert(skip.Skipped,"tracking retains skip");Assert(new ApartmentTutorialProgress(true).Welcome,"new session asks again");
        foreach(ApartmentMovementMode mode in Enum.GetValues(typeof(ApartmentMovementMode)))
        {
            var p=new ApartmentTutorialProgress(true);p.ChooseTutorial(true);Assert(p.Step==-1&&p.Explaining,"menu lesson first");
            p.Confirm();p.ObserveNeutral(true);p.MenuOpened();p.ChooseMode(mode);Assert(p.MenuChoiceMade,"mode choice registered including Both");p.MenuClosed();
            if(mode!=ApartmentMovementMode.TeleportOnly)
            { Assert(p.Step==0,"walk enabled");p.Confirm();p.ObserveNeutral(true);p.Walk(0);p.Walk(float.NaN);Assert(p.Step==0,"invalid walk ignored");p.Walk(.25f); }
            Assert(p.Step==1,"turn always taught");p.Confirm();p.ObserveNeutral(true);p.Turn(15);
            if(mode!=ApartmentMovementMode.SmoothOnly)
            { Assert(p.Step==2,"teleport enabled");p.Confirm();p.ObserveNeutral(true);p.Teleport(.49f);Assert(p.Step==2,"short teleport ignored");p.Teleport(.5f); }
            p.ObserveNeutral(true);Assert(p.Step==3&&p.AllowInteraction,"selected curriculum completes");
            Assert(p.RequestKind(ApartmentInteractionKind.RoomDoor),"first interaction hint");p.Confirm();p.ObserveNeutral(true);Assert(!p.RequestKind(ApartmentInteractionKind.RoomDoor),"hint once");
        }
        var changed=new ApartmentTutorialProgress(true);changed.ChooseTutorial(true);changed.Confirm();changed.MenuOpened();changed.ChooseMode(ApartmentMovementMode.Both);changed.MenuClosed();
        changed.Confirm();changed.ObserveNeutral(true);changed.Walk(.1f);changed.MenuOpened();changed.ChooseMode(ApartmentMovementMode.TeleportOnly);changed.MenuClosed();
        Assert(changed.Step==1,"mode change removes disabled unfinished walk");
        changed.Confirm();changed.ObserveNeutral(true);changed.Turn(15);changed.MenuOpened();changed.ChooseMode(ApartmentMovementMode.SmoothOnly);changed.MenuClosed();
        Assert(changed.Step==0,"re-enabled unfinished walk remains available");
        changed.Confirm();changed.ObserveNeutral(true);changed.Walk(.25f);Assert(changed.Step==3,"disabled teleport cannot trap tutorial");
    }
    static void CheckInput()
    {
        var b=new ApartmentMenuButtonLatch();Assert(!b.Sample(true,true),"held B on startup ignored");b.Sample(false,true);Assert(b.Sample(true,true),"fresh B opens");
        Assert(!b.Sample(true,true),"held B no repeat");b.Sample(false,true);Assert(b.Sample(true,true),"fresh B closes");b.Sample(false,false);Assert(!b.Sample(true,true),"focus recovery B ignored until release");
        var t=new ApartmentInteractionTriggerLatch();t.Sample(0,true);Assert(t.Sample(.75f,true),"trigger activates once");t.Reset();Assert(!t.Sample(1,true),"page change held trigger ignored");
        t.Sample(.25f,true);Assert(t.Sample(.75f,true),"fresh trigger after UI change");
        Assert(ApartmentMenuController.KeyText(ApartmentMovementMode.SmoothOnly).Contains("当前未启用"),"smooth only help");
        Assert(ApartmentMenuController.KeyText(ApartmentMovementMode.TeleportOnly).Contains("当前未启用"),"teleport only help");
        Assert(!ApartmentMenuController.KeyText(ApartmentMovementMode.Both).Contains("当前未启用"),"both help");
    }
    static void CheckSafety(ApartmentMenuController source)
    {
        var root=new GameObject("Safety test player");root.SetActive(false);root.transform.position=new Vector3(2000,0,2000);
        var cc=root.AddComponent<CharacterController>();cc.radius=.3f;cc.skinWidth=.03f;cc.height=1.8f;cc.center=Vector3.up*.9f;
        var mover=root.AddComponent<ApartmentVRLocomotion>();
        var rigObject=new GameObject("Safety test rig");rigObject.transform.SetParent(root.transform,false);
        var rig=rigObject.AddComponent<OVRCameraRig>();rig.EnsureGameObjectIntegrity();mover.rig=rig;
        // OVRCameraRig creates its anchors in Awake; inactive fixtures explicitly supply a head.
        if(rig.centerEyeAnchor==null) throw new InvalidOperationException("Test rig anchors unavailable");
        rig.centerEyeAnchor.localPosition=new Vector3(.2f,1.65f,.1f);
        mover.teleportFloorMask=source.locomotion.teleportFloorMask;mover.teleportEnabled=false;
        root.SetActive(false);
        // The real scene's player occupies the spawn in edit mode. The fixture must not treat that
        // duplicate test body as furniture; runtime validation already excludes its own player hierarchy.
        var playerColliders=source.locomotion.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled).ToArray();
        try
        {
            foreach(var c in playerColliders)c.enabled=false;Physics.SyncTransforms();
            Assert(mover.ValidateSafetyDestination(source.safeResetPoint.position,out _,out string actualReason),"authored spawn is a valid reset target: "+actualReason);
        }
        finally{foreach(var c in playerColliders)c.enabled=true;Physics.SyncTransforms();}
        int layer=0;while((mover.teleportFloorMask.value&(1<<layer))==0&&layer<31)layer++;
        var floor=new GameObject("Safety test ground");floor.layer=layer;var box=floor.AddComponent<BoxCollider>();box.size=new Vector3(4,.1f,4);
        Vector3 target=new Vector3(2010,0,2000);floor.transform.position=target-Vector3.up*.05f;Physics.SyncTransforms();
        Assert(mover.ValidateSafetyDestination(target,out _,out _),"reset ignores distance, navmesh, A enable and current support");
        Assert(!mover.TrySafetyReset(target,out _),"untracked editor cannot commit reset");
        var obstacle=new GameObject("Safety destination blocker").AddComponent<BoxCollider>();obstacle.size=Vector3.one;obstacle.transform.position=target+Vector3.up*.5f;Physics.SyncTransforms();
        Assert(!mover.ValidateSafetyDestination(target,out _,out _),"occupied reset destination rejected");
        obstacle.enabled=false;Physics.SyncTransforms();Assert(mover.ValidateSafetyDestination(target,out _,out _),"cleared reset destination accepted");
        obstacle.transform.position=root.transform.position+Vector3.up*.8f;obstacle.enabled=true;Physics.SyncTransforms();
        Assert(mover.ValidateSafetyDestination(target,out _,out _),"blocked current position does not block recovery");
        Quaternion rotation=root.transform.rotation;float eyeHeight=rig.centerEyeAnchor.position.y-root.transform.position.y;
        typeof(ApartmentVRLocomotion).GetMethod("CommitRelocation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(mover,new object[]{target,ApartmentRelocationReason.SafetyReset});
        Assert(Vector3.Distance(new Vector3(rig.centerEyeAnchor.position.x,target.y,rig.centerEyeAnchor.position.z),target)<.001f,"recovery compensates real head offset");
        Assert(root.transform.rotation==rotation&&Mathf.Abs(rig.centerEyeAnchor.position.y-root.transform.position.y-eyeHeight)<.001f,"recovery preserves heading and eye height");
        Assert(mover.LastRelocationReason==ApartmentRelocationReason.SafetyReset,"recovery has distinct event reason");
        var wrongLayer=floor.layer;floor.layer=0;Physics.SyncTransforms();Assert(!mover.ValidateSafetyDestination(target,out _,out _),"table/default layer cannot be safe ground");floor.layer=wrongLayer;
        box.enabled=false;Physics.SyncTransforms();Assert(!mover.ValidateSafetyDestination(target,out _,out _),"missing reset ground rejected");
        Object.DestroyImmediate(root);Object.DestroyImmediate(floor);Object.DestroyImmediate(obstacle.gameObject);
    }
    static void CheckViews(ApartmentMenuController source)
    {
        var go=new GameObject("Menu preview scratch");var menu=go.AddComponent<ApartmentMenuController>();
        menu.font=source.font;menu.panelShader=source.panelShader;menu.textOverlayShader=source.textOverlayShader;
        var paths=new List<string>();
        for(int page=0;page<ApartmentMenuController.Tabs.Length;page++)
        {
            menu.BuildPreview(page);Assert(menu.MovementMode==ApartmentMovementMode.Both,"new session default Both");
            Render(go,menu.PanelRect,"Logs/M6-Menu-"+page+".png");paths.Add("Logs/M6-Menu-"+page+".png");
        }
        // Drive the actual menu hit test and callbacks, without pretending this is tracked headset input.
        typeof(ApartmentMenuController).GetProperty("InputReady").SetValue(menu,true);
        menu.interactor=new GameObject("UI fixture owner").AddComponent<ApartmentFurnitureInteractor>();
        var fixtureMover=go.AddComponent<ApartmentVRLocomotion>();fixtureMover.rig=source.locomotion.rig;menu.locomotion=fixtureMover;
        Click(menu,"Cancel quit");Assert(menu.IsOpen&&menu.Page==2,"quit cancellation returns to controls");
        Click(menu,"Tab 4");
        foreach(ApartmentMovementMode mode in Enum.GetValues(typeof(ApartmentMovementMode)))
        {
            Click(menu,"Mode "+(int)mode);
            Assert(menu.MovementMode==mode,"real UI selects "+mode);
            Assert(menu.AllowsSmoothMove==(mode!=ApartmentMovementMode.TeleportOnly)&&menu.AllowsTeleport==(mode!=ApartmentMovementMode.SmoothOnly),"mode input policy "+mode);
        }
        Click(menu,"Safety reset");Assert(menu.ConfirmingReset,"reset requires confirmation");
        Render(go,menu.PanelRect,"Logs/M6-ResetConfirmation.png");paths.Add("Logs/M6-ResetConfirmation.png");
        Click(menu,"Cancel reset");Assert(!menu.ConfirmingReset,"reset cancel returns without moving");
        foreach(int index in new[]{0,1,3}){Click(menu,"Tab "+index);Assert(menu.Page==index&&!menu.ConfirmingReset,"placeholder navigation only "+index);}
        Click(menu,"Tab 4");Click(menu,"Tab 5");
        Assert(menu.ActivePanel==ApartmentPanelState.QuitConfirmation&&menu.Page==4,"menu exit preserves source settings page");
        CheckModal(menu,ApartmentMenuController.ExitQuestion,"确认退出","取消");
        Render(go,menu.PanelRect,"Logs/M8-QuitConfirmation.png");paths.Add("Logs/M8-QuitConfirmation.png");
        Click(menu,"Cancel quit");Assert(menu.IsOpen&&menu.Page==4,"cancel returns to exact menu source");
        Click(menu,"Tab 5");menu.Toggle();Assert(menu.IsOpen&&menu.Page==4,"B cancels menu quit");
        Click(menu,"Tab 5");CheckSuspendedQuit(menu,ApartmentPanelState.Menu);
        Assert(menu.Page==4,"focus recovery preserves menu source page");
        Click(menu,"Tab 5");Click(menu,"Confirm quit");
        Assert(!menu.HasOpenPanel&&menu.BlocksWorldInput,"menu confirm closes UI and retains neutral-input gate (edit-mode fixture does not exit editor)");
        menu.BuildTourPreview();
        CheckModal(menu,ApartmentMenuController.TourTitle,"继续探索","退出游戏");
        Render(go,menu.PanelRect,"Logs/M8-TourComplete.png");paths.Add("Logs/M8-TourComplete.png");
        menu.Toggle();Assert(menu.ShowingTourCompletion&&!menu.IsOpen,"B cannot open menu or dismiss completion");
        Click(menu,"Tour quit");CheckModal(menu,ApartmentMenuController.ExitQuestion,"确认退出","取消");
        Click(menu,"Cancel quit");Assert(menu.ShowingTourCompletion,"cancel returns to tour source");
        Click(menu,"Tour quit");menu.Toggle();Assert(menu.ShowingTourCompletion,"B cancels tour quit");
        Click(menu,"Tour quit");CheckSuspendedQuit(menu,ApartmentPanelState.TourComplete);
        CheckModal(menu,ApartmentMenuController.TourTitle,"继续探索","退出游戏");
        Click(menu,"Tour quit");Click(menu,"Confirm quit");Assert(!menu.HasOpenPanel,"tour confirm closes UI (edit-mode fixture)");
        // Isolated fixture for the actual once-per-session notification and dismissal callbacks.
        var tour=go.AddComponent<ApartmentTourTracker>();menu.tour=tour;
        var visit=new ApartmentTourProgress(6);
        for(int room=0;room<6;room++){for(int n=0;n<8;n++)visit.Observe(room,.1f);for(int n=0;n<8;n++)visit.Observe(-1,.1f);}
        typeof(ApartmentTourTracker).GetProperty("Progress").SetValue(tour,visit);
        menu.ShowTourCompletion();
        Assert(menu.ShowingTourCompletion&&menu.HasOpenPanel&&!menu.IsOpen&&tour.NotificationShown,"completed tour shows once outside menu");
        int revision=menu.UIRevision;
        Click(menu,"Continue exploring");Assert(!menu.HasOpenPanel&&!menu.ShowingTourCompletion&&menu.BlocksWorldInput,"continue exploring closes notice, awaits neutral");
        Assert(menu.UIRevision>revision,"panel transition invalidates shared trigger input");
        menu.ShowTourCompletion();Assert(!menu.HasOpenPanel,"completed tour cannot repeat after dismiss");
        var freshVisit=new ApartmentTourProgress(6);
        Assert(!freshVisit.Complete&&Enumerable.Range(0,6).All(i=>!freshVisit.Entered(i)&&!freshVisit.Exited(i)),"new play session starts with no completed rooms");
        Object.DestroyImmediate(menu.interactor.gameObject);
        Object.DestroyImmediate(go);
        go=new GameObject("Welcome preview scratch");var tutorial=go.AddComponent<ApartmentTutorialController>();tutorial.font=source.font;tutorial.panelShader=source.panelShader;tutorial.textOverlayShader=source.textOverlayShader;
        var welcome=tutorial.BuildWelcomePreview();Render(go,welcome,"Logs/M6-Welcome.png");paths.Add("Logs/M6-Welcome.png");
        foreach(bool start in new[]{true,false})
        {
            typeof(ApartmentTutorialController).GetProperty("Session").SetValue(tutorial,new ApartmentTutorialProgress(true));
            typeof(ApartmentTutorialController).GetProperty("InputReady").SetValue(tutorial,true);
            tutorial.BuildWelcomePreview();
            var button=welcome.Find(start?"Start tutorial":"Skip tutorial").GetComponent<RectTransform>();var point=button.TransformPoint(button.rect.center);
            tutorial.ProcessPointer(new Ray(point-button.forward,button.forward),2,true,out _);
            Assert(start?tutorial.Session.Step==-1:tutorial.Session.Skipped,"real welcome UI "+(start?"starts":"skips"));
        }
        Object.DestroyImmediate(go);report.previews=paths.ToArray();
    }
    static void CheckModal(ApartmentMenuController menu,string title,string left,string right)
    {
        var labels=menu.PanelRect.GetComponentsInChildren<TextMeshProUGUI>();
        Assert(labels.Select(t=>t.text).SequenceEqual(new[]{title,left,right}),"standalone modal contains only exact title and two button labels");
        Assert(menu.GetComponentsInChildren<Canvas>().Length==1,"exactly one active panel canvas");
        Assert(menu.PanelRect.Find("Footer")==null&&menu.PanelRect.Find("Menu title")==null&&menu.PanelRect.Find("Tab 0")==null,"modal has no footer, menu title or sidebar");
        Assert(menu.BlocksWorldInput,"modal blocks locomotion and furniture input");
    }
    static void CheckSuspendedQuit(ApartmentMenuController menu,ApartmentPanelState source)
    {
        typeof(ApartmentMenuController).GetMethod("Suspend",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
        Assert(!menu.Visible&&menu.ActivePanel==source&&menu.GetComponentsInChildren<Canvas>().Length==0,"tracking loss hides panels and cancels pending quit to source");
        typeof(ApartmentMenuController).GetMethod("ConfirmQuit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
        Assert(menu.ActivePanel==source,"untracked stale confirm cannot quit");
        typeof(ApartmentMenuController).GetProperty("InputReady").SetValue(menu,true);
        typeof(ApartmentMenuController).GetMethod("Rebuild",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{true});
        Assert(menu.Visible&&menu.ActivePanel==source&&menu.BlocksWorldInput,"recovery shows source, does not automatically quit");
    }
    static void Click(ApartmentMenuController menu,string name)
    {
        var rect=menu.PanelRect.Find(name).GetComponent<RectTransform>();Vector3 point=rect.TransformPoint(rect.rect.center);
        menu.ProcessPointer(new Ray(point-rect.forward,rect.forward),2,true,out _);
    }
    [MenuItem("Apartment Phase 2/M6 Menu/Inspect Running Session %#&3")]
    public static void InspectRunningSession()
    {
        if(!EditorApplication.isPlaying){Debug.LogWarning("M6 session inspection requires Play.");return;}
        var scene=SceneManager.GetActiveScene();var menu=All<ApartmentMenuController>(scene).Single();
        var data=new RuntimeSnapshot{menuEnabled=menu.isActiveAndEnabled,moverEnabled=menu.locomotion.isActiveAndEnabled,
            tutorialEnabled=menu.tutorial.isActiveAndEnabled,welcome=menu.tutorial.Session.Welcome,
            movementMode=menu.MovementMode.ToString(),menuOpen=menu.IsOpen,inputReady=menu.InputReady,
            tutorialStep=menu.tutorial.Session.Step,safePoint=menu.safeResetPoint.position,questVerified=false};
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/M6RuntimeSnapshot.json",JsonUtility.ToJson(data,true));
        Debug.Log("M6 runtime inspected: menu="+data.menuEnabled+", mover="+data.moverEnabled+", welcome="+data.welcome+", mode="+data.movementMode+", inputReady="+data.inputReady+". Not hardware acceptance.");
    }
    [Serializable] sealed class RuntimeSnapshot
    {public bool menuEnabled,moverEnabled,tutorialEnabled,welcome,menuOpen,inputReady,questVerified;public string movementMode;public int tutorialStep;public Vector3 safePoint;}
    static void Render(GameObject go,RectTransform panel,string path)
    {
        panel.position=new Vector3(1000,1000,1001.2f);panel.rotation=Quaternion.identity;
        foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        var camera=new GameObject("UI preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,1000);camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.12f,.16f);camera.orthographic=true;
        camera.orthographicSize=Mathf.Max(panel.sizeDelta.y,panel.sizeDelta.x/1.4f)*.001f*.55f;
        panel.GetComponent<Canvas>().worldCamera=camera;RenderTexture texture=null;Texture2D png=null;var previous=RenderTexture.active;
        try
        {
            texture=new RenderTexture(1400,1000,24);camera.targetTexture=texture;
            Canvas.ForceUpdateCanvases();foreach(var label in go.GetComponentsInChildren<TextMeshProUGUI>())
            {label.ForceMeshUpdate();Assert(!label.isTextOverflowing,"text overflow: "+path+" "+label.name);}
            camera.Render();RenderTexture.active=texture;png=new Texture2D(1400,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,1000),0,0);png.Apply();
            Directory.CreateDirectory("Logs");File.WriteAllBytes(path,png.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(camera.gameObject);if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}if(png!=null)Object.DestroyImmediate(png);}
    }
}
#endif
