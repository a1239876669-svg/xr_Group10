"""Read-only static integration checks; does not start Unity or claim runtime acceptance."""
from pathlib import Path
import re
R=Path(__file__).resolve().parents[3]
def read(p):return (R/p).read_text(encoding='utf-8-sig')
def docs(s):return {fid:(typ,text) for typ,fid,text in re.findall(r'--- !u!(\d+) &(-?\d+)(.*?)(?=\n--- !u!|\Z)',s,re.S)}
scene=read('Assets/ShoppingBag/Integration/ShoppingBag_UI_Test.unity');d=docs(scene)
assert not any(t=='20' for t,s in d.values()), 'Fixture must have no Camera'
assert 'ShoppingBag_TestControls' not in scene, 'Desktop simulator/OnGUI must not own integrated UI'
assert 'propertyPath: contextProvider\n      value: \n      objectReference: {fileID: 7002}' in scene
assert 'bag: {fileID: 3001}' in d['7002'][1] and 'gate: {fileID: 3004}' in d['7002'][1]
assert 'bridge: {fileID: 7002}' in d['7003'][1] and 'fixtureRoot: {fileID: 7001}' in d['7003'][1]
assert 'uiScenePath: Assets/Scenes/Apartment_Detailed_VR.unity' in scene
ids=set(d)
for fid,(typ,s) in d.items():
    for line in s.splitlines():
        if 'guid:' in line:continue
        for target in re.findall(r'\{fileID: (-?\d+)\}',line):
            assert target=='0' or target in ids,(fid,line)
assert all('m_Father: {fileID: 7001}' in d[fid][1] for fid in ['2001','2101','5001','5101','5201','5301'])
assert 'm_TransformParent: {fileID: 7001}' in d['3000'][1]
ui=docs(read('Assets/Scenes/Apartment_Detailed_VR.unity'))
assert 'm_Name: ApartmentMenu' in ui['184434898'][1]
assert 'm_Father: {fileID: 542895440}' in ui['184434900'][1]
player=re.search(r'm_GameObject: \{fileID: (-?\d+)\}',ui['542895440'][1]).group(1)
assert 'm_Name: VRPlayer' in ui[player][1] and 'm_Father: {fileID: 0}' in ui['542895440'][1], 'Exact UI path changed'
font=read('Assets/Apartment/Fonts/VRStandaloneUI-SC.asset');chars=set(map(int,re.findall(r'm_Unicode: (\d+)',font)))
assert all(ord(c) in chars for c in 'Shopping Bag Small Medium Large Current size: Held - release first Not configured ●○'), 'Static font lacks new UI glyphs'
menu=read('Assets/Scripts/ApartmentMenuController.cs')
assert 'else if (Page == 3)' in menu and 'shoppingBag.BuildMyItemsPage(view)' in menu
assert 'public void RefreshMyItems()' in menu and 'Page != 3' in menu
bridge=read('Assets/ShoppingBag/Integration/ApartmentShoppingBagBridge.cs')
assert 'bag.TrySetSize(size)' in bridge and 'bag.IsHeld' in bridge
assert 'menu.BlocksWorldInput' in bridge and '!tutorial.AllowInteraction' in bridge and 'locomotion.IsTeleportAiming' in bridge
assert 'localScale =' not in bridge and 'FindObjects' not in bridge
for p in (R/'Assets/ShoppingBag/Integration').rglob('*'):
    if p.suffix!='.meta':assert Path(str(p)+'.meta').exists(),p
assert (R/'Assets/ShoppingBag/Integration.meta').exists()
print('PASS: content-only fixture, serialized Bridge/Gate, existing UI exact path, hierarchy/fileIDs, font coverage, real sizing and public-state wiring. Runtime/Unity import NOT tested.')