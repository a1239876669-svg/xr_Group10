"""Deterministic Phase B asset authoring. Default verifies; --generate updates only bag prefab/test scene/material/metas.
Never regenerate the three original size profiles. Uses standard-library helpers from Phase A.
"""
import importlib.util
import sys
from pathlib import Path
sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('bag_assets', Path(__file__).with_name('asset_checks.py'))
a = importlib.util.module_from_spec(spec)
spec.loader.exec_module(a)

def generate():
    p = a.ROOT / 'Prefabs/ShoppingBag.prefab'
    s = p.read_text(encoding='utf-8')
    if 'BagContactSensor' not in s:
        # Preserve all original fileIDs and SDK wiring. Append only new components/child.
        s = s.replace('  - component: {fileID: 1008}\n', '  - component: {fileID: 1008}\n  - component: {fileID: 1009}\n  - component: {fileID: 1010}\n', 1)
        s = s.replace('  - {fileID: 1601}\n', '  - {fileID: 1601}\n  - {fileID: 1701}\n', 1)
        s += a.mono(1009, 1000, 'BagHaptics', '  sensor: {fileID: 1703}\n  amplitude: 0.35\n  frequency: 0.5\n  duration: 0.08\n  cooldown: 0.25\n  dryRunInEditor: 0\n  logRequests: 0\n')
        s += a.mono(1010, 1000, 'BagInteractionGate', '  bag: {fileID: 1006}\n')
        s += a.go(1700, 'BagContactSensor', [1701, 1702, 1703]) + a.transform(1701, 1700, 1001)
        s += a.collider(1702, 1700, (.45,.40,.22), (0,.20,0), True)
        s += a.mono(1703, 1700, 'BagContactSensor', '  bag: {fileID: 1006}\n  contactTrigger: {fileID: 1702}\n  furnitureLayers:\n    serializedVersion: 2\n    m_Bits: 4294967295\n')
        a.write('Prefabs/ShoppingBag.prefab', s)
    a.write('Materials/ContactWarning.mat', a.HEADER + a.block(21, 2100000, 'Material',
        '  serializedVersion: 8\n  m_Name: ContactWarning\n'
        f'  m_Shader: {{fileID: 4800000, guid: {a.guid("Materials/BagWarning.shader")}, type: 3}}\n'
        '  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n'
        '  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: -1\n  stringTagMap: {}\n  disabledShaderPasses: []\n'
        '  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats: []\n'
        '    m_Colors:\n    - _Color: {r: 1, g: 0.015, b: 0.005, a: 1}\n'))
    # Keep the existing additive content scene, its prefab instance and controls.
    scene = (a.ROOT/'ShoppingBag_Test.unity').read_text(encoding='utf-8')
    if 'ChannelA_Left' not in scene:
        scene = scene.replace('  - component: {fileID: 4002}\n', '  - component: {fileID: 4002}\n  - component: {fileID: 4003}\n', 1)
        scene += a.mono(4003,4000,'BagDesktopTestDriver','  bag: {fileID: 3001}\n  sensor: {fileID: 3003}\n')
        pguid=a.guid('Prefabs/ShoppingBag.prefab')
        scene += f'--- !u!114 &3003 stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: 1703, guid: {pguid}, type: 3}}\n  m_PrefabInstance: {{fileID: 3000}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: 0}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {a.guid("Scripts/BagContactSensor.cs")}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n'
        for gid,name,x,z in [(5000,'ChannelA_Left',-.29,2.2),(5100,'ChannelA_Right',.29,2.2),
                               (5200,'ChannelB_Left',-.36,3.3),(5300,'ChannelB_Right',.36,3.3)]:
            part=a.cube(gid,name,position=(x,1.05,z),scale=(.2,.7,.45),solid=True)
            part=part.replace(f'  - component: {{fileID: {gid+4}}}\n',f'  - component: {{fileID: {gid+4}}}\n  - component: {{fileID: {gid+5}}}\n',1)
            part+=a.mono(gid+5,gid,'BagContactTarget',f'  furnitureColliders:\n  - {{fileID: {gid+4}}}\n  furnitureRenderers:\n  - {{fileID: {gid+3}}}\n  warningMaterial: {a.ref(2100000,"Materials/ContactWarning.mat")}\n')
            scene+=part
            scene=scene.replace('  - {fileID: 4001}\n',f'  - {{fileID: {gid+1}}}\n  - {{fileID: 4001}}\n',1)
        a.write('ShoppingBag_Test.unity',scene)
    # Extend only this test floor so release after the second gate still lands on the floor.
    scene = (a.ROOT/'ShoppingBag_Test.unity').read_text(encoding='utf-8')
    start=scene.index('--- !u!4 &2001\n');end=scene.index('--- !u!',start+4)
    floor=scene[start:end].replace('m_LocalPosition: {x: 0, y: -0.05, z: 1.2}', 'm_LocalPosition: {x: 0, y: -0.05, z: 2.4}').replace('m_LocalScale: {x: 4, y: 0.1, z: 4}', 'm_LocalScale: {x: 4, y: 0.1, z: 6}')
    a.write('ShoppingBag_Test.unity',scene[:start]+floor+scene[end:])
    a.metadata()
    shader_meta=a.ROOT/'Materials/BagWarning.shader.meta'
    text=shader_meta.read_text(encoding='utf-8')
    if 'DefaultImporter:' in text:
        shader_meta.write_text(text.replace('DefaultImporter:\n  externalObjects: {}','ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []'),encoding='utf-8',newline='\n')

if __name__ == '__main__':
    if '--generate' in sys.argv: generate()
    a.check()
    prefab=(a.ROOT/'Prefabs/ShoppingBag.prefab').read_text(encoding='utf-8')
    assert 'm_Name: BagContactSensor' in prefab and 'contactTrigger: {fileID: 1702}' in prefab
    assert 'handleTrigger: {fileID: 1602}' in prefab
    assert 'dryRunInEditor: 0' in prefab
    scene=(a.ROOT/'ShoppingBag_Test.unity').read_text(encoding='utf-8')
    for name in ['ChannelA_Left','ChannelA_Right','ChannelB_Left','ChannelB_Right']:
        assert f'm_Name: {name}' in scene
    print('PASS: Phase B separate body sensor, hardware-capable haptics prefab (desktop simulation stays dry), registered two-width fixed channel.')
