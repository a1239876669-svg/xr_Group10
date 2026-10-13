#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

// Explicit, reversible installation into the detailed copy only. No automatic import callbacks.
public static class ApartmentExperienceUpgrade
{
    public const string ScenePath = "Assets/Scenes/Apartment_Detailed_VR.unity";
    const string FontPath = "Assets/Apartment/Fonts/VRGuide-SC.asset";
    static readonly string[] RoomNames = { "Entrance", "Master", "Bed2", "Bath" };
    static readonly float[] RoomAngles = { 90, 90, 90, -90 };
    static readonly string[] FrameSuffixes = { "", "_fB", "_fT", "_fL", "_fR" };
    static readonly string[] ProtectedPaths = { "Assets/Scenes/Apartment.unity", "Assets/Scenes/ApartmentPhase2_VR.unity",
        "Assets/Scenes/ApartmentPhase2.unity", "Assets/Scenes/ApartmentPhase2_Furnished.unity" };
    static Scene RequireScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("请先退出 Play 并等待 Unity 编译完成。");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1 || scene.isDirty)
            throw new InvalidOperationException("请打开并保存精细 VR 副本。不会丢弃未保存内容。");
        return scene;
    }
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static Transform Find(Scene scene, string path)
    {
        string[] p=path.Split('/'); var root=scene.GetRootGameObjects().Single(g=>g.name==p[0]).transform;
        return p.Length==1?root:root.Find(string.Join("/",p.Skip(1)));
    }
    static string Hash(string path)
    { using(var h=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))); }

    [MenuItem("Apartment Phase 2/M5 Upgrade/Install Approved Upgrade %#&9")]
    public static void Install()
    {
        try
        {
            var scene=RequireScene(); var protectedHashes=ProtectedPaths.Select(Hash).ToArray();
            if(All<ApartmentTutorialController>(scene).Length>0) { Validate(scene); Debug.Log("M5 already installed; no duplicate objects created."); return; }
            var mover=All<ApartmentVRLocomotion>(scene).Single(); var owner=All<ApartmentFurnitureInteractor>(scene).Single();
            var doorRoot=Find(scene,"DetailedEnvironment/House/Doors");
            foreach(string name in RoomNames)
                if(doorRoot.Find("Door_"+name+"_Hinge/Door_"+name)?.GetComponent<MeshRenderer>()==null)
                    throw new InvalidOperationException("房门结构发生变化："+name);
            foreach(string side in new[]{"L","R"}) foreach(string suffix in FrameSuffixes)
                if(doorRoot.Find("Door_Bal_"+side+suffix)?.GetComponent<MeshRenderer>()==null)
                    throw new InvalidOperationException("阳台门结构发生变化。");
            string backup="Logs/M5Backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"); Directory.CreateDirectory(backup);
            File.Copy(ScenePath,backup+"/Apartment_Detailed_VR.unity");
            var font=EnsureFont();
            Undo.IncrementCurrentGroup(); int undo=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Upgrade detailed apartment experience");
            Undo.RecordObjects(new Object[]{mover,owner},"Connect floor landing and tutorial");
            mover.teleportValidationMode=ApartmentTeleportValidationMode.FloorLanding;
            for(int i=0;i<RoomNames.Length;i++) InstallRoom(doorRoot,RoomNames[i],RoomAngles[i],owner);
            var sliding=InstallSliding(doorRoot,owner);
            owner.doors=All<ApartmentCabinetDoor>(scene); owner.slidingDoors=new[]{sliding}; owner.hintFont=font;
            var tutorialObject=Child("Tutorial",mover.transform);
            var tutorial=Undo.AddComponent<ApartmentTutorialController>(tutorialObject);
            tutorial.locomotion=mover; tutorial.interactor=owner; tutorial.font=font;
            tutorial.panelShader=Shader.Find("Apartment/TutorialOverlay");tutorial.textOverlayShader=Shader.Find("TextMeshPro/Distance Field Overlay");
            mover.tutorial=tutorial; owner.tutorial=tutorial;
            // Old SDK reticles on doors must not route a second interaction input.
            foreach(var behaviour in doorRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if(behaviour!=null && (behaviour.GetType().Namespace??"").StartsWith("Oculus.Interaction",StringComparison.Ordinal))
                { Undo.RecordObject(behaviour,"Disable legacy door interaction"); behaviour.enabled=false; }
            foreach(var c in new Component[]{mover,owner,tutorial}) EditorUtility.SetDirty(c);
            Physics.SyncTransforms(); Validate(scene);
            for(int i=0;i<ProtectedPaths.Length;i++) if(Hash(ProtectedPaths[i])!=protectedHashes[i]) throw new InvalidOperationException("Protected scene changed: "+ProtectedPaths[i]);
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(undo); AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("M5 installed and saved: floor landing, 4 room doors, 6 cabinet doors, balcony slider, session tutorial. Backup: "+backup);
        }
        catch(Exception e){Debug.LogError("M5 install stopped: "+e+"\nNo source scenes or Git data modified. Current edits can be inspected/undone.");}
    }
    static GameObject Child(string name,Transform parent)
    { var go=new GameObject(name); Undo.RegisterCreatedObjectUndo(go,name); go.transform.SetParent(parent,false); return go; }
    static void InstallRoom(Transform root,string name,float angle,ApartmentFurnitureInteractor owner)
    {
        var hinge=root.Find("Door_"+name+"_Hinge"); var leaf=hinge.Find("Door_"+name);
        foreach(var script in hinge.GetComponentsInChildren<MonoBehaviour>(true))
            if(script!=null && (script is QuestGuidance.Locomotion.DoorAutoSwing || (script.GetType().Namespace??"").StartsWith("Oculus.Interaction",StringComparison.Ordinal)))
            { Undo.RecordObject(script,"Use trigger door motion"); script.enabled=false; }
        var renderer=leaf.GetComponent<MeshRenderer>(); Bounds world=renderer.bounds;
        // The authored visual mesh extends below the floor. The solid moving leaf starts just above the floor.
        int floorMask=owner.locomotion.teleportFloorMask;
        var groundHits=Physics.RaycastAll(new Vector3(world.center.x,world.max.y+.3f,world.center.z),Vector3.down,world.size.y+2f,floorMask,QueryTriggerInteraction.Ignore);
        if(groundHits.Length==0) throw new InvalidOperationException("房门脚下没有地面："+name);
        float floor=groundHits.Max(h=>h.point.y);
        var localBounds=leaf.GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 min=localBounds.min,max=localBounds.max;
        min.y=Mathf.Max(min.y,leaf.InverseTransformPoint(new Vector3(world.center.x,floor+.003f,world.center.z)).y);
        if(max.y<=min.y) throw new InvalidOperationException("房门碰撞高度无效。");
        foreach(var c in leaf.GetComponents<Collider>()){Undo.RecordObject(c,"Preserve original disabled mesh collider");c.enabled=false;}
        var box=Undo.AddComponent<BoxCollider>(leaf.gameObject); box.center=(min+max)*.5f; box.size=max-min;
        var body=hinge.GetComponent<Rigidbody>()??Undo.AddComponent<Rigidbody>(hinge.gameObject);
        Undo.RecordObject(body,"Kinematic controlled door");body.isKinematic=true;body.useGravity=false;
        var door=Undo.AddComponent<ApartmentRoomDoor>(hinge.gameObject); door.doorCollider=box;door.doorRenderer=renderer;
        door.hingeBody=body;door.inputOwner=owner;door.openAngle=angle;door.degreesPerSecond=90;door.closedLocalRotation=hinge.localRotation;
        FitRoomProbe(door);
        EditorUtility.SetDirty(door);
    }
    static void FitRoomProbe(ApartmentRoomDoor door)
    {
        var leaf=door.doorCollider.transform;var box=door.doorCollider;
        var walls=Find(door.gameObject.scene,"DetailedEnvironment/House").GetComponentsInChildren<Collider>()
            .Where(c=>c.enabled&&!c.isTrigger&&(c.name.StartsWith("Wall",StringComparison.Ordinal)||c.name.StartsWith("Tile_",StringComparison.Ordinal))).ToArray();
        Bounds visual=door.doorRenderer.bounds;
        Bounds local=leaf.GetComponent<MeshFilter>().sharedMesh.bounds;Vector3 min=local.min,max=local.max;
        float floor=Physics.RaycastAll(new Vector3(visual.center.x,visual.max.y+.3f,visual.center.z),Vector3.down,visual.size.y+2f,
            door.inputOwner.locomotion.teleportFloorMask,QueryTriggerInteraction.Ignore).Max(h=>h.point.y);
        // Adjacent rooms can have a slightly raised carpet or bathroom tile within the sweep.
        float baseFloor=floor;
        foreach(var support in Physics.OverlapSphere(new Vector3(door.transform.position.x,floor,door.transform.position.z),
            Mathf.Max(visual.size.x,visual.size.z)+.1f,door.inputOwner.locomotion.teleportFloorMask,QueryTriggerInteraction.Ignore))
            if(support.bounds.max.y<=baseFloor+.2f)floor=Mathf.Max(floor,support.bounds.max.y);
        min.y=Mathf.Max(min.y,leaf.InverseTransformPoint(new Vector3(visual.center.x,floor+.003f,visual.center.z)).y);
        foreach(var wall in walls)
        {
            Bounds b=wall.bounds;
            if(b.min.y>floor+.5f && b.min.x<visual.max.x && b.max.x>visual.min.x && b.min.z<visual.max.z && b.max.z>visual.min.z)
                max.y=Mathf.Min(max.y,leaf.InverseTransformPoint(new Vector3(visual.center.x,b.min.y-.003f,visual.center.z)).y);
        }
        var aim=leaf.GetComponent<ApartmentDoorAimSurface>();
        if(aim==null)
        {
            aim=Undo.AddComponent<ApartmentDoorAimSurface>(leaf.gameObject);var surface=Undo.AddComponent<BoxCollider>(leaf.gameObject);
            surface.center=local.center;surface.size=local.size;surface.isTrigger=true;aim.surface=surface;EditorUtility.SetDirty(aim);
        }
        Vector3 hingeLocal=leaf.InverseTransformPoint(door.transform.position);
        Vector3 direction=local.center-hingeLocal;int axis=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)?0:2;
        // The decorative leaf slightly overlaps both jambs even when closed.
        // Fit the solid box to the opening; the separate full-size aiming surface remains opaque to rays.
        int thinAxis=axis==0?2:0;
        // Bathroom decorative trim is thicker than the 90-degree clearance beside its tiled return.
        // Keep the visible mesh and full aiming outline; fit only the solid panel core (6mm inset per face).
        if(door.name=="Door_Bath_Hinge")
        {min[thinAxis]+=.006f/leaf.lossyScale[thinAxis];max[thinAxis]-=.006f/leaf.lossyScale[thinAxis];}
        foreach(var wall in walls)
        {
            Bounds b=wall.bounds;Vector3 lo=leaf.InverseTransformPoint(b.min),hi=leaf.InverseTransformPoint(b.max);
            float frameDepth=.25f/leaf.lossyScale[thinAxis];
            if(lo.y>=max.y||hi.y<=min.y||lo[thinAxis]>=max[thinAxis]+frameDepth||hi[thinAxis]<=min[thinAxis]-frameDepth)continue;
            if(lo[axis]>local.center[axis])max[axis]=Mathf.Min(max[axis],lo[axis]-.003f/leaf.lossyScale[axis]);
            if(hi[axis]<local.center[axis])min[axis]=Mathf.Max(min[axis],hi[axis]+.003f/leaf.lossyScale[axis]);
        }
        float initialNear=direction[axis]>0?min[axis]:max[axis];float scale=leaf.lossyScale[axis];
        Undo.RecordObject(box,"Fit physics probe to authored doorway, retain full pointing outline");
        string lastBlock="";
        for(int attempt=0;attempt<=40;attempt++)
        {
            if(direction[axis]>0)min[axis]=initialNear+attempt*.005f/scale;else max[axis]=initialNear-attempt*.005f/scale;
            box.center=(min+max)*.5f;box.size=max-min;Physics.SyncTransforms();bool clear=true;
            for(int step=0;step<=180&&clear;step++)
            {
                Quaternion rotation=door.transform.parent.rotation*door.closedLocalRotation*Quaternion.AngleAxis(door.openAngle*step/180f,Vector3.up);
                Vector3 position=door.transform.position+rotation*leaf.localPosition;Quaternion leafRotation=rotation*leaf.localRotation;
                foreach(var wall in walls)
                    if(Physics.ComputePenetration(box,position,leafRotation,wall,wall.transform.position,wall.transform.rotation,out _,out float depth)&&depth>.0002f)
                    {lastBlock=wall.name+" angle="+door.openAngle*step/180f+" depth="+depth+" box="+box.size;clear=false;break;}
            }
            if(clear){EditorUtility.SetDirty(box);Debug.Log("M5 doorway fitted "+door.name+": hinge fitting allowance "+attempt*.005f+"m; visible mesh/hinge/walls unchanged.");return;}
        }
        throw new InvalidOperationException("门洞需要进一步检查，未忽略墙体碰撞："+door.name+" "+lastBlock);
    }
    [MenuItem("Apartment Phase 2/M5 Upgrade/Fit Existing Doorway Probes %#&7")]
    public static void FitExistingDoorways()
    {
        try
        {
            var scene=RequireScene();Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Fit room door physics to original doorways");
            foreach(var door in All<ApartmentRoomDoor>(scene))FitRoomProbe(door);
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("M5 room doorway fitting saved.");
        }
        catch(Exception e){Debug.LogError("M5 fitting stopped: "+e);}
    }
    [MenuItem("Apartment Phase 2/M5 Upgrade/Apply Approved Outward Entrance Door %#&6")]
    public static void ApplyApprovedEntranceDirection()
    {
        try
        {
            var scene=RequireScene();
            var door=Find(scene,"DetailedEnvironment/House/Doors/Door_Entrance_Hinge").GetComponent<ApartmentRoomDoor>();
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("User approved outward entrance door");
            Undo.RecordObject(door,"Reverse only the entrance door swing");door.openAngle=90;EditorUtility.SetDirty(door);
            var note=Find(scene,"DetailedEnvironment").Find("M5 shoe clearance pending");
            if(note!=null){Undo.RecordObject(note.gameObject,"Record final approved solution");note.name="M5 approved outward entrance";}
            FitRoomProbe(door);Physics.SyncTransforms();Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("M5 entrance door now opens outward 90 degrees; shoe cabinet restored, all furniture/hinges/walls unchanged.");
        }
        catch(Exception e){Debug.LogError("M5 entrance direction stopped: "+e);}
    }
    static ApartmentSlidingDoor InstallSliding(Transform root,ApartmentFurnitureInteractor owner)
    {
        var group=Child("BalconyTriggerDoor",root).transform;
        var moving=Child("MovingLeftLeaf",group).transform;
        var fixedLeaf=Child("FixedRightLeaf",group).transform;
        var colliders=new System.Collections.Generic.List<BoxCollider>();
        foreach(string side in new[]{"L","R"}) foreach(string suffix in FrameSuffixes)
        {
            var part=root.Find("Door_Bal_"+side+suffix); Undo.SetTransformParent(part,side=="L"?moving:fixedLeaf,"Group original sliding door parts");
            if(side=="L")
            {
                foreach(var c in part.GetComponents<Collider>()){Undo.RecordObject(c,"Preserve original sliding mesh collider");c.enabled=false;}
                var box=Undo.AddComponent<BoxCollider>(part.gameObject);var bounds=part.GetComponent<MeshFilter>().sharedMesh.bounds;
                box.center=bounds.center;box.size=bounds.size;colliders.Add(box);
            }
        }
        var body=Undo.AddComponent<Rigidbody>(moving.gameObject);body.isKinematic=true;body.useGravity=false;
        var slider=Undo.AddComponent<ApartmentSlidingDoor>(group.gameObject);slider.movingLeaf=moving;slider.movingColliders=colliders.ToArray();
        slider.body=body;slider.inputOwner=owner;slider.doorRenderer=moving.Find("Door_Bal_L").GetComponent<Renderer>();
        slider.closedLocalPosition=moving.localPosition;slider.localTravel=new Vector3(1.55f,0,0);slider.metresPerSecond=.75f;
        EditorUtility.SetDirty(slider); return slider;
    }
    public static string RequiredCharacters() => new string((ApartmentTutorialController.AllText+ApartmentCabinetDoor.FontCharacters+
        new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray())).Where(c=>!char.IsControl(c)).Distinct().ToArray());
    static TMP_FontAsset EnsureFont()
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if(font!=null){foreach(char c in RequiredCharacters())if(!font.HasCharacter(c))throw new InvalidOperationException("教程字体缺少："+c);return font;}
        var source=AssetDatabase.LoadAssetAtPath<Font>(ApartmentFurnitureSetup.FontSource);
        font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
        if(font==null || !font.TryAddCharacters(RequiredCharacters(),out string missing)) throw new InvalidOperationException("教程中文字形生成失败。");
        font.name="VRGuide-SC";font.atlasPopulationMode=AtlasPopulationMode.Static; font.hideFlags=HideFlags.None; font.material.hideFlags=HideFlags.None;
        AssetDatabase.CreateAsset(font,FontPath);AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var t in font.atlasTextures){t.hideFlags=HideFlags.None;AssetDatabase.AddObjectToAsset(t,font);EditorUtility.SetDirty(t);}
        EditorUtility.SetDirty(font);EditorUtility.SetDirty(font.material);AssetDatabase.SaveAssets();return font;
    }
    public static void Validate(Scene scene)
    {
        var mover=All<ApartmentVRLocomotion>(scene).Single();var owner=All<ApartmentFurnitureInteractor>(scene).Single();
        var tutorial=All<ApartmentTutorialController>(scene).Single();
        if(mover.ConfigurationError()!=null || mover.TeleportConfigurationError()!=null || mover.teleportValidationMode!=ApartmentTeleportValidationMode.FloorLanding ||
            mover.tutorial!=tutorial || owner.tutorial!=tutorial || tutorial.interactor!=owner || tutorial.locomotion!=mover || tutorial.font==null ||
            tutorial.panelShader==null || tutorial.textOverlayShader==null)
            throw new InvalidOperationException("M5 player/tutorial wiring invalid.");
        if(All<OVRCameraRig>(scene).Length!=1 || owner.doors.Length!=10 || owner.doors.Distinct().Count()!=10 ||
            All<ApartmentRoomDoor>(scene).Length!=4 || All<ApartmentCabinetDoor>(scene).Count(d=>!(d is ApartmentRoomDoor))!=6 ||
            owner.slidingDoors.Length!=1 || All<ApartmentSlidingDoor>(scene).Length!=1)
            throw new InvalidOperationException("M5 door/rig count invalid.");
        foreach(var door in owner.doors) if(door.ConfigurationError()!=null) throw new InvalidOperationException(door.name+": "+door.ConfigurationError());
        if(owner.slidingDoors[0].ConfigurationError()!=null) throw new InvalidOperationException(owner.slidingDoors[0].ConfigurationError());
        if(All<QuestGuidance.Locomotion.DoorAutoSwing>(scene).Any(d=>d.enabled)) throw new InvalidOperationException("Automatic door driver still enabled.");
        foreach(char c in RequiredCharacters())if(!tutorial.font.HasCharacter(c))throw new InvalidOperationException("Missing glyph: "+c);
    }

    [MenuItem("Apartment Phase 2/M5 Upgrade/Run Upgrade Checks %#&0")]
    public static void Check() { ApartmentExperienceChecks.Run(); }

    [MenuItem("Apartment Phase 2/M5 Upgrade/Render Tutorial Preview %#&8")]
    public static void RenderPreview()
    {
        Scene scratch=default;RenderTexture texture=null;Texture2D png=null;
        try
        {
            var original=RequireScene();var source=All<ApartmentTutorialController>(original).Single();
            scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var go=new GameObject("UI preview only");var preview=go.AddComponent<ApartmentTutorialController>();preview.font=source.font;preview.locomotion=source.locomotion;
            typeof(ApartmentTutorialController).GetMethod("EnsureVisuals",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(preview,null);
            var rect=go.GetComponentInChildren<Canvas>().GetComponent<RectTransform>();rect.position=new Vector3(1000,1000,1001.2f);rect.rotation=Quaternion.identity;rect.sizeDelta=new Vector2(900,420);
            foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var labels=go.GetComponentsInChildren<TextMeshProUGUI>();var content=labels.Single(t=>t.name=="Chinese instruction");
            content.rectTransform.offsetMin=new Vector2(30,116);content.rectTransform.offsetMax=new Vector2(-30,-18);content.fontSize=38;
            content.enableAutoSizing=true;content.fontSizeMin=28;content.fontSizeMax=38;
            content.text=ApartmentTutorialController.Lessons[0]+"\n\n"+ApartmentTutorialController.ConfirmHelp;
            rect.Find("OK").GetComponent<UnityEngine.UI.Image>().color=new Color(.12f,.4f,.75f,1);
            var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,1000);camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.2f);camera.orthographic=true;camera.orthographicSize=.27f;
            rect.GetComponent<Canvas>().worldCamera=camera;
            texture=new RenderTexture(1200,650,24);camera.targetTexture=texture;
            Canvas.ForceUpdateCanvases();foreach(var label in labels)label.ForceMeshUpdate();camera.Render();
            foreach(var lesson in ApartmentTutorialController.Lessons.Concat(ApartmentTutorialController.KindLessons))
            {
                content.text=lesson+"\n\n"+ApartmentTutorialController.ConfirmHelp;content.ForceMeshUpdate();
                if(content.isTextOverflowing)throw new InvalidOperationException("Chinese tutorial panel overflow: "+lesson);
            }
            content.text=ApartmentTutorialController.Lessons[0]+"\n\n"+ApartmentTutorialController.ConfirmHelp;content.ForceMeshUpdate();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=texture;
            try{png=new Texture2D(1200,650,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1200,650),0,0);png.Apply();}
            finally{RenderTexture.active=previous;}
            Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/M5-TutorialPreview.png",png.EncodeToPNG());
            SceneManager.SetActiveScene(original);Debug.Log("M5 tutorial preview rendered: Logs/M5-TutorialPreview.png (not a headset capture).");
        }
        catch(Exception e){Debug.LogError("M5 preview failed: "+e);}
        finally{if(scratch.IsValid())EditorSceneManager.CloseScene(scratch,true);if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}if(png!=null)Object.DestroyImmediate(png);}
    }
}
#endif
