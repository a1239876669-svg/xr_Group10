"""Read-only contract/glyph checks for the dependent UI refinement; no Unity startup."""
from pathlib import Path
import subprocess,re,hashlib,json,runpy
R=Path(__file__).resolve().parents[2]
BASE="07ba53a976ff60237ed3a28726f065f829781e54"
def git(*args):
    p=subprocess.run(["git","-C",str(R),*args],capture_output=True,check=True)
    return p.stdout.decode("utf-8-sig").replace("\r\n","\n")
def read(p):return (R/p).read_text(encoding="utf-8-sig")
protected=["Packages","ProjectSettings","Assets/Scenes","Assets/ShoppingBag/Prefabs","Assets/ShoppingBag/Scripts","Assets/GuidedJourney/Core","Assets/Scripts/ApartmentVRLocomotion.cs","Assets/Scripts/Review/ReviewStorage.cs","Assets/Apartment/Fonts"]
assert not git("diff",BASE,"--name-only","--",*protected).strip(),"Unexpected protected difference"
xr=R/"Assets/XR/Settings/OpenXR Package Settings.asset"
assert hashlib.sha256(xr.read_bytes()).hexdigest() in {"817943a6381d46b00db8cd8bf00155466e2d4ac3c4ab25ff8ed94f23ae09b2f1","1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff"},"Unreviewed XR settings"
assert not git("diff",BASE,"--name-only","--",str(xr.relative_to(R))+".meta").strip()
# Stable serialized data fields and pure basic tutorial state remain byte-equivalent as text.
old=git("show",BASE+":Assets/Scripts/Review/ReviewTools.cs");new=read("Assets/Scripts/Review/ReviewTools.cs")
for name in ["ReviewRecord","ReviewSession"]:
    pattern=r"public class "+name+r"\s*\{.*?\n    \}"
    assert re.search(pattern,old,re.S)[0]==re.search(pattern,new,re.S)[0],name
old=git("show",BASE+":Assets/Scripts/ApartmentTutorialController.cs");new=read("Assets/Scripts/ApartmentTutorialController.cs")
assert old[old.index("public sealed class ApartmentTutorialProgress"):old.index("[DisallowMultipleComponent]")]==new[new.index("public sealed class ApartmentTutorialProgress"):new.index("[DisallowMultipleComponent]")]
# Existing formal scene and prefab local references have no new/dangling local IDs.
for scene in ["Assets/Scenes/Apartment_Detailed_VR.unity","Assets/ShoppingBag/Prefabs/ShoppingBag.prefab"]:
    data=read(scene);ids=set(re.findall(r"^--- !u!\d+ &(-?\d+)",data,re.M))
    for line in data.splitlines():
        if "guid:" in line:continue
        for fid in re.findall(r"\{fileID: (-?\d+)\}",line):assert fid=="0" or fid in ids,(scene,line)
scene=read("Assets/Scenes/Apartment_Detailed_VR.unity")
assert "safeResetPoint: {fileID: 1342695680}" in scene
assert "4cfb4a1d6ebf7d54eb1dd3294ba99787" not in scene,"Legacy debug pointer not allowed in formal scene"
# New assets must keep stable, unique GUIDs and .meta companions.
paths=[R/"Assets/Scripts/Localization",R/"Assets/Scripts/GuidedJourney",R/"Assets/GuidedJourney/Tests"]
allmeta={}
for m in (R/"Assets").rglob("*.meta"):
    value=re.search(r"^guid: ([0-9a-f]{32})\s*$",m.read_text(encoding="utf-8-sig"),re.M)
    if value:allmeta.setdefault(value[1],[]).append(m)
for folder in paths:
    for f in [folder,*folder.rglob("*")]:
        if f.suffix==".meta":continue
        m=Path(str(f)+".meta");assert m.exists(),str(f)
        value=re.search(r"^guid: ([0-9a-f]{32})\s*$",m.read_text(encoding="utf-8-sig"),re.M)[1]
        assert len(allmeta[value])==1,(f,"duplicate GUID")
g=runpy.run_path(str(R/"Assets/ShoppingBag/Tests/integration_checks.py"))
characters={ord(c) for p in (R/"Assets/Scripts/Localization").glob("*.cs") for c in p.read_text(encoding="utf-8") if ord(c)>=32 and ord(c)!=127}
missing=sorted(c for c in characters if not g["available"](c));assert not missing,missing
print(json.dumps({"result":"PASS","base":BASE,"scenePrefabRigLocomotionCoreUnchanged":True,"reviewVersion2FieldsUnchanged":True,"sourceGlyphCharacters":len(characters),"missingGlyphs":0,"formalRecoveryPointAssigned":True,"localXRRepair":"Allowed only as separately approved unstaged local test configuration; never part of UI commit","hardware":"Not Run"},indent=2))
