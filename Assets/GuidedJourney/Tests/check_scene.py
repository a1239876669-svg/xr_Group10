"""Read-only guided-journey scene/asset contract checks. No Unity or project code execution."""
from pathlib import Path
import subprocess,re,json,hashlib
R=Path(__file__).resolve().parents[3]
SCENE='Assets/Scenes/Apartment_Detailed_VR.unity'
BASE='a27dd1c6f937ed694d69e885dd2c83cf512cd9f8'  # Audited integration baseline, also valid after commit.
def git(*args):
    p=subprocess.run(['git','-C',str(R),*args],capture_output=True)
    assert p.returncode==0,p.stderr.decode(errors='replace')
    return p.stdout.decode('utf-8',errors='replace')
def docs(s):
    result={}
    for typ,fid,body in re.findall(r'^--- !u!(\d+) &(-?\d+)(.*?)(?=\n--- !u!|\Z)',s,re.M|re.S):
        assert fid not in result,'Duplicate local fileID: '+fid
        result[fid]=(typ,body)
    return result
scene=(R/SCENE).read_text(encoding='utf-8-sig');after=docs(scene);before=docs(git('show',BASE+':'+SCENE))
added=set(after)-set(before);expected={'2147000001','2147000002','2147000003','2147000004','2147000005'}
assert added==expected and not(set(before)-set(after))
changed={k for k in before if before[k]!=after[k]}
assert changed=={'27991925','1260247394','1260247395','1261912974','9223372036854775807'}
# Existing meshes, colliders, transforms, physics, Rig and movement configuration remain unchanged.
for k in before:
    if before[k][0] in ('4','23','65','136','143','64','54','20','43'):assert before[k]==after[k],k
for fid,(typ,body) in after.items():
    for line in body.splitlines():
        if 'guid:' in line:continue
        for target in re.findall(r'\{fileID: (-?\d+)\}',line):assert target=='0' or target in after,(fid,target)
guide=after['2147000001'][1]
for key,value in {'tutorial':'1260247395','menu':'184434899','locomotion':'542895441','interactor':'742092206','roomMap':'854460277','review':'854460278','bagBridge':'1082077889','entranceDoor':'171350835','balconyDoor':'527763517','practiceCabinet':'154016231','kitchenDropPoint':'2147000003'}.items():assert key+': {fileID: '+value+'}' in guide,key
assert 'guidedJourney: {fileID: 2147000001}' in after['1260247395'][1]
assert 'm_Name: GuidedJourney_KitchenDropPoint_VisualOnly' in after['2147000002'][1]
assert 'm_LocalPosition: {x: 2.1, y: 0.012, z: 5.5}' in after['2147000003'][1]
for component,collider,renderer in [('2147000004','1261912979','1261912978'),('2147000005','27991930','27991929')]:
    block=after[component][1]
    assert 'furnitureColliders:\n  - {fileID: '+collider+'}' in block
    assert 'furnitureRenderers:\n  - {fileID: '+renderer+'}' in block
    assert '77b608892d835a498d83230311d36980' in block
# New custom script is resolvable; package-script import is separately a Unity check.
custom=R/'Assets/Scripts/GuidedJourney/ApartmentGuidedJourney.cs'
guid=re.search(r'^guid: (\w+)',Path(str(custom)+'.meta').read_text(),re.M)[1]
assert 'guid: '+guid in guide
metas={}
for m in (R/'Assets').rglob('*.meta'):
    x=re.search(r'^guid: ([0-9a-f]{32})\s*$',m.read_text(encoding='utf-8-sig'),re.M)
    if x:metas.setdefault(x[1],[]).append(m)
assert len(metas[guid])==1
for f in (R/'Assets/GuidedJourney').rglob('*'):
    if f.suffix!='.meta':assert Path(str(f)+'.meta').exists(),str(f)
for f in (R/'Assets/Scripts/GuidedJourney').rglob('*'):
    if f.suffix!='.meta':assert Path(str(f)+'.meta').exists(),str(f)
# A/B/C1 runtime source is untouched; the approved prefab difference is only Editor dry run.
for source in (R/'Assets/ShoppingBag/Scripts').glob('*.cs'):
    assert source.read_text(encoding='utf-8-sig')==git('show',BASE+':'+source.relative_to(R).as_posix()),str(source)
prefab='Assets/ShoppingBag/Prefabs/ShoppingBag.prefab';base=git('show',BASE+':'+prefab);current=(R/prefab).read_text(encoding='utf-8-sig')
assert base.count('dryRunInEditor: 1')==1 and base.replace('dryRunInEditor: 1','dryRunInEditor: 0')==current
assert not git('diff',BASE,'--name-only','--','Packages','ProjectSettings','Assets/XR').strip(),'Protected shared configuration changed'
print(json.dumps({'result':'PASS','sceneDocuments':len(after),'newSceneDocuments':len(added),'unchangedOriginalGeometryAndRig':True,'validNewSceneReferences':True,'contactTargets':sum('furnitureColliders:' in b for _,b in after.values()),'originalABAndC1RuntimeSourcesUnchanged':True,'onlyPrefabChange':'dryRunInEditor1->0','protectedConfigurationUnchanged':True,'unityImport':'Not Run'},indent=2))
