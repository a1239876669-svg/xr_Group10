"""Phase A asset authoring/checks. Default: read-only checks; --generate: explicitly rebuild assets.
Uses only Python's standard library. Does not start Unity or execute Unity scripts.
"""
import re
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[1]
HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
SDK = {"Grabbable": "43f86b14a27b52f4f9298c33015b5c26",
       "GrabInteractable": "5a1bc992571301d4a9602ac95ef4c71a",
       "GrabFreeTransformer": "bbb7085c111fcb44493d57d4e336d3fd"}

def guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "xr-group10-shopping-bag/" + path).hex

def ref(fid, asset=None):
    return "{fileID: " + str(fid) + (", guid: " + guid(asset) + ", type: 2" if asset else "") + "}"

def vec(v):
    return "{x: %g, y: %g, z: %g}" % tuple(v)

def base():
    return "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"

def block(kind, fid, name, fields):
    return f"--- !u!{kind} &{fid}\n{name}:\n" + base() + fields

def go(fid, name, components):
    return block(1, fid, "GameObject", "  serializedVersion: 6\n  m_Component:\n" +
                 "".join(f"  - component: {ref(c)}\n" for c in components) +
                 f"  m_Layer: 0\n  m_Name: {name}\n  m_TagString: Untagged\n  m_IsActive: 1\n")

def transform(fid, gid, parent=0, children=(), position=(0, 0, 0), scale=(1, 1, 1)):
    return block(4, fid, "Transform", f"  m_GameObject: {ref(gid)}\n  serializedVersion: 2\n"
                 "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n" +
                 f"  m_LocalPosition: {vec(position)}\n  m_LocalScale: {vec(scale)}\n"
                 "  m_ConstrainProportionsScale: 0\n" +
                 ("  m_Children:\n" + "".join(f"  - {ref(c)}\n" for c in children) if children else "  m_Children: []\n") +
                 f"  m_Father: {ref(parent)}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n")

def mono(fid, gid, script, fields="", sdk=False):
    sguid = SDK[script] if sdk else guid("Scripts/" + script + ".cs")
    return block(114, fid, "MonoBehaviour", f"  m_GameObject: {ref(gid)}\n  m_Enabled: 1\n"
                 f"  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {sguid}, type: 3}}\n"
                 "  m_Name: \n  m_EditorClassIdentifier: \n" + fields)

def collider(fid, gid, size=(1, 1, 1), center=(0, 0, 0), trigger=False):
    return block(65, fid, "BoxCollider", f"  m_GameObject: {ref(gid)}\n  m_Material: {{fileID: 0}}\n"
                 f"  m_IsTrigger: {int(trigger)}\n  m_ProvidesContacts: 0\n  m_Enabled: 1\n"
                 f"  serializedVersion: 3\n  m_Size: {vec(size)}\n  m_Center: {vec(center)}\n")

def cube(fid, name, parent=0, position=(0, 0, 0), scale=(1, 1, 1), solid=False, handle=False):
    mat = "Materials/Handle.mat" if handle else "Materials/Whitebox.mat"
    text = go(fid, name, [fid+1, fid+2, fid+3] + ([fid+4] if solid else []))
    text += transform(fid+1, fid, parent, position=position, scale=scale)
    text += block(33, fid+2, "MeshFilter", f"  m_GameObject: {ref(fid)}\n"
                  "  m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}\n")
    text += block(23, fid+3, "MeshRenderer", f"  m_GameObject: {ref(fid)}\n  m_Enabled: 1\n"
                  "  m_CastShadows: 1\n  m_ReceiveShadows: 1\n  m_DynamicOccludee: 1\n"
                  "  m_MotionVectors: 1\n  m_RenderingLayerMask: 1\n  m_Materials:\n"
                  f"  - {ref(2100000, mat)}\n  m_SortingLayerID: 0\n  m_SortingOrder: 0\n")
    if solid:
        text += collider(fid+4, fid)
    return text

def write(path, text):
    p = ROOT / path
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8", newline="\n")

def generate():
    for name, dims, height in [("Small", (.30, .30, .15), .10), ("Medium", (.45, .40, .22), .12), ("Large", (.60, .48, .30), .14)]:
        write("Config/" + name + ".asset", HEADER + mono(11400000, 0, "BagSizeProfile",
              f"  bodySize: {vec(dims)}\n  handleHeight: {height}\n  handleThickness: 0.02\n").replace("  m_Name: \n", f"  m_Name: {name}\n"))
    for name, color in [("Whitebox", (.86, .84, .78)), ("Handle", (.20, .23, .25))]:
        write("Materials/" + name + ".mat", HEADER + block(21, 2100000, "Material",
              f"  serializedVersion: 8\n  m_Name: {name}\n"
              "  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}\n"
              "  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n"
              "  m_EnableInstancingVariants: 1\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n"
              "  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n"
              "    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n"
              "    - _Glossiness: 0.15\n    - _Metallic: 0\n    m_Colors:\n"
              f"    - _Color: {{r: {color[0]}, g: {color[1]}, b: {color[2]}, a: 1}}\n"))
    text = HEADER + go(1000, "ShoppingBag", [1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008])
    text += transform(1001, 1000, children=[1101, 1201, 1301, 1401, 1501, 1601])
    text += block(54, 1002, "Rigidbody", "  m_GameObject: {fileID: 1000}\n  serializedVersion: 5\n"
                  "  m_Mass: 0.3\n  m_LinearDamping: 0.1\n  m_AngularDamping: 0.5\n"
                  "  m_CenterOfMass: {x: 0, y: 0, z: 0}\n  m_InertiaTensor: {x: 1, y: 1, z: 1}\n"
                  "  m_InertiaRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_ImplicitCom: 1\n  m_ImplicitTensor: 1\n"
                  "  m_UseGravity: 1\n  m_IsKinematic: 0\n  m_Interpolate: 1\n  m_Constraints: 0\n  m_CollisionDetection: 3\n")
    text += mono(1003, 1000, "Grabbable", "  _transferOnSecondSelection: 0\n  _addNewPointsToFront: 0\n"
                 "  _forwardElement: {fileID: 0}\n  _oneGrabTransformer: {fileID: 1008}\n"
                 "  _twoGrabTransformer: {fileID: 0}\n  _targetTransform: {fileID: 1001}\n  _maxGrabPoints: 1\n"
                 "  _rigidbody: {fileID: 1002}\n  _kinematicWhileSelected: 1\n  _throwWhenUnselected: 0\n"
                 "  _forceKinematicDisabled: 0\n", sdk=True)
    text += mono(1004, 1000, "GrabInteractable", "  _interactorFilters:\n  - {fileID: 1007}\n"
                 "  _maxInteractors: -1\n  _maxSelectingInteractors: 1\n  _data: {fileID: 0}\n"
                 "  _pointableElement: {fileID: 1003}\n  _rigidbody: {fileID: 1002}\n"
                 "  _grabSource: {fileID: 1501}\n  _useClosestPointAsGrabSource: 0\n"
                 "  _releaseDistance: 0\n  _resetGrabOnGrabsUpdated: 0\n", sdk=True)
    text += collider(1005, 1000, (.45, .40, .22), (0, .20, 0))
    text += mono(1006, 1000, "ShoppingBag", "".join(f"  {n}: {ref(11400000, 'Config/'+s+'.asset')}\n" for n,s in [("small","Small"),("medium","Medium"),("large","Large")]) +
                 "  initialSize: 1\n  grabbable: {fileID: 1003}\n  grabInteractable: {fileID: 1004}\n"
                 "  body: {fileID: 1002}\n  bodyVisual: {fileID: 1101}\n  leftHandle: {fileID: 1201}\n"
                 "  rightHandle: {fileID: 1301}\n  topHandle: {fileID: 1401}\n  grabPoint: {fileID: 1501}\n"
                 "  bodyCollider: {fileID: 1005}\n  handleTrigger: {fileID: 1602}\n")
    text += mono(1007, 1000, "BagGripFilter", "  bag: {fileID: 1006}\n")
    text += mono(1008, 1000, "GrabFreeTransformer", sdk=True)
    text += cube(1100, "Body", 1001, (0, .20, 0), (.45, .40, .22))
    text += cube(1200, "HandleLeft", 1001, (-.12375, .46, 0), (.02, .12, .02), handle=True)
    text += cube(1300, "HandleRight", 1001, (.12375, .46, 0), (.02, .12, .02), handle=True)
    text += cube(1400, "HandleTop", 1001, (0, .52, 0), (.2675, .02, .02), handle=True)
    text += go(1500, "GrabPoint", [1501]) + transform(1501, 1500, 1001, position=(0, .52, 0))
    text += go(1600, "GrabHandleVolume", [1601, 1602]) + transform(1601, 1600, 1001)
    text += collider(1602, 1600, (.2675, .04, .04), (0, .52, 0), trigger=True)
    write("Prefabs/ShoppingBag.prefab", text)
    # Additive content scene: deliberately has no Camera, Rig, input or movement owner.
    pguid = guid("Prefabs/ShoppingBag.prefab")
    scene = HEADER + cube(2000, "ShoppingBag_TestFloor", position=(0, -.05, 1.2), scale=(4, .10, 4), solid=True)
    scene += cube(2100, "ShoppingBag_TestStand", position=(0, .375, 1.2), scale=(.8, .75, .8), solid=True)
    scene += "--- !u!1001 &3000\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n    m_Modifications:\n"
    for axis,value in [("x",0), ("y",.75), ("z",1.2)]:
        scene += f"    - target: {{fileID: 1001, guid: {pguid}, type: 3}}\n      propertyPath: m_LocalPosition.{axis}\n      value: {value}\n      objectReference: {{fileID: 0}}\n"
    scene += f"    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n  m_SourcePrefab: {{fileID: 100100000, guid: {pguid}, type: 3}}\n"
    scene += f"--- !u!114 &3001 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 1006, guid: {pguid}, type: 3}}\n  m_PrefabInstance: {{fileID: 3000}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {guid('Scripts/ShoppingBag.cs')}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n"
    scene += go(4000, "ShoppingBag_TestControls", [4001,4002]) + transform(4001,4000)
    scene += mono(4002, 4000, "BagSizeTestControls", "  bag: {fileID: 3001}\n  showDesktopPanel: 1\n")
    scene += "--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {fileID: 2001}\n  - {fileID: 2101}\n  - {fileID: 3002}\n  - {fileID: 4001}\n"
    scene += f"--- !u!4 &3002 stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: 1001, guid: {pguid}, type: 3}}\n  m_PrefabInstance: {{fileID: 3000}}\n  m_PrefabAsset: {{fileID: 0}}\n"
    write("ShoppingBag_Test.unity", scene)
    metadata()

def metadata():
    paths = [ROOT] + sorted(ROOT.rglob("*"))
    for p in paths:
        if p.suffix == ".meta" or "__pycache__" in p.parts:
            continue
        path = p.relative_to(ROOT).as_posix() if p != ROOT else ""
        meta = Path(str(p) + ".meta")
        if meta.exists():
            continue
        data = "fileFormatVersion: 2\nguid: " + guid(path) + "\n"
        if p.is_dir():
            data += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif p.suffix == ".cs":
            data += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {fileID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif p.suffix == ".prefab":
            data += "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif p.suffix in (".asset", ".mat"):
            data += "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: " + ("2100000" if p.suffix == ".mat" else "11400000") + "\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        elif p.suffix == ".asmdef":
            data += "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        else:
            data += "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        meta.write_text(data, encoding="utf-8", newline="\n")

def check():
    files = [p for p in ROOT.rglob("*") if p.is_file() and p.suffix != ".meta"]
    metas = {re.search(r"guid: (\w+)", p.read_text(encoding="utf-8")).group(1): p for p in ROOT.rglob("*.meta")}
    assert len(metas) == len(list(ROOT.rglob("*.meta"))), "duplicate GUID"
    for p in (PROJECT / "Assets").rglob("*.meta"):
        if ROOT in p.parents or p == ROOT.with_suffix(".meta"):
            continue
        match = re.search(r"guid: (\w+)", p.read_text(encoding="utf-8-sig"))
        assert match is None or match.group(1) not in metas, f"GUID collides with existing asset: {p}"
    for p in files:
        assert Path(str(p)+".meta").exists(), f"missing meta: {p}"
        if p.suffix not in (".prefab", ".unity", ".asset", ".mat"):
            continue
        s = p.read_text(encoding="utf-8")
        ids = re.findall(r"^--- !u!\d+ &(\d+)", s, re.M)
        assert len(ids) == len(set(ids)), f"duplicate fileID in {p}"
        for fid in re.findall(r"\{fileID: (\d+)\}", s):
            assert fid == "0" or fid in ids, f"broken local reference {fid} in {p}"
        for g in re.findall(r"guid: ([a-f0-9]{32})", s):
            assert g in metas or g in SDK.values() or g.startswith("00000000"), f"unknown asset GUID {g}"
    prefab = (ROOT/"Prefabs/ShoppingBag.prefab").read_text(encoding="utf-8")
    assert "_maxSelectingInteractors: 1" in prefab and "_maxGrabPoints: 1" in prefab
    assert "_throwWhenUnselected: 0" in prefab and "_useClosestPointAsGrabSource: 0" in prefab
    assert "_pointableElement: {fileID: 1003}" in prefab and "_grabSource: {fileID: 1501}" in prefab
    scene = (ROOT/"ShoppingBag_Test.unity").read_text(encoding="utf-8")
    assert not re.search(r"^--- !u!20 &", scene, re.M), "test content must not introduce a camera"
    for n, expected in [("Small", (.30,.30,.15)), ("Medium", (.45,.40,.22)), ("Large", (.60,.48,.30))]:
        assert "bodySize: " + vec(expected) in (ROOT/f"Config/{n}.asset").read_text(encoding="utf-8")
    print(f"PASS: {len(files)} files, GUID/fileID references, SDK near-grab wiring, sizes and camera-free scene. Unity import/runtime not tested.")

def inventory():
    write("Documentation/FileInventory.md", "")
    metadata()
    files = [ROOT.with_suffix(".meta")] + sorted(p for p in ROOT.rglob("*") if p.is_file())
    rows = "\n".join("- `" + p.relative_to(PROJECT).as_posix() + "`" for p in files)
    write("Documentation/FileInventory.md", "# 阶段 A 新建文件清单\n\n所有文件均为新增，未修改已有文件。共 " + str(len(files)) + " 个文件（包含目录和资源的 .meta）。\n\n" + rows + "\n")

if __name__ == "__main__":
    if "--generate" in sys.argv:
        generate()
    elif "--inventory" in sys.argv:
        inventory()
    elif "--metadata" in sys.argv:
        metadata()
    check()
