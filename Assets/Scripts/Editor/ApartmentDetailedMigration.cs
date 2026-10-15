#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Oculus.Interaction.Input;
using Object = UnityEngine.Object;

// Explicit user-invoked tools only. Source scenes and the original working VR scene are never saved here.
public static class ApartmentDetailedMigration
{
    public const string SourcePath = "Assets/Scenes/Apartment.unity";
    public const string OriginalVRPath = "Assets/Scenes/ApartmentPhase2_VR.unity";
    public const string TargetPath = "Assets/Scenes/Apartment_Detailed_VR.unity";
    public const string GeneratedFolder = "Assets/Apartment/DetailedMigration";
    public static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(o => o.GetComponentsInChildren<T>(true)).ToArray();
    public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    [Serializable] public class ObjectInfo
    {
        public string path;
        public bool active;
        public Vector3 position, scale, center, size;
        public string[] components;
        public string mesh;
        public int vertices;
    }
    [Serializable] public class SourceReport
    {
        public string scene;
        public string[] roots;
        public int objects, renderers, missingScripts;
        public ObjectInfo[] kitchen, doors, floors;
    }
    static ObjectInfo Info(Transform t)
    {
        var renderer = t.GetComponent<Renderer>(); var filter = t.GetComponent<MeshFilter>();
        var mesh = filter != null ? filter.sharedMesh : null;
        return new ObjectInfo { path = PathOf(t), active = t.gameObject.activeInHierarchy, position = t.position,
            scale = t.lossyScale, center = renderer != null ? renderer.bounds.center : t.position,
            size = renderer != null ? renderer.bounds.size : Vector3.zero,
            components = t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().FullName).ToArray(),
            mesh = mesh != null ? mesh.name : "", vertices = mesh != null ? mesh.vertexCount : 0 };
    }
    static void RequireCleanEditor()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("请先退出 Play，并等待导入和编译完成。");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前场景有未保存内容，工具不会丢弃或保存它。");
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Inspect Detailed Source (Read Only) %#&j")]
    public static void Inspect()
    {
        try
        {
            RequireCleanEditor();
            var scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
            var transforms = All<Transform>(scene);
            var report = new SourceReport { scene = scene.path, roots = scene.GetRootGameObjects().Select(g=>g.name).ToArray(),
                objects=transforms.Length, renderers=All<Renderer>(scene).Length,
                missingScripts=transforms.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                kitchen=transforms.Where(t=>PathOf(t).StartsWith("House/Fixtures/Kitchen",StringComparison.Ordinal)).Select(Info).ToArray(),
                doors=transforms.Where(t=>PathOf(t).StartsWith("House/Doors",StringComparison.Ordinal)).Select(Info).ToArray(),
                floors=transforms.Where(t=>t.name.StartsWith("Floor",StringComparison.Ordinal) ||
                    PathOf(t).StartsWith("EntrancePlatform",StringComparison.Ordinal)).Select(Info).ToArray() };
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/DetailedSourceInspection.json", JsonUtility.ToJson(report,true));
            var house=scene.GetRootGameObjects().Single(g=>g.name=="House");
            Selection.activeGameObject=house;
            if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Detailed source inspected WITHOUT saving. Objects="+report.objects+"; renderers="+
                report.renderers+"; missing scripts="+report.missingScripts+"; Logs/DetailedSourceInspection.json");
        }
        catch(Exception e) { Debug.LogError("Detailed inspection stopped: "+e); }
    }

    [MenuItem("Apartment Phase 2/M4 Detailed/Create Detailed VR Copy (Never Overwrite) %#&u")]
    public static void Create()
    {
        Scene baseline = default;
        try
        {
            RequireCleanEditor();
            if (File.Exists(TargetPath) || File.Exists(TargetPath+".meta"))
                throw new InvalidOperationException("精细 VR 副本已经存在；请打开并检查它，不会覆盖。");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder("Assets/Apartment", "DetailedMigration");
            if (!AssetDatabase.CopyAsset(SourcePath, TargetPath)) throw new IOException("无法创建独立场景副本。");
            var scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Single);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate tested locomotion and kitchen interaction to detailed copy");
            foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "Prepare detailed VR copy");
            var sourceRig = All<OVRCameraRig>(scene).Single(); Vector3 preferredSpawn = sourceRig.transform.position;
            baseline = EditorSceneManager.OpenScene(OriginalVRPath, OpenSceneMode.Additive);
            var originalMover = All<ApartmentVRLocomotion>(baseline).Single();
            int agent = originalMover.teleportSurface.agentTypeID;
            SceneManager.SetActiveScene(scene);
            var player = Object.Instantiate(originalMover.gameObject); player.name = "VRPlayer";
            SceneManager.MoveGameObjectToScene(player,scene);
            Undo.RegisterCreatedObjectUndo(player,"Copy tested player settings");
            var mover = player.GetComponent<ApartmentVRLocomotion>();
            // Keep the detailed scene's existing tracked hand/controller visuals, but only one mover.
            Object.DestroyImmediate(mover.rig.gameObject);
            mover.rig = sourceRig; mover.manager = sourceRig.GetComponent<OVRManager>();
            sourceRig.transform.SetParent(player.transform,false);
            sourceRig.transform.localPosition = Vector3.zero; sourceRig.transform.localRotation=Quaternion.identity;
            sourceRig.transform.localScale=Vector3.one;
            sourceRig.name="OVRCameraRig"; sourceRig.enabled=true; sourceRig.usePerEyeCameras=false;
            var managerData = new SerializedObject(mover.manager);
            managerData.FindProperty("_trackingOriginType").intValue=(int)OVRManager.TrackingOrigin.FloorLevel;
            managerData.ApplyModifiedProperties();
            foreach(var emulator in sourceRig.GetComponentsInChildren<OVRHeadsetEmulator>(true)) emulator.enabled=false;
            foreach(var camera in sourceRig.GetComponentsInChildren<Camera>(true))
            { camera.enabled = camera.transform == sourceRig.centerEyeAnchor; camera.tag=camera.enabled?"MainCamera":"Untagged"; }
            foreach(var listener in sourceRig.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = listener.transform == sourceRig.centerEyeAnchor;
            foreach(var behaviour in sourceRig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behaviour==null) continue;
                string type=behaviour.GetType().FullName;
                if(type.StartsWith("Oculus.Interaction.Locomotion.",StringComparison.Ordinal) ||
                    type.StartsWith("QuestGuidance.Locomotion.",StringComparison.Ordinal) ||
                    behaviour.GetType().Name=="JoystickLocomotion" || behaviour.GetType().Name=="OVRPlayerController")
                    behaviour.enabled=false;
            }
            DisableLegacyViewMasks(sourceRig);
            foreach(string name in new[]{"TeleportZones","LocomotionUIModule","PointableCanvasModule"})
                foreach(var root in scene.GetRootGameObjects().Where(g=>g.name==name)) root.SetActive(false);
            mover.turnMode=ApartmentTurnMode.Smooth; mover.debugMode=false;
            mover.desktopCamera.gameObject.SetActive(false); mover.desktopCamera.enabled=false;
            player.transform.SetPositionAndRotation(preferredSpawn,Quaternion.identity);
            var owner=player.GetComponentInChildren<ApartmentFurnitureInteractor>(true);
            owner.locomotion=mover; owner.doors=Array.Empty<ApartmentCabinetDoor>();
            EditorSceneManager.CloseScene(baseline,true); baseline=default;
            var environment = new GameObject("DetailedEnvironment"); Undo.RegisterCreatedObjectUndo(environment,"Create map collection root");
            foreach(string name in new[]{"House","EntrancePlatform"})
                scene.GetRootGameObjects().Single(g=>g.name==name).transform.SetParent(environment.transform,true);
            int floorLayer=LayerMask.NameToLayer("TeleportFloor");
            if(floorLayer<0) throw new InvalidOperationException("现有 TeleportFloor 分类丢失。");
            foreach(var collider in environment.GetComponentsInChildren<Collider>(true))
                if(collider.name=="Floor" || collider.name.StartsWith("Floor_",StringComparison.Ordinal) || collider.name.EndsWith("_Carpet",StringComparison.Ordinal) ||
                    PathOf(collider.transform).EndsWith("EntrancePlatform/Slab",StringComparison.Ordinal) ||
                    PathOf(collider.transform).EndsWith("EntrancePlatform/Threshold",StringComparison.Ordinal))
                    collider.gameObject.layer=floorLayer;
            var kitchen=environment.transform.Find("House/Fixtures/Kitchen");
            var doors=new List<ApartmentCabinetDoor>();
            BuildCabinet(kitchen.Find("BaseCab_W"),true,4,owner,doors);
            BuildCabinet(kitchen.Find("BaseCab_N"),false,2,owner,doors);
            owner.doors=doors.ToArray();
            // Existing room doors retain their authored automatic-near-hand behavior; reconnect to the retained rig.
            var rigRef=sourceRig.GetComponent<OVRCameraRigRef>();
            if(rigRef==null) rigRef=sourceRig.gameObject.AddComponent<OVRCameraRigRef>();
            rigRef.InjectAllOVRCameraRigRef(sourceRig,false);
            foreach(var autoDoor in environment.GetComponentsInChildren<QuestGuidance.Locomotion.DoorAutoSwing>(true))
            {
                var data=new SerializedObject(autoDoor); data.FindProperty("rigRef").objectReferenceValue=rigRef; data.ApplyModifiedProperties();
                var modifier=autoDoor.GetComponent<NavMeshModifier>();
                if(modifier==null) modifier=autoDoor.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild=true; modifier.applyToChildren=true;
            }
            var surface=environment.AddComponent<NavMeshSurface>(); surface.agentTypeID=agent;
            surface.collectObjects=CollectObjects.Children; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask=~0; surface.defaultArea=NavMesh.GetAreaFromName("Walkable");
            surface.overrideVoxelSize=true; surface.voxelSize=.05f;
            var surfaceData=new SerializedObject(surface); surfaceData.FindProperty("m_GenerateLinks").boolValue=false; surfaceData.ApplyModifiedProperties();
            Physics.SyncTransforms(); surface.BuildNavMesh();
            if(surface.navMeshData==null) throw new InvalidOperationException("精细场景导航地图烘焙失败。");
            AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            mover.teleportEnabled=true; mover.teleportSurface=surface; mover.teleportFloorMask=1<<floorLayer;
            if(!ClearSpawn(mover,preferredSpawn)) throw new InvalidOperationException("同学的出生平台位置没有足够的站立空间。");
            ValidateConfiguration(scene);
            foreach(var component in new Component[]{mover,owner,sourceRig,mover.manager,surface,rigRef}) EditorUtility.SetDirty(component);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("精细 VR 场景保存失败。");
            // Flush before saving so subsequent Undo never produces an apparently clean half-restored graph.
            Undo.FlushUndoRecordObjects();
            Selection.activeGameObject=environment;
            if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Detailed migration SAVED: "+TargetPath+"; 6 cabinet doors; one player; smooth turn/right-A teleport; source scene and old VR unchanged. Hardware acceptance still required.");
        }
        catch(Exception e) { Debug.LogError("Detailed migration stopped: "+e+"\nOnly the new copy may contain partial work; source and baseline remain unchanged."); }
        finally { if(baseline.IsValid()&&baseline.isLoaded) EditorSceneManager.CloseScene(baseline,true); }
    }

    static bool ClearSpawn(ApartmentVRLocomotion mover,Vector3 preferred)
    {
        Physics.SyncTransforms();
        var capsule=mover.GetComponent<CharacterController>();
        float radius=capsule.radius-capsule.skinWidth;
        var bottom=preferred+Vector3.up*capsule.radius;
        var top=preferred+Vector3.up*(capsule.height-capsule.radius);
        return !Physics.OverlapCapsule(bottom,top,radius,~0,QueryTriggerInteraction.Ignore)
            .Any(c=>c.gameObject.scene==mover.gameObject.scene && !c.transform.IsChildOf(mover.transform));
    }
    static void DisableLegacyViewMasks(OVRCameraRig rig)
    {
        // The old SDK drivers are disabled, so their independent renderer must not retain a default tunnel.
        foreach(var effect in rig.GetComponentsInChildren<Oculus.Interaction.TunnelingEffect>(true))
        {
            if(effect.transform==rig.transform || rig.transform.IsChildOf(effect.transform))
                throw new InvalidOperationException("遮罩与相机根物体共用对象，不能直接停用。");
            var mesh=new SerializedObject(effect).FindProperty("_meshFilter").objectReferenceValue as MeshFilter;
            var renderer=mesh!=null?mesh.GetComponent<Renderer>():null;
            if(renderer!=null){Undo.RecordObject(renderer,"Disable retained legacy tunnel renderer");renderer.enabled=false;}
            Undo.RecordObject(effect,"Disable orphaned SDK tunnel effect");effect.enabled=false;
            Undo.RecordObject(effect.gameObject,"Hide only the old tunnel effect object");effect.gameObject.SetActive(false);
        }
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Disable Retained Legacy View Masks %#&n")]
    public static void FixViewMasks()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath||SceneManager.sceneCount!=1)throw new InvalidOperationException("只允许处理精细 VR 副本的旧移动遮罩。");
            var rig=All<OVRCameraRig>(scene).Single();DisableLegacyViewMasks(rig);
            Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Retained old SDK locomotion tunnels disabled in detailed copy; hands, controllers, camera and new movement unchanged.");
        }
        catch(Exception e){Debug.LogError("Legacy view mask cleanup stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Reopen Saved Detailed VR %#&v")]
    public static void Reopen()
    {
        try
        {
            RequireCleanEditor();
            var scene=EditorSceneManager.OpenScene(TargetPath,OpenSceneMode.Single);
            Selection.activeGameObject=All<ApartmentVRLocomotion>(scene).Single().teleportSurface.gameObject;
            Debug.Log("Reopened saved detailed VR copy without writing any scene.");
        }
        catch(Exception e) {Debug.LogError("Detailed reopen stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Refine Generated Cabinet Hinges %#&b")]
    public static void RefineHinges()
    {
        try
        {
            RequireCleanEditor(); var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath || SceneManager.sceneCount!=1) throw new InvalidOperationException("只允许更新精细 VR 副本的新柜门。");
            foreach(var door in All<ApartmentCabinetDoor>(scene))
            {
                if(door.State!=ApartmentCabinetState.Closed) throw new InvalidOperationException("先让柜门处于关闭状态。");
                var leaf=door.doorCollider.transform; var bounds=door.doorCollider.bounds;
                Vector3 p=door.transform.position, leafPosition=leaf.position;
                // Put the hinge on the outer front corner, not halfway through the 18mm panel.
                // This keeps the panel thickness inside its own span when adjacent doors swing outward.
                if(door.transform.parent.name=="BaseCab_W_Interactive") p.x=bounds.max.x;
                else if(door.transform.parent.name=="BaseCab_N_Interactive") p.z=bounds.min.z;
                else throw new InvalidOperationException("不是此工具生成的柜门。");
                Undo.RecordObjects(new Object[]{door.transform,leaf},"Refine generated hinge without moving closed panel");
                door.transform.position=p; leaf.position=leafPosition;
            }
            Physics.SyncTransforms(); Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Six generated hinges refined; closed panel poses, cabinet dimensions and materials unchanged.");
        }
        catch(Exception e) {Debug.LogError("Detailed hinge refinement stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Apply Approved Chair Clearance %#&h")]
    public static void AdjustChair()
    {
        try
        {
            RequireCleanEditor(); var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath || SceneManager.sceneCount!=1) throw new InvalidOperationException("只允许调整精细 VR 副本。");
            var mover=All<ApartmentVRLocomotion>(scene).Single();
            var chair=mover.teleportSurface.transform.Find("House/Furniture/LivingRoom/SofaChairSet8_Chair1");
            const string markerName="Approved clearance -0.30X -0.15Z";
            if(chair.Find(markerName)!=null) {Debug.Log("Approved chair clearance already applied; no further movement."); return;}
            Undo.RecordObject(chair,"User-approved detailed-copy chair clearance"); chair.position+=new Vector3(-.30f,0,-.15f);
            var marker=new GameObject(markerName); marker.transform.SetParent(chair,false); Undo.RegisterCreatedObjectUndo(marker,"Remember idempotent approved adjustment");
            // Disable the old SDK's own body colliders, not its hand/controller visuals or door grab colliders.
            foreach(var behaviour in mover.rig.GetComponentsInChildren<MonoBehaviour>(true))
                if(behaviour!=null && behaviour.GetType().FullName=="Oculus.Interaction.Locomotion.CharacterController")
                    foreach(var c in behaviour.GetComponents<Collider>()) {Undo.RecordObject(c,"Only one player collision owner"); c.enabled=false;}
            Undo.RecordObject(mover.teleportSurface,"Accurate detailed map after approved chair movement");
            mover.teleportSurface.voxelSize=.025f;
            Physics.SyncTransforms(); mover.teleportSurface.BuildNavMesh();
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            Undo.FlushUndoRecordObjects(); EditorUtility.SetDirty(mover.teleportSurface); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Approved chair clearance saved only in detailed VR copy: -0.30m X, -0.15m Z; all dimensions unchanged.");
        }
        catch(Exception e) {Debug.LogError("Chair clearance stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Refresh Detailed Ground and Map %#&p")]
    public static void RefreshGround()
    {
        try
        {
            RequireCleanEditor(); var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath || SceneManager.sceneCount!=1) throw new InvalidOperationException("只允许更新精细 VR 副本。");
            var mover=All<ApartmentVRLocomotion>(scene).Single(); int layer=LayerMask.NameToLayer("TeleportFloor");
            foreach(var carpet in mover.teleportSurface.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.EndsWith("_Carpet",StringComparison.Ordinal)))
            {
                Undo.RecordObject(carpet.gameObject,"Classify real carpet support"); carpet.gameObject.layer=layer;
                // The imported rug is a floating, zero-thickness triangle sheet 12cm above the base floor.
                // Give it an actual ground-connected support collider without moving/replacing its visible mesh.
                var support=carpet.transform.Find("CarpetGroundSupport");
                if(support==null)
                {
                    Bounds b=carpet.bounds; var go=new GameObject("CarpetGroundSupport",typeof(BoxCollider));
                    Undo.RegisterCreatedObjectUndo(go,"Connect carpet collision to underlying floor");
                    go.transform.SetParent(mover.teleportSurface.transform,false); go.transform.position=new Vector3(b.center.x,(b.max.y-.01f)*.5f,b.center.z);
                    go.layer=layer; go.GetComponent<BoxCollider>().size=new Vector3(b.size.x,b.max.y+.01f,b.size.z);
                    // Store under the renderer while preserving the world-space collider footprint.
                    go.transform.SetParent(carpet.transform,true); support=go.transform;
                }
                foreach(var collider in carpet.GetComponents<Collider>())
                { Undo.RecordObject(collider,"Keep original floating rug collider recoverable"); collider.enabled=false; }
            }
            Undo.RecordObject(mover.teleportSurface,"Bake detailed geometry after import and save");
            // Keep small indoor regions for route validation; do not trim them just for being <2 square metres.
            // Radius/height and live collision checks still decide whether a player actually fits.
            mover.teleportSurface.minRegionArea=0f;
            Physics.SyncTransforms(); mover.teleportSurface.BuildNavMesh();
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            EditorUtility.SetDirty(mover.teleportSurface); AssetDatabase.SaveAssets(); Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Detailed ground classification and fresh independent navigation map saved; geometry unchanged.");
        }
        catch(Exception e) { Debug.LogError("Detailed ground update stopped: "+e); }
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Apply Approved Indoor Furniture Clearance %#&y")]
    public static void AdjustIndoorFurniture()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath||SceneManager.sceneCount!=1) throw new InvalidOperationException("只允许调整已批准的精细 VR 副本家具。");
            var mover=All<ApartmentVRLocomotion>(scene).Single();var env=mover.teleportSurface.transform;
            const string markerName="Approved indoor furniture clearance - balcony deferred";
            if(env.Find(markerName)!=null) {Debug.Log("Approved indoor clearance already applied; nothing moved again.");return;}
            var moves=new Dictionary<Transform,Vector3>();
            moves.Add(env.Find("House/Furniture/LivingRoom/SofaChairSet8_Sofa"),new Vector3(.15f,0,0));
            moves.Add(env.Find("House/Furniture/LivingRoom/Plant"),new Vector3(0,0,-.55f));
            foreach(string name in new[]{"DiningSet3_Chair","DiningSet3_Chair (1)"})
            {
                var root=env.Find("House/Furniture/LivingRoom/"+name);
                var parts=root.GetComponentsInChildren<Renderer>().Where(r=>r.bounds.center.x<4.3f).Select(r=>r.transform).Distinct().ToArray();
                if(parts.Length!=2 || parts.Sum(p=>p.GetComponents<Collider>().Length)!=3)
                    throw new InvalidOperationException("左侧餐椅构成与核对结果不一致；不会猜测移动。");
                foreach(var part in parts.Where(p=>!parts.Any(other=>other!=p&&p.IsChildOf(other))))
                    moves.Add(part,new Vector3(.55f,0,-.50f));
            }
            // Re-check ALL proposed positions together, not separately against the old placement.
            var colliders=env.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            Func<Collider,Vector3> offset=c=>moves.Where(m=>c.transform==m.Key||c.transform.IsChildOf(m.Key)).Select(m=>m.Value).FirstOrDefault();
            foreach(var part in colliders.Where(c=>offset(c)!=Vector3.zero)) foreach(var other in colliders)
            {
                if(part==other)continue;
                bool was=Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,
                    other.transform.position,other.transform.rotation,out _,out float oldDepth);
                bool now=Physics.ComputePenetration(part,part.transform.position+offset(part),part.transform.rotation,other,
                    other.transform.position+offset(other),other.transform.rotation,out _,out float depth);
                if(now&&depth>(was?oldDepth:0f)+.001f)
                    throw new InvalidOperationException("移动会产生新的重叠："+PathOf(part.transform)+" -> "+PathOf(other.transform));
            }
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Approved indoor furniture clearance in VR copy only");
            foreach(var move in moves) {Undo.RecordObject(move.Key,"Keep dimensions, move approved furniture");move.Key.position+=move.Value;}
            var marker=new GameObject(markerName);marker.transform.SetParent(env,false);Undo.RegisterCreatedObjectUndo(marker,"Remember approved copy-only layout adaptation");
            Undo.RecordObject(mover.teleportSurface,"Bake after approved furniture clearance");
            Physics.SyncTransforms();mover.teleportSurface.BuildNavMesh();
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorUtility.SetDirty(mover.teleportSurface);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Approved furniture adjustment saved only in detailed copy: sofa +15cm X, plant -55cm Z, two left dining chairs +55cm X/-50cm Z. Balcony doors unchanged.");
        }
        catch(Exception e) {Debug.LogError("Approved indoor clearance stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Apply Approved Entrance Cabinet Clearance %#&e")]
    public static void AdjustEntranceCabinet()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath||SceneManager.sceneCount!=1)throw new InvalidOperationException("只允许调整精细 VR 副本。");
            var mover=All<ApartmentVRLocomotion>(scene).Single();var env=mover.teleportSurface.transform;
            const string markerName="Approved entrance cabinet -0.20Z";
            if(env.Find(markerName)!=null){Debug.Log("Approved entrance clearance already applied; nothing moved again.");return;}
            var roots=new[]{env.Find("House/Fixtures/Entrance/ShoeCab"),env.Find("House/Fixtures/Entrance/ShoeCabTop")};
            Vector3 delta=new Vector3(0,0,-.20f);
            var all=env.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            var parts=roots.SelectMany(t=>t.GetComponentsInChildren<Collider>()).Where(c=>c.enabled&&!c.isTrigger).ToArray();
            foreach(var part in parts)foreach(var other in all)
            {
                if(parts.Contains(other))continue;
                bool was=Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,
                    other.transform.position,other.transform.rotation,out _,out float oldDepth);
                bool now=Physics.ComputePenetration(part,part.transform.position+delta,part.transform.rotation,other,
                    other.transform.position,other.transform.rotation,out _,out float depth);
                if(now&&depth>(was?oldDepth:0)+.001f)throw new InvalidOperationException("鞋柜会产生新重叠："+PathOf(other.transform));
            }
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("User-approved 20cm entrance cabinet clearance in detailed copy");
            foreach(var root in roots){Undo.RecordObject(root,"Move approved shoe cabinet and top, not mirror");root.position+=delta;}
            var marker=new GameObject(markerName);marker.transform.SetParent(env,false);Undo.RegisterCreatedObjectUndo(marker,"Remember approved entrance adjustment");
            Undo.RecordObject(mover.teleportSurface,"Bake final entrance clearance");Physics.SyncTransforms();mover.teleportSurface.BuildNavMesh();
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorUtility.SetDirty(mover.teleportSurface);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Approved shoe cabinet and top moved -20cm Z in detailed copy only; mirror/chair/walls/dimensions untouched.");
        }
        catch(Exception e){Debug.LogError("Entrance clearance stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Apply Verified Final Indoor Clearance %#&g")]
    public static void ApplyFinalClearance()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath||SceneManager.sceneCount!=1)throw new InvalidOperationException("只允许调整精细 VR 副本。");
            var mover=All<ApartmentVRLocomotion>(scene).Single();var env=mover.teleportSurface.transform;
            const string markerName="Verified final sofa and dining clearance - no chair movement";
            if(env.Find(markerName)!=null){Debug.Log("Verified final clearance already applied; no repeated movement.");return;}
            if(env.Find("Approved entrance cabinet -0.20Z")==null || env.Find("Approved indoor furniture clearance - balcony deferred")==null)
                throw new InvalidOperationException("先完成已批准的鞋柜和室内调整。");
            // Refresh the hypothetical collision/navigation proof against exactly the current saved placement.
            InspectAlternatives();var report=JsonUtility.FromJson<AlternativeReport>(File.ReadAllText("Logs/DetailedClearanceAlternatives.json"));
            // Prefer a 2.5cm margin beyond the first passing option rather than a voxel-edge-only sliver.
            var candidate=report.candidates.Last(c=>string.IsNullOrEmpty(c.failure)&&c.chairZ==0&&c.diningYaw==0&&
                c.roomRoutes!=null&&c.roomRoutes.Length==6&&c.roomRoutes.All(v=>v));
            var sofa=env.Find("House/Furniture/LivingRoom/SofaChairSet8_Sofa");
            var dining=env.Find("House/Furniture/LivingRoom/DiningSet3_Chair").GetComponentsInChildren<Renderer>()
                .Where(r=>r.bounds.center.x<4.3f).Select(r=>r.transform).Distinct().ToArray();
            if(dining.Length!=2)throw new InvalidOperationException("已核对的餐椅部件发生变化。");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Verified approved final indoor furniture clearance");
            Undo.RecordObject(sofa,"Clear connected route without changing body size");sofa.position+=Vector3.forward*candidate.sofaZ;
            foreach(var part in dining){Undo.RecordObject(part,"Keep nearby dining chair clear of sofa and table legs");part.position+=new Vector3(candidate.diningX,0,candidate.diningZ);}
            var marker=new GameObject(markerName);marker.transform.SetParent(env,false);Undo.RegisterCreatedObjectUndo(marker,"Remember final copy-only clearance");
            Undo.RecordObject(mover.teleportSurface,"Bake verified indoor clearance");Physics.SyncTransforms();mover.teleportSurface.BuildNavMesh();
            AssetDatabase.CreateAsset(mover.teleportSurface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/DetailedTeleportNav.asset"));
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorUtility.SetDirty(mover.teleportSurface);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Verified final indoor clearance saved in copy only: sofa +"+candidate.sofaZ+"m Z; nearest left dining chair "+candidate.diningX+
                "m X/+"+candidate.diningZ+"m Z; chair, balcony, source scenes and all dimensions unchanged.");
        }
        catch(Exception e){Debug.LogError("Final clearance stopped: "+e);}
    }
    static void BuildCabinet(Transform original,bool frontX,int count,ApartmentFurnitureInteractor owner,List<ApartmentCabinetDoor> doors)
    {
        if(original==null) throw new InvalidOperationException("精细厨房缺少已核对的柜体。");
        var template=original.GetComponent<MeshRenderer>(); var mesh=original.GetComponent<MeshFilter>().sharedMesh;
        if(mesh==null || mesh.vertexCount!=24 || Quaternion.Angle(original.rotation,Quaternion.identity)>.01f)
            throw new InvalidOperationException("柜体不是已核对的轴对齐分面网格。");
        var bounds=template.bounds; var root=new GameObject(original.name+"_Interactive");
        Undo.RegisterCreatedObjectUndo(root,"Create hollow detailed cabinet"); root.transform.SetParent(original.parent,false);
        const float t=.018f, reveal=.03f, gap=.004f;
        float y0=bounds.min.y, y1=bounds.max.y;
        Panel(root.transform,"Bottom",template,new Bounds(new Vector3(bounds.center.x,y0+t/2,bounds.center.z),new Vector3(bounds.size.x,t,bounds.size.z)));
        Panel(root.transform,"Top",template,new Bounds(new Vector3(bounds.center.x,y1-t/2,bounds.center.z),new Vector3(bounds.size.x,t,bounds.size.z)));
        float inside=bounds.size.y-2*t;
        if(frontX)
        {
            Panel(root.transform,"Back",template,new Bounds(new Vector3(bounds.min.x+t/2,bounds.center.y,bounds.center.z),new Vector3(t,inside,bounds.size.z)));
            foreach(int sign in new[]{-1,1}) Panel(root.transform,sign<0?"SideSouth":"SideNorth",template,
                new Bounds(new Vector3(bounds.center.x-reveal/2,bounds.center.y,bounds.center.z+sign*(bounds.extents.z-t/2)),new Vector3(bounds.size.x-reveal,inside,t)));
        }
        else
        {
            Panel(root.transform,"Back",template,new Bounds(new Vector3(bounds.center.x,bounds.center.y,bounds.max.z-t/2),new Vector3(bounds.size.x,inside,t)));
            foreach(int sign in new[]{-1,1}) Panel(root.transform,sign<0?"SideWest":"SideEast",template,
                new Bounds(new Vector3(bounds.center.x+sign*(bounds.extents.x-t/2),bounds.center.y,bounds.center.z+reveal/2),new Vector3(t,inside,bounds.size.z-reveal)));
        }
        float totalSpan=(frontX?bounds.size.z:bounds.size.x)-2*t;
        var cells=Enumerable.Repeat(totalSpan/count,count).ToArray();
        if(frontX && count==4)
        {
            // The north run projects into the sweep of an equal-width last door. Vary only the NEW door splits,
            // not either authored cabinet footprint: keep a 4cm clearance to that adjacent run at 90 degrees.
            var north=original.parent.Find("BaseCab_N").GetComponent<Renderer>().bounds;
            cells[count-1]=Mathf.Min(cells[count-1],north.min.x-bounds.max.x-.04f);
            if(cells[count-1]<.25f) throw new InvalidOperationException("北侧转角没有足够空间容纳活动门，不能自动调整柜体。");
            for(int i=0;i<count-1;i++) cells[i]=(totalSpan-cells[count-1])/(count-1);
        }
        float cursor=(frontX?bounds.min.z:bounds.min.x)+t;
        for(int i=0;i<count;i++)
        {
            float width=cells[i]-gap;
            float axis=cursor+cells[i]*.5f; cursor+=cells[i];
            var size=frontX?new Vector3(t,inside-gap,width):new Vector3(width,inside-gap,t);
            var center=frontX?new Vector3(bounds.max.x-t/2,bounds.center.y,axis):new Vector3(axis,bounds.center.y,bounds.min.z+t/2);
            var leaf=Panel(root.transform,"Door_"+(i+1),template,new Bounds(center,size));
            var pivot=new GameObject("Door_"+(i+1)+"_Hinge"); Undo.RegisterCreatedObjectUndo(pivot,"Create detailed cabinet hinge");
            pivot.transform.SetParent(root.transform,false);
            Vector3 edge=(frontX?Vector3.forward:Vector3.right)*width*.5f*(i%2==0?-1f:1f);
            pivot.transform.position=center+edge+(frontX?Vector3.right:Vector3.back)*t*.5f; leaf.transform.SetParent(pivot.transform,true);
            var body=pivot.AddComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
            var modifier=pivot.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild=true; modifier.applyToChildren=true;
            var door=pivot.AddComponent<ApartmentCabinetDoor>(); door.doorCollider=leaf.GetComponent<BoxCollider>();
            door.doorRenderer=leaf.GetComponent<MeshRenderer>(); door.hingeBody=body; door.inputOwner=owner;
            door.openAngle=i%2==0?90f:-90f; door.degreesPerSecond=90f; door.closedLocalRotation=Quaternion.identity;
            doors.Add(door); EditorUtility.SetDirty(door);
        }
        var volume=root.AddComponent<NavMeshModifierVolume>(); volume.center=bounds.center;
        volume.size=bounds.size+Vector3.up*.2f; volume.area=NavMesh.GetAreaFromName("Not Walkable");
        original.gameObject.SetActive(false); // Preserve the authored solid cube and its exact materials/mesh for recovery.
    }
    // Reuse each original face's material, normal and planar UV mapping, not a newly chosen substitute material.
    static GameObject Panel(Transform parent,string name,MeshRenderer template,Bounds bounds)
    {
        var source=template.GetComponent<MeshFilter>().sharedMesh; var mesh=Object.Instantiate(source);
        mesh.name=parent.name+"_"+name; var oldVertices=source.vertices; var vertices=new Vector3[oldVertices.Length];
        var uv=source.uv; var newUv=(Vector2[])uv.Clone(); var normals=source.normals;
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 world=template.transform.TransformPoint(oldVertices[i]); Vector3 min=template.bounds.min,max=template.bounds.max;
            vertices[i]=new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,Mathf.InverseLerp(min.x,max.x,world.x)),
                Mathf.Lerp(bounds.min.y,bounds.max.y,Mathf.InverseLerp(min.y,max.y,world.y)),
                Mathf.Lerp(bounds.min.z,bounds.max.z,Mathf.InverseLerp(min.z,max.z,world.z)))-bounds.center;
            normals[i]=template.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(normals[i]).normalized;
        }
        for(int s=0;s<source.subMeshCount;s++)
        {
            int[] triangles=source.GetTriangles(s);
            for(int j=0;j<triangles.Length;j+=3)
            {
                int a=triangles[j],b=triangles[j+1],c=triangles[j+2];
                Vector3 e1=oldVertices[b]-oldVertices[a],e2=oldVertices[c]-oldVertices[a];
                float aa=Vector3.Dot(e1,e1),bb=Vector3.Dot(e2,e2),ab=Vector3.Dot(e1,e2),det=aa*bb-ab*ab;
                if(Mathf.Abs(det)<.0000001f) continue;
                foreach(int index in new[]{a,b,c})
                {
                    Vector3 point=template.transform.InverseTransformPoint(vertices[index]+bounds.center)-oldVertices[a];
                    float p=Vector3.Dot(point,e1),q=Vector3.Dot(point,e2);
                    newUv[index]=uv[a]+(uv[b]-uv[a])*((p*bb-q*ab)/det)+(uv[c]-uv[a])*((q*aa-p*ab)/det);
                }
            }
        }
        mesh.vertices=vertices; mesh.uv=newUv; mesh.normals=normals; mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder+"/"+mesh.name+".asset"));
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
        Undo.RegisterCreatedObjectUndo(go,"Create detailed cabinet panel"); go.transform.SetParent(parent,false); go.transform.position=bounds.center;
        go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterials=template.sharedMaterials;
        go.GetComponent<BoxCollider>().center=Vector3.zero; go.GetComponent<BoxCollider>().size=bounds.size;
        return go;
    }
    public static void ValidateConfiguration(Scene scene)
    {
        var mover=All<ApartmentVRLocomotion>(scene).Single(); var owner=All<ApartmentFurnitureInteractor>(scene).Single();
        if(All<OVRCameraRig>(scene).Length!=1 || mover.ConfigurationError()!=null || mover.TeleportConfigurationError()!=null ||
            mover.debugMode || mover.turnMode!=ApartmentTurnMode.Smooth || owner.locomotion!=mover || owner.hintFont==null ||
            owner.doors.Length!=6 || All<ApartmentCabinetDoor>(scene).Length!=6 || owner.maxDistance!=2f)
            throw new InvalidOperationException("精细 VR 玩家/交互/导航配置不完整。");
        foreach(var door in owner.doors) if(door.ConfigurationError()!=null || door.inputOwner!=owner)
            throw new InvalidOperationException("柜门配置错误："+door.name);
        if(All<Transform>(scene).Any(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0))
            throw new InvalidOperationException("场景包含丢失脚本。");
        if(All<Camera>(scene).Count(c=>c.isActiveAndEnabled)!=1 || All<AudioListener>(scene).Count(c=>c.isActiveAndEnabled)!=1)
            throw new InvalidOperationException("场景必须只有一个有效摄像机和声音监听器。");
        if(mover.rig.GetComponentsInChildren<MonoBehaviour>(true).Any(b=>b!=null && b.isActiveAndEnabled &&
            b.GetType().FullName.StartsWith("Oculus.Interaction.Locomotion.",StringComparison.Ordinal)))
            throw new InvalidOperationException("旧 SDK 移动逻辑仍开启，不能同时移动玩家。");
        if(mover.rig.GetComponentsInChildren<Oculus.Interaction.TunnelingEffect>(true).Any(e=>e.isActiveAndEnabled))
            throw new InvalidOperationException("旧移动系统的视野遮罩仍然开启。");
    }

    [Serializable] class CheckReport
    {
        public string scene, failure="";
        public int passed;
        public bool questVerified;
        public List<string> checks=new List<string>();
        public List<string> routeFailures=new List<string>();
        public List<string> deferredRoutes=new List<string>();
    }
    [Serializable] class NavigationSample
    { public Vector3 requested, sampled; public bool found; public string status; }
    [Serializable] class SourceShape
    { public string path,shape;public int area;public Vector3 size; }
    [Serializable] class ClearanceCandidate
    {public string group;public Vector3 delta;public string[] introducedOverlaps;}
    [Serializable] class ClearanceReport {public ClearanceCandidate[] candidates;}
    [Serializable] class ClearanceAlternative
    {public float chairZ,sofaZ,diningX,diningZ,diningYaw;public string failure;public bool[] roomRoutes;}
    [Serializable] class AlternativeReport {public ClearanceAlternative[] candidates;}
    [MenuItem("Apartment Phase 2/M4 Detailed/Inspect Final Clearance Alternatives (Read Only) %#&t")]
    public static void InspectAlternatives()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath)throw new InvalidOperationException("只检查精细 VR 副本。");
            var mover=All<ApartmentVRLocomotion>(scene).Single();var env=mover.teleportSurface.transform;
            var surface=mover.teleportSurface;var sources=(List<NavMeshBuildSource>)typeof(NavMeshSurface)
                .GetMethod("CollectSources",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(surface,null);
            var sofa=env.Find("House/Furniture/LivingRoom/SofaChairSet8_Sofa");
            var chair=env.Find("House/Furniture/LivingRoom/SofaChairSet8_Chair1");
            var dining=env.Find("House/Furniture/LivingRoom/DiningSet3_Chair").GetComponentsInChildren<Renderer>()
                .Where(r=>r.bounds.center.x<4.3f).Select(r=>r.transform).Distinct().ToArray();
            Vector3 pivot=dining.Select(t=>t.GetComponent<Renderer>().bounds.center).Aggregate(Vector3.zero,(a,b)=>a+b)/dining.Length;
            var all=env.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            var candidates=new List<ClearanceAlternative>();
            foreach(float dz in new[]{.45f,.50f,.525f,.55f,.575f,.60f})
                candidates.Add(new ClearanceAlternative{sofaZ=dz,diningX=-.03f,diningZ=dz-.40f+.02f});
            foreach(var candidate in candidates)
            {
                var maps=new Dictionary<Transform,Matrix4x4>();
                maps.Add(sofa,Matrix4x4.Translate(Vector3.forward*candidate.sofaZ));
                maps.Add(chair,Matrix4x4.Translate(Vector3.forward*candidate.chairZ));
                foreach(var part in dining) maps.Add(part,Matrix4x4.TRS(pivot+new Vector3(candidate.diningX,0,candidate.diningZ),
                    Quaternion.AngleAxis(candidate.diningYaw,Vector3.up),Vector3.one)*Matrix4x4.Translate(-pivot));
                Func<Transform,Matrix4x4> map=t=>maps.Where(m=>t==m.Key||t.IsChildOf(m.Key)).Select(m=>m.Value).DefaultIfEmpty(Matrix4x4.identity).First();
                foreach(var part in all.Where(c=>maps.Keys.Any(t=>c.transform==t||c.transform.IsChildOf(t))))
                {
                    var a=map(part.transform);
                    foreach(var other in all)
                    {
                        if(other==part)continue;var b=map(other.transform);
                        bool old=Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,
                            other.transform.position,other.transform.rotation,out _,out float oldDepth);
                        bool now=Physics.ComputePenetration(part,a.MultiplyPoint3x4(part.transform.position),a.rotation*part.transform.rotation,other,
                            b.MultiplyPoint3x4(other.transform.position),b.rotation*other.transform.rotation,out _,out float depth);
                        if(now&&depth>(old?oldDepth:0)+.001f){candidate.failure="introduced overlap: "+PathOf(part.transform)+" -> "+PathOf(other.transform);break;}
                    }
                    if(candidate.failure!=null)break;
                }
                if(candidate.failure!=null)continue;
                var hypothetical=sources.Select(s=>{if(s.component!=null)s.transform=map(s.component.transform)*s.transform;return s;}).ToList();
                var data=NavMeshBuilder.BuildNavMeshData(surface.GetBuildSettings(),hypothetical,new Bounds(new Vector3(4.8f,1.4f,2.6f),new Vector3(12,7,13)),Vector3.zero,Quaternion.identity);
                var instance=NavMesh.AddNavMeshData(data,Vector3.right*1000f,Quaternion.identity);
                try
                {
                    var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=1};Vector3 offset=Vector3.right*1000f;
                    NavMesh.SamplePosition(mover.transform.position+offset,out var start,.25f,filter);
                    var points=new[]{new Vector3(4.30f,0,.75f),new Vector3(5.7f,.126f,3.3f),new Vector3(1.66f,.012f,2),
                        new Vector3(1.66f,.012f,3.9f),new Vector3(1.66f,.012f,6.08f),new Vector3(7.55f,0,2.9f)};
                    candidate.roomRoutes=points.Select(p=>{var path=new NavMeshPath();return NavMesh.SamplePosition(p+offset,out var target,.12f,filter)&&
                        NavMesh.CalculatePath(start.position,target.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete;}).ToArray();
                }
                finally{instance.Remove();Object.DestroyImmediate(data);}
            }
            File.WriteAllText("Logs/DetailedClearanceAlternatives.json",JsonUtility.ToJson(new AlternativeReport{candidates=candidates.ToArray()},true));
            Debug.Log("Final clearance alternatives checked on temporary offset maps; no scene/furniture positions saved or changed.");
        }
        catch(Exception e){Debug.LogError("Clearance alternatives stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Inspect Approved Furniture Clearance (Read Only) %#&f")]
    public static void InspectClearance()
    {
        try
        {
            RequireCleanEditor();var scene=SceneManager.GetActiveScene();
            if(scene.path!=TargetPath) throw new InvalidOperationException("请打开精细 VR 副本。");
            var env=All<ApartmentVRLocomotion>(scene).Single().teleportSurface.transform;
            var all=env.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            var candidates=new List<ClearanceCandidate>();
            Action<string,Collider[],Vector3> inspect=(name,parts,delta)=>
            {
                var overlaps=new List<string>();
                foreach(var part in parts) foreach(var other in all)
                {
                    if(parts.Contains(other)) continue;
                    bool was=Physics.ComputePenetration(part,part.transform.position,part.transform.rotation,other,
                        other.transform.position,other.transform.rotation,out _,out float oldDepth);
                    bool now=Physics.ComputePenetration(part,part.transform.position+delta,part.transform.rotation,other,
                        other.transform.position,other.transform.rotation,out _,out float depth);
                    if(now&&depth>(was?oldDepth:0f)+.001f) overlaps.Add(PathOf(part.transform)+" -> "+PathOf(other.transform)+" depth="+depth);
                }
                candidates.Add(new ClearanceCandidate{group=name,delta=delta,introducedOverlaps=overlaps.Distinct().ToArray()});
            };
            foreach(string name in new[]{"SofaChairSet8_Sofa","Plant"})
            {
                var root=env.Find("House/Furniture/LivingRoom/"+name);
                inspect(name,root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray(),
                    name=="Plant"?new Vector3(0,0,-.55f):new Vector3(.15f,0,0));
            }
            foreach(string name in new[]{"DiningSet3_Chair","DiningSet3_Chair (1)"})
            {
                var root=env.Find("House/Furniture/LivingRoom/"+name);
                var parts=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.bounds.center.x<4.3f).ToArray();
                if(parts.Length==0) throw new InvalidOperationException("没有找到左侧餐椅的独立碰撞部分。");
                foreach(float dx in new[]{.45f,.50f,.55f,.60f}) inspect(name+" left",parts,new Vector3(dx,0,0));
                foreach(float dz in new[]{-.50f,-.65f,-.80f}) inspect(name+" left alternate",parts,new Vector3(.55f,0,dz));
            }
            File.WriteAllText("Logs/DetailedFurnitureClearance.json",JsonUtility.ToJson(new ClearanceReport{candidates=candidates.ToArray()},true));
            Debug.Log("Approved furniture candidates inspected without moving or saving scene objects.");
        }
        catch(Exception e) {Debug.LogError("Furniture clearance inspection: "+e);}
    }
    [Serializable] class GeometryReport
    { public ObjectInfo[] solids, colliders; public NavigationSample[] map;public SourceShape[] sources;public float minRegionArea; }
    [MenuItem("Apartment Phase 2/M4 Detailed/Diagnose Detailed Geometry (Read Only) %#&o")]
    public static void Diagnose()
    {
        try
        {
            RequireCleanEditor(); var scene=SceneManager.GetActiveScene();
            var mover=All<ApartmentVRLocomotion>(scene).Single();
            var filter=new NavMeshQueryFilter { agentTypeID=mover.teleportSurface.agentTypeID,areaMask=1 };
            NavMesh.SamplePosition(mover.transform.position,out var start,.25f,filter);
            var samples=new List<NavigationSample>();
            for(float z=-2f;z<=7.5f;z+=.25f) for(float x=.25f;x<=9.5f;x+=.25f)
            {
                Vector3 p=new Vector3(x,.15f,z); bool found=NavMesh.SamplePosition(p,out var hit,.25f,filter);
                var route=new NavMeshPath(); bool path=found&&NavMesh.CalculatePath(start.position,hit.position,filter,route);
                samples.Add(new NavigationSample {requested=p,sampled=hit.position,found=found,status=path?route.status.ToString():"None"});
            }
            var report=new GeometryReport { solids=mover.teleportSurface.GetComponentsInChildren<Renderer>()
                .Where(r=>r.gameObject.activeInHierarchy).Select(r=>Info(r.transform)).ToArray(),
                colliders=mover.teleportSurface.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger)
                .Select(c=>new ObjectInfo{path=PathOf(c.transform),active=c.gameObject.activeInHierarchy,
                    position=c.transform.position,scale=c.transform.lossyScale,center=c.bounds.center,size=c.bounds.size,
                    components=c.GetComponents<Component>().Where(v=>v!=null).Select(v=>v.GetType().FullName).ToArray(),
                    mesh=c is MeshCollider mc && mc.sharedMesh!=null?mc.sharedMesh.name:""}).ToArray(),map=samples.ToArray(),
                    minRegionArea=mover.teleportSurface.minRegionArea };
            var collector=typeof(NavMeshSurface).GetMethod("CollectSources",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            if(collector!=null)
                report.sources=((List<NavMeshBuildSource>)collector.Invoke(mover.teleportSurface,null)).Select(s=>new SourceShape {
                    path=s.component!=null?PathOf(s.component.transform):s.sourceObject!=null?s.sourceObject.name:"modifier volume",
                    shape=s.shape.ToString(),area=s.area,size=s.size }).ToArray();
            File.WriteAllText("Logs/DetailedGeometry.json",JsonUtility.ToJson(report,true));
            Debug.Log("Detailed geometry inspected (no scene changes): Logs/DetailedGeometry.json");
        }
        catch(Exception e) {Debug.LogError("Detailed geometry: "+e);}
    }
    [MenuItem("Apartment Phase 2/M4 Detailed/Check Detailed VR (Read Only) %#&l")]
    public static void Check()
    {
        if (All<ApartmentTutorialController>(SceneManager.GetActiveScene()).Length > 0)
        { ApartmentExperienceChecks.Run(); return; }
        var report=new CheckReport { scene=TargetPath }; Scene scratch=default,original=default;
        try
        {
            RequireCleanEditor(); original=SceneManager.GetActiveScene();
            if(original.path!=TargetPath || SceneManager.sceneCount!=1) throw new InvalidOperationException("请只打开已保存的精细 VR 副本。");
            ValidateConfiguration(original); Pass(report,true,"one valid player, Chinese font, six doors, single camera/audio, legacy locomotion disabled");
            var source=All<ApartmentFurnitureInteractor>(original).Single(); var mover=source.locomotion;
            if(ApartmentExperienceUpgrade.RequiresPreloadedGlyphs(source.hintFont))
                foreach(char c in ApartmentFurnitureSetup.RequiredCharacters()) Pass(report,source.hintFont.HasCharacter(c),"font glyph: "+c);
            Pass(report,ClearSpawn(mover,mover.transform.position),"original detailed spawn platform remains clear");
            var filter=new NavMeshQueryFilter { agentTypeID=mover.teleportSurface.agentTypeID,areaMask=1 };
            Pass(report,NavMesh.SamplePosition(mover.transform.position,out var start,.25f,filter),"spawn is on saved map");
            var points=new[]{ new Vector3(4.30f,0,.75f),new Vector3(5.7f,.126f,3.3f),new Vector3(1.66f,.012f,2.0f),
                new Vector3(1.66f,.012f,3.9f),new Vector3(1.66f,.012f,6.08f),new Vector3(7.55f,0,2.9f) };
            foreach(var p in points)
            {
                bool found=NavMesh.SamplePosition(p,out var target,.12f,filter); var route=new NavMeshPath();
                bool complete=found && NavMesh.CalculatePath(start.position,target.position,filter,route) && route.status==NavMeshPathStatus.PathComplete;
                string result="static map route to room at "+p+"; sample="+found+" "+target.position+"; status="+route.status+
                    " (live closed doors still require opening)";
                if(complete) Pass(report,true,result); else report.routeFailures.Add(result);
            }
            report.deferredRoutes.Add("Balcony access is intentionally NOT accepted: user deferred opening the source scene's fixed glass panels. Glass colliders remain enabled; no teleport through them.");
            foreach(var volume in mover.teleportSurface.GetComponentsInChildren<NavMeshModifierVolume>())
                Pass(report,!NavMesh.SamplePosition(new Vector3(volume.center.x,.012f,volume.center.z),out _, .02f,filter),"cabinet footprint not walkable: "+volume.name);
            Pass(report,!source.RuntimeReady,"Editor checks do not claim Quest tracking");
            var input=new ApartmentInteractionTriggerLatch();
            Pass(report,!input.Sample(1,true),"held-at-start trigger disarmed"); input.Sample(0,true);
            Pass(report,input.Sample(.75f,true) && !input.Sample(1,true),"trigger one-shot press");
            input.Sample(0,false); Pass(report,!input.Sample(1,true),"teleport/focus recovery requires release");
            var button=new ApartmentTeleportButtonLatch(); button.Sample(true,false);
            Pass(report,button.Sample(true,true)==ApartmentTeleportButtonAction.Begin,"right-A aim begins");
            Pass(report,button.Sample(true,false)==ApartmentTeleportButtonAction.Release,"right-A release requests teleport");
            var smooth=new ApartmentSmoothTurnInput(); smooth.Sample(Vector2.zero,.2f);
            Pass(report,smooth.Sample(Vector2.right,.2f)==1f && smooth.Sample(Vector2.right,.2f)==1f,"continuous yaw stays continuous");
            scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scratch);
            // Never clone ProBuilder components: their destruction may dispose shared, scene-local meshes.
            // Physics-only proxies retain the exact shapes, but have no authoring/scene-lifecycle scripts.
            var doors=CreateDoorPhysicsFixture(mover.teleportSurface.transform,scratch);
            var ownerObject=new GameObject("Read-only test proxy"); var owner=ownerObject.AddComponent<ApartmentFurnitureInteractor>();
            owner.locomotion=mover; owner.hintFont=source.hintFont; owner.doors=doors;
            foreach(var door in doors) { door.inputOwner=owner; door.ResetClosed(); }
            Physics.SyncTransforms();
            foreach(var door in doors)
            {
                Vector3 closed=door.doorCollider.transform.position; Quaternion rotation=door.doorCollider.transform.rotation;
                Pass(report,door.TryInteract(out _),PathOf(door.transform)+" accepts trigger");
                for(int i=0;i<30&&door.IsMoving;i++) door.AdvanceMotion(.05f,Vector3.zero,false);
                Pass(report,door.State==ApartmentCabinetState.Open,door.name+" opens to 90 degrees: "+door.BlockedReason);
                Pass(report,door.InteractionPrompt==ApartmentCabinetDoor.ClosePrompt,"open hint requests close");
                Pass(report,door.TryInteract(out _),"close request accepted");
                for(int i=0;i<30&&door.IsMoving;i++) door.AdvanceMotion(.05f,Vector3.zero,false);
                Pass(report,door.State==ApartmentCabinetState.Closed && Vector3.Distance(closed,door.doorCollider.transform.position)<.00001f &&
                    Quaternion.Angle(rotation,door.doorCollider.transform.rotation)<.001f,"door returns to original closed pose");
                bool west=door.transform.parent.name.StartsWith("BaseCab_W",StringComparison.Ordinal);
                Vector3 front=west?Vector3.right:Vector3.back,side=west?Vector3.forward:Vector3.right;
                bool visible=false;
                foreach(float sideways in new[]{0f,-.4f,.4f,-.8f,.8f})
                {
                    Vector3 center=door.doorCollider.bounds.center,origin=center+front*.7f+side*sideways;
                    var ray=new Ray(origin,(center-origin).normalized);
                    if(owner.TryFirstHit(ray,out var hit) && ApartmentFurnitureInteractor.FindInteractable(hit.collider)==door)
                    { visible=true;break; }
                }
                Pass(report,visible,"nearest 2m ray selects pointed door from a clear front/diagonal viewpoint: "+PathOf(door.transform));
            }
            var sample=doors[0]; sample.TryInteract(out _); sample.AdvanceMotion(.05f,Vector3.zero,false);
            float before=sample.CurrentAngle; sample.PauseMotion(); sample.AdvanceMotion(.05f,Vector3.zero,false);
            Pass(report,sample.State==ApartmentCabinetState.Paused && sample.CurrentAngle==before,"focus loss pauses cabinet animation");
            sample.TryInteract(out _); for(int i=0;i<30&&sample.IsMoving;i++) sample.AdvanceMotion(.05f,Vector3.zero,false);
            Vector3 open=sample.doorCollider.bounds.center; sample.ResetClosed();
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.name="Temporary player obstruction";
            blocker.transform.position=open; blocker.transform.localScale=Vector3.one*.25f;
            Physics.SyncTransforms(); sample.TryInteract(out _);
            for(int i=0;i<30&&sample.IsMoving;i++) sample.AdvanceMotion(.05f,Vector3.zero,false);
            Pass(report,sample.State==ApartmentCabinetState.Blocked,"live obstacle stops sweep");
            blocker.SetActive(false); Physics.SyncTransforms(); sample.TryInteract(out _);
            for(int i=0;i<30&&sample.IsMoving;i++) sample.AdvanceMotion(.05f,Vector3.zero,false);
            Pass(report,sample.State==ApartmentCabinetState.Open,"new press resumes after obstacle clears");
        }
        catch(Exception e) { report.failure=e.ToString(); }
        finally
        {
            if(original.IsValid()&&original.isLoaded) SceneManager.SetActiveScene(original);
            if(scratch.IsValid()&&scratch.isLoaded) EditorSceneManager.CloseScene(scratch,true);
            Physics.SyncTransforms(); Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/DetailedMigrationChecks.json",JsonUtility.ToJson(report,true));
            if(report.failure=="" && report.routeFailures.Count==0) Debug.Log("Detailed VR checks PASS in agreed indoor scope: "+report.passed+"; balcony opening deferred; saved scene unmodified; Quest/Air Link acceptance required.");
            else if(report.failure=="") Debug.LogWarning("Detailed VR checks incomplete: "+report.passed+" checks pass; "+report.routeFailures.Count+
                " static room routes remain disconnected. See Logs/DetailedMigrationChecks.json; do not claim full teleport acceptance.");
            else Debug.LogError("Detailed VR checks FAILED after "+report.passed+": "+report.failure);
        }
    }
    static ApartmentCabinetDoor[] CreateDoorPhysicsFixture(Transform environment,Scene scene)
    {
        Vector3 offset=Vector3.right*1000f;
        var root=new GameObject("Detailed physics fixture"); SceneManager.MoveGameObjectToScene(root,scene);
        foreach(var source in environment.GetComponentsInChildren<Collider>())
        {
            if(!source.enabled||source.isTrigger||source.GetComponentInParent<ApartmentCabinetDoor>()!=null) continue;
            var go=new GameObject(source.name); go.transform.SetParent(root.transform,false);
            go.transform.SetPositionAndRotation(source.transform.position+offset,source.transform.rotation);
            go.transform.localScale=source.transform.lossyScale; go.layer=source.gameObject.layer;
            if(source is BoxCollider box)
            { var c=go.AddComponent<BoxCollider>(); c.center=box.center;c.size=box.size; }
            else if(source is MeshCollider mesh)
            { var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh.sharedMesh;c.convex=mesh.convex;c.cookingOptions=mesh.cookingOptions; }
            else if(source is CapsuleCollider capsule)
            { var c=go.AddComponent<CapsuleCollider>();c.center=capsule.center;c.radius=capsule.radius;c.height=capsule.height;c.direction=capsule.direction; }
            else if(source is SphereCollider sphere)
            { var c=go.AddComponent<SphereCollider>();c.center=sphere.center;c.radius=sphere.radius; }
            else throw new InvalidOperationException("未支持的检查碰撞体："+source.GetType().Name);
        }
        var doors=new List<ApartmentCabinetDoor>();
        foreach(var group in environment.GetComponentsInChildren<ApartmentCabinetDoor>().GroupBy(d=>d.transform.parent))
        {
            var parent=new GameObject(group.Key.name); parent.transform.SetParent(root.transform,false);
            parent.transform.SetPositionAndRotation(group.Key.position+offset,group.Key.rotation);
            parent.transform.localScale=group.Key.lossyScale;
            foreach(var source in group)
            {
                var clone=Object.Instantiate(source.gameObject,parent.transform,true);
                clone.name=source.name; clone.transform.position=source.transform.position+offset;
                doors.Add(clone.GetComponent<ApartmentCabinetDoor>());
            }
        }
        Physics.SyncTransforms(); return doors.ToArray();
    }
    static void Pass(CheckReport report,bool condition,string label)
    { if(!condition) throw new InvalidOperationException(label); report.passed++; report.checks.Add(label); }
}
#endif
