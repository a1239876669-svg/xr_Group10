#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApartmentTourUpgrade
{
    static Scene RequireScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            scene.path != ApartmentExperienceUpgrade.ScenePath || scene.isDirty || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("请退出 Play，保存精细 VR 场景并等待编译。不会丢弃未保存修改。");
        return scene;
    }
    // Local coordinates from the detailed copy's physical wall and floor bounds, not the old graybox.
    static Bounds Volume(float x0, float x1, float z0, float z1) =>
        new Bounds(new Vector3((x0+x1)*.5f,1.65f,(z0+z1)*.5f),new Vector3(x1-x0,3.1f,z1-z0));
    public static ApartmentVisitRegion[] Regions() => new[]
    {
        new ApartmentVisitRegion { roomName="客厅", volumes=new[]{Volume(3.28f,6.54f,.24f,5.49f),Volume(3.28f,5.61f,5.49f,7.26f),Volume(6.54f,9.36f,4.75f,5.49f)} },
        new ApartmentVisitRegion { roomName="厨房", volumes=new[]{Volume(.24f,3.08f,4.90f,7.26f)} },
        new ApartmentVisitRegion { roomName="主卧", volumes=new[]{Volume(6.73f,9.36f,.24f,4.60f)} },
        new ApartmentVisitRegion { roomName="次卧", volumes=new[]{Volume(.24f,3.08f,.24f,2.88f)} },
        new ApartmentVisitRegion { roomName="卫生间", volumes=new[]{Volume(.24f,3.08f,3.08f,4.72f)} },
        new ApartmentVisitRegion { roomName="阳台", volumes=new[]{Volume(5.76f,9.36f,5.75f,7.26f)} }
    };
    [MenuItem("Apartment Phase 2/M7 Visit/Install Exit and Visit Completion %#&1")]
    public static void Install()
    {
        try
        {
            var scene=RequireScene();var menu=ApartmentMenuUpgrade.All<ApartmentMenuController>(scene).Single();
            if(menu.tour!=null){Validate(scene);Debug.Log("M7 already installed; no duplicate regions.");return;}
            string backup="Logs/M7Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);
            File.Copy(scene.path,backup+"/Apartment_Detailed_VR.unity");
            var font=ApartmentMenuUpgrade.EnsureFont("Assets/Apartment/Fonts/VRTour-SC.asset");
            var space=scene.GetRootGameObjects().Single(g=>g.name=="DetailedEnvironment").transform;
            // Stop if someone has changed the geometry that defined these regions.
            var kitchen=space.Find("House/Structure/Floor_Kitchen").GetComponent<Renderer>().bounds;
            var local=space.InverseTransformPoint(kitchen.center);
            if(Vector3.Distance(local,new Vector3(1.66f,.006f,6.08f))>.05f)
                throw new InvalidOperationException("厨房范围已改变，请先重新核对房间边界。");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Install apartment visit completion and exit");
            Undo.RecordObjects(new UnityEngine.Object[]{menu,menu.tutorial,menu.interactor},"Connect visit UI and font");
            var go=new GameObject("ApartmentTourTracker");Undo.RegisterCreatedObjectUndo(go,"Create non-physical visit tracker");
            go.transform.SetParent(menu.transform,false);
            var tracker=Undo.AddComponent<ApartmentTourTracker>(go);tracker.menu=menu;tracker.space=space;tracker.rooms=Regions();
            menu.tour=tracker;menu.font=font;menu.tutorial.font=font;menu.interactor.hintFont=font;
            foreach(var o in new UnityEngine.Object[]{tracker,menu,menu.tutorial,menu.interactor})EditorUtility.SetDirty(o);
            Validate(scene);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("M7 installed and saved: six room enter/exit tracking, once per session notice, confirmed exit. Backup: "+backup);
        }
        catch(Exception e){Debug.LogError("M7 install stopped; edits remain undoable: "+e);}
    }
    static void Validate(Scene scene)
    {
        ApartmentMenuUpgrade.Validate(scene);
        var tracker=ApartmentMenuUpgrade.All<ApartmentTourTracker>(scene).Single();
        if(tracker.menu==null||tracker.menu.tour!=tracker||tracker.space==null||tracker.rooms==null||tracker.rooms.Length!=6)
            throw new InvalidOperationException("参观统计引用不完整。");
        if(tracker.GetComponentsInChildren<Collider>().Length!=0)throw new InvalidOperationException("参观范围不应该有物理碰撞体。");
    }
    [Serializable] sealed class Report { public int passed;public List<string> failures=new List<string>();public bool questVerified; }
    [MenuItem("Apartment Phase 2/M7 Visit/Run Visit Checks %#&2")]
    public static void Check()
    {
        var report=new Report();
        Action<bool,string> check=(value,name)=>{if(value)report.passed++;else report.failures.Add(name);};
        Action<ApartmentTourProgress,int> dwell=(p,room)=>{for(int i=0;i<8;i++)p.Observe(room,.1f);};
        try
        {
            var scene=RequireScene();Validate(scene);var tracker=ApartmentMenuUpgrade.All<ApartmentTourTracker>(scene).Single();
            foreach(ApartmentMovementMode mode in Enum.GetValues(typeof(ApartmentMovementMode)))
            {
                string keys=ApartmentMenuController.KeyText(mode);
                check(!keys.Contains("未分配")&&!keys.Contains("握把")&&!keys.Contains("X、Y")&&!keys.Contains("系统按键"),"only assigned keys: "+mode);
                check(keys.Contains("右手 B")&&keys.Contains("右食指扳机"),"menu and trigger documented: "+mode);
            }
            var p=new ApartmentTourProgress(6);check(!p.Complete,"new session incomplete");
            for(int room=0;room<6;room++)
            {
                p.Observe(room,.1f);check(!p.Entered(room),"brief boundary touch ignored "+room);
                dwell(p,room);check(p.Entered(room)&&!p.Exited(room),"must enter before exiting "+room);
                check(!p.Complete,"not complete while last room occupied "+room);
                dwell(p,-1);check(p.Exited(room),"stable departure counted "+room);
                check(p.Complete==(room==5),"complete only after every room exited "+room);
            }
            p.Suspend();check(p.Complete,"tracking loss retains completed rooms");
            check(!new ApartmentTourProgress(6).Complete,"next Play resets visits");
            p=new ApartmentTourProgress(6);dwell(p,0);dwell(p,5);
            check(p.Exited(0)&&p.Entered(5)&&!p.Entered(1)&&!p.Entered(2)&&!p.Complete,"teleport observes endpoint only");
            p=new ApartmentTourProgress(6);dwell(p,2);p.Suspend();dwell(p,-1);
            check(p.Entered(2)&&!p.Exited(2),"tracking loss or safety reset cannot fake departure");
            dwell(p,2);dwell(p,-1);check(p.Exited(2),"revisit after reset counts normal departure");
            p=new ApartmentTourProgress(6);p.Observe(0,100);check(!p.Entered(0),"stalled frame not a visit");
            p.Observe(99,.1f);p.Observe(0,float.NaN);check(!p.Complete,"invalid samples ignored");
            var points=new[]{new Vector3(4.4f,1.6f,3),new Vector3(2.5f,1.6f,5.5f),new Vector3(7.4f,1.6f,1.5f),new Vector3(2.5f,1.6f,2),new Vector3(2.5f,1.6f,3.5f),new Vector3(7,1.6f,6.3f)};
            for(int i=0;i<points.Length;i++)check(tracker.FindRegion(points[i])==i,"scene room endpoint "+tracker.rooms[i].roomName);
            check(tracker.FindRegion(new Vector3(4.25f,1.6f,-1.55f))==-1,"spawn platform not a room");
            check(tracker.FindRegion(new Vector3(3.18f,1.6f,3.5f))==-1,"wall does not count as room");
            check(tracker.FindRegion(new Vector3(1,6,1))==-1,"above roof not a room");
            check(tracker.FindRegion(new Vector3(3.1f,1.6f,3.5f),4)==4,"boundary hysteresis avoids doorway jitter");
            check(ApartmentMenuController.TourTitle=="您已看完整个公寓，您可以继续探索和标记注释或者退出游戏","completion uses approved exact title");
            check(ApartmentMenuController.Tabs.Last()=="退出游戏","exit entry available");
        }
        catch(Exception e){report.failures.Add(e.ToString());}
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/M7VisitChecks.json",JsonUtility.ToJson(report,true));
        if(report.failures.Count==0)Debug.Log("M7 checks PASS: "+report.passed+" assertions; Quest not verified.");
        else Debug.LogError("M7 checks FAIL: "+string.Join(" | ",report.failures));
    }
}
#endif
