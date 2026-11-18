#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRGroup10.ShoppingBag;

// Explicit scene-instance work only. No builder, source-prefab, input or project-settings edits.
public static class Phase3SpatialStabilization
{
    public const string ScenePath = "Assets/Scenes/Apartment_Detailed_VR.unity";
    public const string LogRoot = "Logs/Phase3Stabilization-20261026";
    static Scene scene;
    [Serializable] public sealed class Item {
        public string path, type, id; public Vector3 position, localPosition, scale, min, max;
        public bool active, enabled; public string detail;
    }
    [Serializable] public sealed class Report { public string evidence="Unity Editor imported scene geometry; not Quest/Player verification"; public List<Item> items=new List<Item>(); public List<string> checks=new List<string>(); }
    public static T[] All<T>() where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static string PathOf(Transform t) => t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
    public static long Id(UnityEngine.Object o) => unchecked((long)GlobalObjectId.GetGlobalObjectIdSlow(o).targetObjectId);
    public static T Find<T>(long id) where T:Component {
        var direct=All<T>().Where(o=>Id(o)==id).ToArray();if(direct.Length==1)return direct[0];
        string text=File.ReadAllText(ScenePath);
        var doc=Regex.Match(text,@"(?ms)^--- !u!\d+ &"+id+@"\b.*?(?=^--- !u!|\z)").Value;
        var source=Regex.Match(doc,@"m_CorrespondingSourceObject: \{fileID: (\d+)");
        var instance=Regex.Match(doc,@"m_PrefabInstance: \{fileID: (\d+)");
        if(source.Success&&instance.Success) {
            ulong src=ulong.Parse(source.Groups[1].Value),pref=ulong.Parse(instance.Groups[1].Value);
            var matches=All<T>().Where(o=>{var g=GlobalObjectId.GetGlobalObjectIdSlow(o);return g.targetObjectId==src&&g.targetPrefabId==pref;}).ToArray();
            if(matches.Length==1)return matches[0];
            throw new InvalidOperationException("Cannot resolve prefab scene ID "+id+" source="+src+" prefab="+pref+" candidates="+string.Join(";",All<T>().Where(o=>Id(o)==(long)src).Select(o=>GlobalObjectId.GetGlobalObjectIdSlow(o).ToString())));
        }
        throw new InvalidOperationException("Cannot resolve unique scene ID "+id+" as "+typeof(T).Name);
    }
    public static Transform Named(string name) => All<Transform>().Single(t=>t.name==name);
    public static void Open() {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
        Directory.CreateDirectory(LogRoot);
        scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
    }
    public static Bounds VisualBounds(Transform root) {
        var pts=root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.gameObject.activeInHierarchy&&m.sharedMesh!=null)
            .SelectMany(m=>m.sharedMesh.vertices.Select(v=>m.transform.TransformPoint(v))).ToArray();
        if(pts.Length==0) throw new InvalidOperationException("No imported visual vertices: "+root.name);
        var b=new Bounds(pts[0],Vector3.zero);foreach(var p in pts)b.Encapsulate(p);return b;
    }
    static Report Snapshot() {
        var r=new Report();
        foreach(var t in All<Transform>()) {
            if(!PathOf(t).Contains("DetailedEnvironment"))continue;
            var fs=t.GetComponents<MeshFilter>(); var cs=t.GetComponents<Collider>();
            if(fs.Length==0&&cs.Length==0&&t.GetComponent<ApartmentCabinetDoor>()==null)continue;
            var i=new Item{path=PathOf(t),type="Transform",id=Id(t).ToString(),position=t.position,localPosition=t.localPosition,scale=t.localScale,active=t.gameObject.activeInHierarchy,enabled=true};
            if(t.gameObject.activeInHierarchy && fs.Any(f=>f.sharedMesh!=null)) {var b=VisualBounds(t);i.min=b.min;i.max=b.max;}
            i.detail=string.Join("; ",cs.Select(c=>c.GetType().Name+":"+Id(c)+" enabled="+c.enabled+" trigger="+c.isTrigger));r.items.Add(i);
        }
        foreach(var d in All<ApartmentCabinetDoor>())r.items.Add(new Item{path=PathOf(d.transform),type="Door",id=Id(d).ToString(),position=d.transform.position,detail="angle="+d.openAngle+" collider="+Id(d.doorCollider)+" renderer="+Id(d.doorRenderer)+" body="+Id(d.hingeBody)});
        return r;
    }
    public static void Probe() {
        Open();File.WriteAllText(LogRoot+"/Unity-Before-Geometry.json",JsonUtility.ToJson(Snapshot(),true));
        Debug.Log("PHASE3_PROBE_COMPLETE "+Application.unityVersion);
    }
    static void Ground(long id,float supportTop) {
        var t=Find<Transform>(id);float dy=supportTop+.001f-VisualBounds(t).min.y;
        t.position+=Vector3.up*dy;PrefabUtility.RecordPrefabInstancePropertyModifications(t);EditorUtility.SetDirty(t);
    }
    public static void ApplyFurniture() {
        Open();
        if(File.Exists(LogRoot+"/Furniture-Applied.marker"))throw new InvalidOperationException("Furniture application already recorded; use Git review before rerunning.");
        var ids=new long[]{233736802,16152113,1858018937,732500854,1580692884,1337975014,247076002,47526949,730754861,1629213757,221027378,1212511184,1517314931,1247993134};
        foreach(var id in ids)Find<Transform>(id); // Resolve the entire plan before any mutation.
        string sha;using(var hash=SHA256.Create())sha=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(ScenePath))).Replace("-","").ToLowerInvariant();
        if(sha!="2294e27162eeefd988f1ad6d31ccaf004d63f2d4a40320ff472711f26086683d")throw new InvalidOperationException("Scene baseline SHA changed; do not apply a stale plan.");
        Ground(233736802,0f);float rugTop=VisualBounds(Find<Transform>(233736802)).max.y;
        foreach(long id in new long[]{16152113,1858018937})Ground(id,rugTop);
        foreach(long id in new long[]{732500854,1580692884,1337975014,247076002,47526949,730754861,1629213757})Ground(id,0f);
        foreach(long id in new long[]{221027378,1212511184,1517314931,1247993134})Ground(id,.012f);
        var support=Find<BoxCollider>(140617042);support.enabled=false;EditorUtility.SetDirty(support);PrefabUtility.RecordPrefabInstancePropertyModifications(support);
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
        File.WriteAllText(LogRoot+"/Furniture-Applied.marker","Scene-instance imported visual vertices aligned to support +1mm; source prefabs unchanged.");
        File.WriteAllText(LogRoot+"/Unity-After-Furniture.json",JsonUtility.ToJson(Snapshot(),true));
        Debug.Log("PHASE3_FURNITURE_APPLIED");
    }
    public static void ApplyDoors() {
        Open();if(File.Exists(LogRoot+"/Doors-Applied.marker"))throw new InvalidOperationException("Door edits already recorded.");
        Phase3SpatialDoorAndRoute.Apply();
        var errors=Phase3SpatialDoorAndRoute.CheckDoors();
        File.WriteAllText(LogRoot+"/Door-Apply-Checks.txt",string.Join("\n",Phase3SpatialDoorAndRoute.LastChecks.Concat(errors.Select(e=>"FAIL "+e))));
        if(errors.Count>0)throw new InvalidOperationException("Door candidate not saved: "+string.Join(" | ",errors));
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
        File.WriteAllText(LogRoot+"/Doors-Applied.marker","Coordinated scene-instance walls, finishes, leaf, solid, aim; all original door references retained.");
        File.WriteAllText(LogRoot+"/Unity-After-Doors.json",JsonUtility.ToJson(Snapshot(),true));Debug.Log("PHASE3_DOORS_APPLIED");
    }

    public static void ApplyLayout() {
        Open();if(File.Exists(LogRoot+"/Layout-Applied.marker"))throw new InvalidOperationException("Layout already recorded.");
        var report=Phase3SpatialLayout.ApplyCandidate(false,false);
        File.WriteAllText(LogRoot+"/Unity-Layout-Candidate-Refined-4.json",JsonUtility.ToJson(report,true));
        if(!report.accepted)throw new InvalidOperationException("Refined layout rejected and restored; no save: "+string.Join(" | ",report.failures));
        EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
        File.WriteAllText(LogRoot+"/Layout-Applied.marker",report.scope);
        File.WriteAllText(LogRoot+"/Unity-After-Layout.json",JsonUtility.ToJson(Snapshot(),true));Debug.Log("PHASE3_LAYOUT_APPLIED "+report.scope);
        Phase3SpatialVerification.RunAll();
    }
    public static void AdjustRightChair() {
        Open();if(File.Exists(LogRoot+"/Right-Chair-Adjusted.marker"))throw new InvalidOperationException("Right chair already adjusted.");
        var chair=Find<Transform>(1212511184);var original=chair.position;
        if(Mathf.Abs(original.x-8f)>.001f)throw new InvalidOperationException("Unexpected right-chair baseline.");
        var own=chair.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
        var others=All<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&!c.transform.IsChildOf(chair)).ToArray();
        var before=new Dictionary<Tuple<Collider,Collider>,float>();
        foreach(var a in own)foreach(var b in others){Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth);before[Tuple.Create(a,b)]=depth;}
        var notes=new List<string>();
        try {
            chair.position+=Vector3.right*.10f;Physics.SyncTransforms();
            foreach(var pair in before) {
                var a=pair.Key.Item1;var b=pair.Key.Item2;
                bool hit=Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth);
                if(hit&&depth>pair.Value+.002f)throw new InvalidOperationException("Chair adjustment adds penetration: "+PathOf(b.transform)+" "+depth);
            }
            float visualGap=VisualBounds(chair).min.x-VisualBounds(Find<Transform>(1517314931)).max.x;
            if(visualGap<.06f)throw new InvalidOperationException("Visible chair/table separation not established.");
            notes.Add("Right chair rootX8.0->8.1m; actual imported visual X separation from table="+visualGap+"m");
            var errors=Phase3SpatialDoorAndRoute.CheckDoors();if(errors.Count>0)throw new InvalidOperationException(string.Join(" | ",errors));
            var routes=Phase3SpatialRouteChecks.Run(false);
            if(routes.failures.Count>0||routes.routes.Count!=3||routes.routes.Any(r=>!r.strictRequestedGoalReached))throw new InvalidOperationException("Chair change failed exact capsule routes");
            PrefabUtility.RecordPrefabInstancePropertyModifications(chair);EditorUtility.SetDirty(chair);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
            File.WriteAllText(LogRoot+"/Right-Chair-Adjusted.marker",string.Join("\n",notes));
            File.WriteAllText(LogRoot+"/Unity-Final-Geometry.json",JsonUtility.ToJson(Snapshot(),true));
            Debug.Log("PHASE3_RIGHT_CHAIR_ADJUSTED "+visualGap);
        }catch{chair.position=original;Physics.SyncTransforms();throw;}
        Phase3SpatialVerification.RunAll();
    }
    public static void ProbeRoutes() {
        Open();var report=Phase3SpatialRouteChecks.Run();
        File.WriteAllText(LogRoot+"/Unity-Route-Probe.json",JsonUtility.ToJson(report,true));Debug.Log("PHASE3_ROUTE_PROBE_COMPLETE");
    }
}
#endif