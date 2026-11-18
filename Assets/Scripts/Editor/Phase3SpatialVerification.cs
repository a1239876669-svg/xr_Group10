#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGroup10.ShoppingBag;

// Geometry/physics checks do not claim hardware locomotion, comfort or building compliance.
public static class Phase3SpatialVerification
{
    [Serializable] public sealed class Group {public string name;public bool passed;public string[] errors;}
    [Serializable] public sealed class Report {public string evidence="Actual Unity EditMode imported meshes and Physics; Quest and Player Not Run";public List<Group> groups=new List<Group>();}
    public static readonly string[] Groups={"EnvelopeAndScale","FurnitureSupport","DoorsAndRecovery","CoreCapsulePaths","ExplicitContactReferences","TaskAnchorsAndData","ShoppingBagSizes"};
    static void Require(bool ok,string text,List<string> errors){if(!ok)errors.Add(text);}
    static bool Near(float a,float b,float tolerance=.0002f)=>Mathf.Abs(a-b)<=tolerance;
    public static string[] CheckGroup(string name)
    {
        var errors=new List<string>();Phase3SpatialStabilization.Open();
        try {
            switch(name) {
                case "EnvelopeAndScale":Envelope(errors);break;
                case "FurnitureSupport":Supports(errors);break;
                case "DoorsAndRecovery":
                    errors.AddRange(Phase3SpatialDoorAndRoute.CheckDoors());
                    Require(Near(Phase3SpatialStabilization.Find<Collider>(1897844609).bounds.max.y,0),"Entrance threshold must be flush",errors);
                    Require(Phase3SpatialStabilization.Find<ApartmentRoomDoor>(899562392).openAngle==-90,"Bedroom2 must open inward",errors);break;
                case "CoreCapsulePaths":
                    var route=Phase3SpatialRouteChecks.Run(false);
                    File.WriteAllText(Phase3SpatialStabilization.LogRoot+"/Unity-Route-Final.json",JsonUtility.ToJson(route,true));
                    errors.AddRange(route.failures);
                    Require(route.routes.Count==3&&route.routes.All(r=>r.success&&r.strictRequestedGoalReached),"All three explicit 0.30m capsule endpoints must connect; alternatives are not counted",errors);break;
                case "ExplicitContactReferences":Contacts(errors);break;
                case "TaskAnchorsAndData":Anchors(errors);break;
                case "ShoppingBagSizes":Bags(errors);break;
                default:throw new ArgumentException(name);
            }
        }catch(Exception exception){errors.Add(exception.GetType().Name+": "+exception.Message);}
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single); }
        return errors.ToArray();
    }
    static void Envelope(List<string> errors)
    {
        var floor=Phase3SpatialStabilization.Find<Transform>(1448635153);var bounds=Phase3SpatialStabilization.VisualBounds(floor);
        Require(Near(bounds.min.x,0)&&Near(bounds.min.z,0)&&Near(bounds.max.x,9.6f)&&Near(bounds.max.z,7.5f),"Original outer floor boundary must not expand",errors);
        var filter=floor.GetComponent<MeshFilter>();float y=bounds.max.y;
        var points=filter.sharedMesh.vertices.Select(v=>floor.TransformPoint(v)).Where(v=>Near(v.y,y,.00001f))
            .Select(v=>new Vector2(Mathf.Round(v.x*100000f)/100000f,Mathf.Round(v.z*100000f)/100000f)).Distinct().ToList();
        Require(points.Count==4,"Expected the verified closed four-corner floor top",errors);
        var center=points.Aggregate(Vector2.zero,(a,b)=>a+b)/points.Count;points=points.OrderBy(p=>Mathf.Atan2(p.y-center.y,p.x-center.x)).ToList();
        double area=0;for(int i=0;i<points.Count;i++){var a=points[i];var b=points[(i+1)%points.Count];area+=(double)a.x*b.y-(double)b.x*a.y;}
        Require(Math.Abs(Math.Abs(area)*.5-72)<.0001,"Actual closed top polygon must remain approximately72m2",errors);
        Require(Phase3SpatialStabilization.Named("Ceiling").gameObject.activeSelf==false,"No ceiling/lighting scope expansion",errors);
        var mover=Phase3SpatialStabilization.All<ApartmentVRLocomotion>().Single();
        Require(Near(mover.capsuleRadius,.30f)&&Near(mover.stepOffset,.20f),"Player capsule/step must not shrink/change",errors);
        Require(Vector3.Distance(mover.transform.lossyScale,Vector3.one)<.0001f,"XR/player metres must stay unscaled",errors);
    }
    static void Supports(List<string> errors)
    {
        float rugTop=Phase3SpatialStabilization.VisualBounds(Phase3SpatialStabilization.Find<Transform>(233736802)).max.y;
        foreach(long id in new long[]{233736802,1580692884,1337975014,247076002,47526949,730754861,1629213757})
            Require(Near(Phase3SpatialStabilization.VisualBounds(Phase3SpatialStabilization.Find<Transform>(id)).min.y,.001f),"Floor support mismatch "+id,errors);
        foreach(long id in new long[]{16152113,732500854,1858018937})
            Require(Near(Phase3SpatialStabilization.VisualBounds(Phase3SpatialStabilization.Find<Transform>(id)).min.y,rugTop+.001f),"Rug support mismatch "+id,errors);
        // The compact balcony set replaces these retained, intentionally disabled originals.
        foreach(long id in new long[]{221027378,1212511184,1517314931,1247993134})
            Require(!Phase3SpatialStabilization.Find<Transform>(id).gameObject.activeInHierarchy,"Legacy balcony furniture unexpectedly active "+id,errors);
        foreach(string name in new[]{"Balcony_Compact_Deck_Chair_Left","Balcony_Compact_Deck_Chair_Right","Balcony_Compact_Round_Coffee_Table"})
        {
            var furniture=Phase3SpatialStabilization.Named(name);
            Require(furniture.gameObject.activeInHierarchy,"Compact balcony furniture inactive: "+name,errors);
            Require(Near(Phase3SpatialStabilization.VisualBounds(furniture).min.y,.013f),"Compact balcony support mismatch: "+name,errors);
        }
        Require(!Phase3SpatialStabilization.Find<Collider>(140617042).enabled,"Redundant high carpet support must remain disabled",errors);
        Require(Phase3SpatialStabilization.Find<Collider>(1448635157).enabled,"Real floor must remain solid",errors);
    }
    static void Contacts(List<string> errors)
    {
        var owners=new Dictionary<Collider,BagContactTarget>();
        foreach(var target in Phase3SpatialStabilization.All<BagContactTarget>()) {
            var data=new SerializedObject(target);var colliders=data.FindProperty("furnitureColliders");var renderers=data.FindProperty("furnitureRenderers");
            Require(data.FindProperty("warningMaterial").objectReferenceValue!=null,"Missing warning material: "+target.name,errors);
            Require(colliders.arraySize>0&&renderers.arraySize>0,"Empty explicit target: "+target.name,errors);
            for(int i=0;i<colliders.arraySize;i++) {
                var c=colliders.GetArrayElementAtIndex(i).objectReferenceValue as Collider;
                Require(c!=null&&!c.isTrigger&&c.transform.IsChildOf(target.transform),"Invalid explicit solid: "+target.name,errors);
                if(c!=null){Require(!owners.ContainsKey(c),"Duplicate collider registration: "+c.name,errors);owners[c]=target;}
            }
            for(int i=0;i<renderers.arraySize;i++){var renderer=renderers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;Require(renderer!=null&&renderer.transform.IsChildOf(target.transform),"Missing/nonlocal feedback renderer: "+target.name,errors);}
        }
        foreach(long id in new long[]{527504760,962122311,1747743412,331741831,846168696,1373652525,1030523185,470823404})
            Require(Phase3SpatialStabilization.Find<Transform>(id).GetComponent<BagContactTarget>()!=null,"Missing modified-surface target "+id,errors);
        foreach(var t in Phase3SpatialStabilization.All<Transform>())Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script at "+Phase3SpatialStabilization.PathOf(t),errors);
    }
    static void Anchors(List<string> errors)
    {
        var journey=Phase3SpatialStabilization.All<MonoBehaviour>().Single(x=>x!=null&&x.GetType().Name=="ApartmentGuidedJourney");var data=new SerializedObject(journey);
        foreach(string field in new[]{"tutorial","menu","locomotion","interactor","roomMap","review","bagBridge","entranceDoor","balconyDoor","practiceCabinet","kitchenDropPoint"})
            Require(data.FindProperty(field).objectReferenceValue!=null,"Journey reference missing: "+field,errors);
        var drop=(Transform)data.FindProperty("kitchenDropPoint").objectReferenceValue;
        Require(Vector3.Distance(drop.position,new Vector3(2.1f,.012f,5.5f))<.0001f&&Near(data.FindProperty("dropFloorY").floatValue,.012f),"Kitchen task/floor datum must remain unchanged",errors);
        Require(Vector3.Distance(Phase3SpatialStabilization.Find<Transform>(1342695680).position,new Vector3(4.25f,0,-1.55f))<.0001f,"Safe reset anchor changed",errors);
        Require(QuestGuidance.Review.ReviewStorage.SceneKey(Phase3SpatialStabilization.ScenePath)=="2129619AB63EE0D1","Historical scene key changed",errors);
    }
    static void Bags(List<string> errors)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShoppingBag/Prefabs/ShoppingBag.prefab");var bag=prefab.GetComponent<ShoppingBag>();
        var sizes=new[]{new Vector3(.30f,.30f,.15f),new Vector3(.45f,.40f,.22f),new Vector3(.60f,.48f,.30f)};
        for(int i=0;i<3;i++)Require(Vector3.Distance(bag.Profile((BagSize)i).BodySize,sizes[i])<.00001f,"Original bag dimensions changed "+i,errors);
        Require(Vector3.Distance(prefab.transform.localScale,Vector3.one)<.0001f,"Bag root scale changed",errors);
    }
    public static void RunAll()
    {
        var report=new Report();foreach(var name in Groups){var errors=CheckGroup(name);report.groups.Add(new Group{name=name,passed=errors.Length==0,errors=errors});}
        File.WriteAllText(Phase3SpatialStabilization.LogRoot+"/Unity-Spatial-Checks.json",JsonUtility.ToJson(report,true));
        RenderBaseline();
        Phase3SpatialStabilization.Open();Render("Unity-Top-Down.png",new Vector3(4.8f,13,2.5f),new Vector3(4.8f,0,2.5f),true);Render("Unity-Oblique.png",new Vector3(11,11,-8),new Vector3(4.8f,.5f,3.5f),false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        if(report.groups.Any(g=>!g.passed))throw new InvalidOperationException("Spatial checks failed; inspect Unity-Spatial-Checks.json");
        Debug.Log("PHASE3_SPATIAL_CHECKS_PASS "+report.groups.Count+" groups; Quest/Player Not Run");
    }
    static void RenderBaseline()
    {
        string source=Phase3SpatialStabilization.LogRoot+"/Before/"+Phase3SpatialStabilization.ScenePath;
        const string temporary="Assets/Phase3_Baseline_Evidence_Temporary.unity";
        if(!File.Exists(source)){Debug.LogWarning("No local before snapshot available; no before image generated.");return;}
        if(File.Exists(temporary))throw new InvalidOperationException("Refusing to overwrite an existing evidence scene.");
        bool created=false;
        try {
            File.Copy(source,temporary,false);created=true;AssetDatabase.ImportAsset(temporary,ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(temporary,OpenSceneMode.Single);
            Render("Unity-Before-Top-Down.png",new Vector3(4.8f,13,2.5f),new Vector3(4.8f,0,2.5f),true);
            Render("Unity-Before-Oblique.png",new Vector3(11,11,-8),new Vector3(4.8f,.5f,3.5f),false);
        }
        finally {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            if(created&&!AssetDatabase.DeleteAsset(temporary))throw new IOException("Could not remove owned temporary evidence scene; inspect before continuing.");
        }
    }
    static void Render(string name,Vector3 from,Vector3 to,bool ortho)
    {
        var go=new GameObject("Phase3 temporary evidence camera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();camera.transform.position=from;camera.transform.LookAt(to,ortho?Vector3.forward:Vector3.up);camera.orthographic=ortho;camera.orthographicSize=6.5f;camera.fieldOfView=48;camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.17f,.19f);
        var texture=new RenderTexture(1600,1400,24);var old=RenderTexture.active;camera.targetTexture=texture;
        try{camera.Render();RenderTexture.active=texture;var image=new Texture2D(1600,1400,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1400),0,0);image.Apply();File.WriteAllBytes(Phase3SpatialStabilization.LogRoot+"/"+name,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);}
        finally{RenderTexture.active=old;camera.targetTexture=null;texture.Release();UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);}
    }
}
#endif