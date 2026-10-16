#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ApartmentExperienceChecks
{
    [Serializable] sealed class Report
    { public int passed; public List<string> failures=new List<string>(); public List<string> motion=new List<string>(); public bool questVerified; }
    static Report report;
    static void Check(bool condition,string name) { if(condition)report.passed++;else report.failures.Add(name); }
    static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    public static void Run()
    {
        report=new Report(); Scene original=SceneManager.GetActiveScene(),scratch=default;string originalText=null;
        try
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || original.path!=ApartmentExperienceUpgrade.ScenePath || original.isDirty || SceneManager.sceneCount!=1)
                throw new InvalidOperationException("请先退出 Play，保存精细 VR 副本后检查。");
            originalText=File.ReadAllText(original.path);ApartmentExperienceUpgrade.Validate(original);Check(true,"configuration");
            CheckTutorial();
            var owner=All<ApartmentFurnitureInteractor>(original).Single();var mover=owner.locomotion;
            foreach(char c in ApartmentExperienceUpgrade.RequiredCharacters())Check(owner.hintFont.HasCharacter(c),"font: "+c);
            scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            CheckLanding(mover);
            var offset=Vector3.right*1000f;
            foreach(var source in All<Collider>(original))
            {
                if(!source.enabled||!source.gameObject.activeInHierarchy||source.isTrigger||source.transform.IsChildOf(mover.transform)||
                    source.GetComponentInParent<ApartmentCabinetDoor>()!=null)continue;
                var slider=source.GetComponentInParent<ApartmentSlidingDoor>();
                if(slider!=null && source.transform.IsChildOf(slider.movingLeaf))continue;
                CopyCollider(source,null,offset);
            }
            var proxyOwner=new GameObject("Physics fixture input owner").AddComponent<ApartmentFurnitureInteractor>();proxyOwner.locomotion=mover;
            var doors=owner.doors.Select(d=>CopyDoor(d,proxyOwner,offset)).ToArray();
            var sliding=CopySlider(owner.slidingDoors[0],proxyOwner,offset);
            Physics.SyncTransforms();
            foreach(var door in doors)
            {
                door.ResetClosed();Check(door.TryInteract(out _),door.name+" accepts open");
                for(int i=0;i<40&&door.IsMoving;i++)door.AdvanceMotion(.05f,Vector3.zero,false);
                report.motion.Add(door.name+" open="+door.CurrentAngle+" state="+door.State+" "+door.BlockedReason);
                Check(door.State==ApartmentCabinetState.Open,door.name+" reaches open: "+door.BlockedReason);
                if(door.State==ApartmentCabinetState.Open)
                {
                    Check(door.TryInteract(out _),door.name+" accepts close");
                    for(int i=0;i<40&&door.IsMoving;i++)door.AdvanceMotion(.05f,Vector3.zero,false);
                    Check(door.State==ApartmentCabinetState.Closed,door.name+" returns closed: "+door.BlockedReason);
                }
                door.ResetClosed();
            }
            Check(sliding.TryInteract(out _),"slider accepts open");
            for(int i=0;i<70&&sliding.IsMoving;i++)sliding.AdvanceMotion(.05f,Vector3.zero,false);
            report.motion.Add("Balcony open="+sliding.CurrentTravel+" state="+sliding.State+" "+sliding.BlockedReason);
            Check(sliding.State==ApartmentCabinetState.Open,"slider full opening: "+sliding.BlockedReason);
            if(sliding.State==ApartmentCabinetState.Open)
            {
                Check(sliding.TryInteract(out _),"slider accepts close");
                for(int i=0;i<70&&sliding.IsMoving;i++)sliding.AdvanceMotion(.05f,Vector3.zero,false);
                Check(sliding.State==ApartmentCabinetState.Closed,"slider closes: "+sliding.BlockedReason);
            }
            sliding.ResetClosed();
            sliding.TryInteract(out _);sliding.AdvanceMotion(.05f,Vector3.zero,false);float sliderPaused=sliding.CurrentTravel;
            sliding.PauseMotion();sliding.AdvanceMotion(.05f,Vector3.zero,false);
            Check(sliding.State==ApartmentCabinetState.Paused&&sliding.CurrentTravel==sliderPaused,"slider focus loss remains paused");sliding.ResetClosed();
            var sliderBlock=new GameObject("Slider body obstruction").AddComponent<BoxCollider>();
            sliderBlock.size=sliding.movingColliders[0].bounds.size+Vector3.one*.04f;sliderBlock.transform.position=sliding.movingColliders[0].bounds.center;
            Physics.SyncTransforms();sliding.TryInteract(out _);sliding.AdvanceMotion(.05f,Vector3.zero,false);
            Check(sliding.State==ApartmentCabinetState.Blocked&&sliding.CurrentTravel==0,"body blocks sliding door");
            sliderBlock.enabled=false;Physics.SyncTransforms();sliding.TryInteract(out _);sliding.AdvanceMotion(.05f,Vector3.zero,false);
            Check(sliding.CurrentTravel>0,"fresh trigger retries slider after obstruction removed");sliding.ResetClosed();
            var sample=doors.First(d=>d is ApartmentRoomDoor);
            sample.TryInteract(out _);sample.AdvanceMotion(.05f,Vector3.zero,false);float paused=sample.CurrentAngle;sample.PauseMotion();
            sample.AdvanceMotion(.05f,Vector3.zero,false);Check(sample.CurrentAngle==paused && sample.State==ApartmentCabinetState.Paused,"room door remains paused");
            sample.ResetClosed();
            var block=new GameObject("Temporary player body").AddComponent<BoxCollider>();block.size=sample.doorCollider.bounds.size+Vector3.one*.04f;block.transform.position=sample.doorCollider.bounds.center;
            Physics.SyncTransforms();sample.TryInteract(out _);sample.AdvanceMotion(.05f,Vector3.zero,false);
            Check(sample.State==ApartmentCabinetState.Blocked&&Mathf.Abs(sample.CurrentAngle)<.001f,"body blocks room door");
            block.enabled=false;Physics.SyncTransforms();sample.TryInteract(out _);sample.AdvanceMotion(.05f,Vector3.zero,false);
            Check(Mathf.Abs(sample.CurrentAngle)>.001f,"fresh press retries after obstacle removed");sample.ResetClosed();
            // Use the real right-hand ray selection code against the closed physics fixture.
            proxyOwner.maxDistance=2;
            foreach(var door in doors)
            {
                Bounds b=door.doorCollider.bounds;bool seen=false;
                foreach(var direction in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back,
                    new Vector3(1,0,-1).normalized,new Vector3(-1,0,-1).normalized,new Vector3(1,0,1).normalized,new Vector3(-1,0,1).normalized})
                {
                    var origin=b.center+direction*.8f;
                    if(proxyOwner.TryFirstHit(new Ray(origin,-direction),out RaycastHit hit)&&ReferenceEquals(ApartmentFurnitureInteractor.FindInteractable(hit.collider),door)){seen=true;break;}
                }
                Check(seen,door.name+" has unobstructed trigger approach");
                if(door is ApartmentRoomDoor)
                {
                    Vector3 normal=door.doorCollider.transform.TransformDirection(door.doorCollider.size.x<door.doorCollider.size.z?Vector3.right:Vector3.forward);
                    foreach(float side in new[]{-1f,1f})
                    {
                        bool sideHit=proxyOwner.TryFirstHit(new Ray(b.center+normal*side*.5f,-normal*side),out RaycastHit hit);
                        Check(sideHit&&ReferenceEquals(ApartmentFurnitureInteractor.FindInteractable(hit.collider),door),door.name+" trigger accessible side "+side);
                    }
                }
            }
            Check(File.ReadAllText(original.path)==originalText,"scene file unchanged by checks");
        }
        catch(Exception e){report.failures.Add(e.ToString());}
        finally
        {
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            if(scratch.IsValid()&&scratch.isLoaded)EditorSceneManager.CloseScene(scratch,true);
            Physics.SyncTransforms();Directory.CreateDirectory("Logs");File.WriteAllText("Logs/M5UpgradeChecks.json",JsonUtility.ToJson(report,true));
            if(report.failures.Count==0)Debug.Log("M5 checks PASS: "+report.passed+" assertions. Quest hardware not verified.");
            else Debug.LogError("M5 checks: "+report.passed+" passed; failures: "+string.Join(" | ",report.failures));
        }
    }
    static void CheckTutorial()
    {
        var p=new ApartmentTutorialProgress();var trigger=new ApartmentInteractionTriggerLatch();
        Check(p.Explaining&&!p.AllowMove&&!p.AllowTurn&&!p.AllowTeleport,"modal gates locomotion");
        p.Walk(1);p.Turn(90);p.Teleport(2);Check(p.Step==0,"unacknowledged input cannot complete tutorial");
        Check(!trigger.Sample(1,true),"held trigger at startup is disarmed");trigger.Sample(0,true);Check(trigger.Sample(.75f,true),"new trigger press");
        p.Confirm();Check(!p.AllowMove&&p.WaitingForNeutral,"OK waits for release");
        Check(!trigger.Sample(1,true),"OK press cannot repeat as world interaction");p.ObserveNeutral(false);Check(!p.AllowMove,"held controls cannot start practice");
        p.ObserveNeutral(true);Check(p.AllowMove&&!p.AllowTurn&&!p.AllowTeleport,"walk practice only");
        p.Walk(0);p.Walk(float.NaN);Check(p.Step==0,"blocked/invalid walking not complete");p.Walk(.24f);Check(p.Step==0,"walk below threshold");p.Walk(.01f);Check(p.Step==1&&p.Explaining,"actual 25cm advances");
        p.Confirm();p.ObserveNeutral(true);p.Turn(14);Check(p.Step==1,"turn below threshold");p.Turn(1);Check(p.Step==2,"actual 15 degrees advances");
        p.Confirm();p.ObserveNeutral(true);p.Teleport(.49f);Check(p.Step==2,"tiny teleport not completion");p.Teleport(.5f);Check(p.Step==3&&!p.AllowInteraction,"completion still requires neutral");
        p.ObserveNeutral(true);Check(p.AllowMove&&p.AllowTurn&&p.AllowTeleport&&p.AllowInteraction,"exploration unlocked");
        foreach(ApartmentInteractionKind kind in Enum.GetValues(typeof(ApartmentInteractionKind)))
        {
            Check(p.RequestKind(kind),kind+" first hint appears");Check(!p.AllowInteraction,kind+" modal takes input");p.Confirm();
            Check(p.HasSeen(kind)&&!p.AllowInteraction,kind+" seen and waiting for release");p.ObserveNeutral(true);Check(!p.RequestKind(kind),kind+" never repeats this session");
        }
        p.Suspend();Check(!p.AllowInteraction,"focus loss gates input");p.ObserveNeutral(true);Check(p.Step==3&&p.HasSeen(ApartmentInteractionKind.Cabinet),"focus recovery retains progress");
        p=new ApartmentTutorialProgress();Check(p.Step==0&&!p.HasSeen(ApartmentInteractionKind.RoomDoor),"new session resets tutorial");
    }
    static void CheckLanding(ApartmentVRLocomotion mover)
    {
        var old=mover.teleportSurface;
        try
        {
            mover.teleportSurface=null;Check(mover.TeleportConfigurationError()==null,"floor mode needs no navigation data");
            Vector3 feet=new Vector3(mover.rig.centerEyeAnchor.position.x,mover.transform.position.y,mover.rig.centerEyeAnchor.position.z);
            var floor=new GameObject("Temporary disconnected test floor").AddComponent<BoxCollider>();floor.gameObject.layer=LayerMask.NameToLayer("TeleportFloor");
            floor.size=new Vector3(1.2f,.1f,1.2f);Vector3 target=feet+Vector3.up*3;
            floor.transform.position=target-Vector3.up*.05f;Physics.SyncTransforms();
            Check(mover.ValidateTeleportDestination(target,out _,out _),"physical floor disconnected from navmesh accepted");
            floor.gameObject.layer=0;Check(!mover.ValidateTeleportDestination(target,out _,out _),"table layer rejected");floor.gameObject.layer=LayerMask.NameToLayer("TeleportFloor");
            var blocker=new GameObject("Temporary landing obstruction").AddComponent<BoxCollider>();blocker.size=Vector3.one*.7f;blocker.transform.position=target+Vector3.up*.7f;Physics.SyncTransforms();
            Check(!mover.ValidateTeleportDestination(target,out _,out _),"occupied landing rejected without rebake");
            blocker.enabled=false;Physics.SyncTransforms();Check(mover.ValidateTeleportDestination(target,out _,out _),"removed obstruction restores landing");
            floor.enabled=false;Physics.SyncTransforms();Check(!mover.ValidateTeleportDestination(target,out _,out _),"missing floor rejected");
            Check(!mover.ValidateTeleportDestination(feet+Vector3.right*20,out _,out _),"distance cap preserved");
            Check(!mover.TryTeleportTo(feet,out _),"no editor teleport without tracked hardware");
            Object.DestroyImmediate(blocker.gameObject);Object.DestroyImmediate(floor.gameObject);
        }
        finally{mover.teleportSurface=old;}
        // First-hit arc collision must keep a closed door in front of the floor.
        Vector3 start=new Vector3(2000,10,2000);
        var wall=new GameObject("Closed door ray fixture").AddComponent<BoxCollider>();wall.transform.position=start+Vector3.forward;wall.size=new Vector3(1,1,.05f);
        var ground=new GameObject("Floor behind door").AddComponent<BoxCollider>();ground.gameObject.layer=LayerMask.NameToLayer("TeleportFloor");ground.transform.position=start+Vector3.forward*2;
        Physics.SyncTransforms();var method=typeof(ApartmentVRLocomotion).GetMethod("FirstSceneHit",BindingFlags.NonPublic|BindingFlags.Instance);
        object[] args={new Ray(start,Vector3.forward),3f,default(RaycastHit),false};method.Invoke(mover,args);
        Check(((RaycastHit)args[2]).collider==wall,"first entity blocks arc behind closed door");
        wall.isTrigger=true;wall.gameObject.AddComponent<ApartmentDoorAimSurface>().surface=wall;Physics.SyncTransforms();
        args=new object[]{new Ray(start,Vector3.forward),3f,default(RaycastHit),false};method.Invoke(mover,args);
        Check(((RaycastHit)args[2]).collider==wall,"full visible door aiming outline blocks arc even outside fitted solid core");
        Object.DestroyImmediate(wall.gameObject);Object.DestroyImmediate(ground.gameObject);
        var rect=new GameObject("OK hit test",typeof(RectTransform)).GetComponent<RectTransform>();rect.position=start+Vector3.forward;rect.sizeDelta=new Vector2(.3f,.1f);
        Check(ApartmentTutorialController.HitRectangle(rect,new Ray(start,Vector3.forward),2,out _),"OK accepts right pointer on button");
        Check(!ApartmentTutorialController.HitRectangle(rect,new Ray(start+Vector3.up,Vector3.forward),2,out _),"OK ignores misses");
        Check(!ApartmentTutorialController.HitRectangle(rect,new Ray(start,Vector3.forward),.5f,out _),"OK enforces pointer reach");Object.DestroyImmediate(rect.gameObject);
    }
    static Collider CopyCollider(Collider source,Transform parent,Vector3 offset)
    {
        var go=new GameObject(source.name);go.layer=source.gameObject.layer;go.transform.SetParent(parent,false);
        go.transform.SetPositionAndRotation(source.transform.position+offset,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;
        Collider result=null;
        if(source is BoxCollider box){var c=go.AddComponent<BoxCollider>();c.center=box.center;c.size=box.size;result=c;}
        else if(source is MeshCollider mesh){var c=go.AddComponent<MeshCollider>();c.sharedMesh=mesh.sharedMesh;c.convex=mesh.convex;result=c;}
        else if(source is SphereCollider sphere){var c=go.AddComponent<SphereCollider>();c.center=sphere.center;c.radius=sphere.radius;result=c;}
        else if(source is CapsuleCollider capsule){var c=go.AddComponent<CapsuleCollider>();c.center=capsule.center;c.radius=capsule.radius;c.height=capsule.height;c.direction=capsule.direction;result=c;}
        return result;
    }
    static ApartmentCabinetDoor CopyDoor(ApartmentCabinetDoor source,ApartmentFurnitureInteractor owner,Vector3 offset)
    {
        var root=new GameObject(source.transform.parent.name+"/"+source.name);root.transform.SetPositionAndRotation(source.transform.position+offset,source.transform.rotation);
        var body=root.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        var box=(BoxCollider)CopyCollider(source.doorCollider,root.transform,offset);
        box.gameObject.AddComponent<MeshFilter>().sharedMesh=source.doorRenderer.GetComponent<MeshFilter>().sharedMesh;
        var renderer=box.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.doorRenderer.sharedMaterials;
        var sourceAim=source.doorCollider.GetComponent<ApartmentDoorAimSurface>();
        if(sourceAim!=null)
        {
            var aim=box.gameObject.AddComponent<ApartmentDoorAimSurface>();var surface=box.gameObject.AddComponent<BoxCollider>();
            surface.center=sourceAim.surface.center;surface.size=sourceAim.surface.size;surface.isTrigger=true;aim.surface=surface;
        }
        var door=source is ApartmentRoomDoor?(ApartmentCabinetDoor)root.AddComponent<ApartmentRoomDoor>():root.AddComponent<ApartmentCabinetDoor>();
        door.doorCollider=box;door.doorRenderer=renderer;door.hingeBody=body;door.inputOwner=owner;door.openAngle=source.openAngle;
        door.closedLocalRotation=root.transform.rotation;door.degreesPerSecond=source.degreesPerSecond;return door;
    }
    static ApartmentSlidingDoor CopySlider(ApartmentSlidingDoor source,ApartmentFurnitureInteractor owner,Vector3 offset)
    {
        var root=new GameObject("Balcony fixture");root.transform.SetPositionAndRotation(source.transform.position+offset,source.transform.rotation);
        var moving=new GameObject("Moving left");moving.transform.SetParent(root.transform,false);moving.transform.localPosition=source.closedLocalPosition;
        var boxes=source.movingColliders.Select(c=>(BoxCollider)CopyCollider(c,moving.transform,offset)).ToArray();
        var body=moving.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
        var renderer=boxes[0].gameObject.AddComponent<MeshRenderer>();boxes[0].gameObject.AddComponent<MeshFilter>().sharedMesh=source.doorRenderer.GetComponent<MeshFilter>().sharedMesh;
        renderer.sharedMaterials=source.doorRenderer.sharedMaterials;
        var door=root.AddComponent<ApartmentSlidingDoor>();door.movingLeaf=moving.transform;door.movingColliders=boxes;door.body=body;door.inputOwner=owner;door.doorRenderer=renderer;
        door.closedLocalPosition=source.closedLocalPosition;door.localTravel=source.localTravel;door.metresPerSecond=source.metresPerSecond;return door;
    }
}
#endif
