#!/usr/bin/env python3
"""Read-only Phase 3 source-geometry audit; Python standard library only.

Reads the frozen Unity YAML, referenced prefabs and ASCII FBX vertices. It does
not import Unity, modify assets, fetch Git objects, bake navigation or run project
code. JSON is written to stdout only. These are offline source calculations,
not Unity Renderer.bounds, Physics.ComputePenetration or Quest measurements.
"""
import argparse
from collections import Counter, defaultdict
from functools import lru_cache
import hashlib
import json
import math
from pathlib import Path
import re
import struct

SCENE = "Assets/Scenes/Apartment_Detailed_VR.unity"
RUG = 233736801
SOFA = 16152112
COFFEE = 1858018936
SELECTED = (16152112, 47526948, 221027377, 233736801, 247076001,
            730754860, 732500853, 1212511183, 1247993133, 1337975013,
            1517314930, 1580692883, 1629213756, 1858018936)
FLOOR_MESHES = {
    "base": (1448635152, 1448635153, 562284784),
    "bath": (172032079, 172032080, 2144755965),
    "balcony": (626166473, 626166474, 1660332696),
    "kitchen": (751686552, 751686553, 561512496),
    "ceiling_inactive": (1585286355, 1585286356, 255977288),
}

def parse_unity(text):
    return {int(m[2]): (int(m[1]), m[4]) for m in re.finditer(
        r"^--- !u!(\d+) &(-?\d+)([^\n]*)\n(.*?)(?=^--- !u!|\Z)",
        text, re.M | re.S)}

def field(body, key):
    m = re.search(r"^  " + re.escape(key) + r": (.*)$", body, re.M)
    return m[1] if m else ""

def reference(body, key):
    m = re.search(r"fileID: (-?\d+)", field(body, key))
    return int(m[1]) if m else 0

def vector(body, key):
    return tuple(float(x) for x in re.findall(
        r"[xyzw]: ([^,}]+)", field(body, key)))

def identity():
    return ((1., 0., 0., 0.), (0., 1., 0., 0.),
            (0., 0., 1., 0.), (0., 0., 0., 1.))

def multiply(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(4))
                       for j in range(4)) for i in range(4))

def matrix(body):
    p, s = vector(body, "m_LocalPosition"), vector(body, "m_LocalScale")
    x, y, z, w = vector(body, "m_LocalRotation")
    q = ((1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)),
         (2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)),
         (2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)))
    return tuple(tuple(q[i][j]*s[j] for j in range(3))+(p[i],)
                 for i in range(3))+((0., 0., 0., 1.),)

def transform(points, m):
    return [tuple(sum(m[i][k]*p[k] for k in range(3))+m[i][3]
                  for i in range(3)) for p in points]

def bounds(points):
    return {"min": [min(p[i] for p in points) for i in range(3)],
            "max": [max(p[i] for p in points) for i in range(3)]}

def cross2(o, a, b):
    return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])

def hull2(points):
    pts = sorted(set((round(p[0], 10), round(p[1], 10)) for p in points))
    if len(pts) < 3:
        return pts
    lo, hi = [], []
    for p in pts:
        while len(lo) >= 2 and cross2(lo[-2], lo[-1], p) <= 1e-12:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(hi) >= 2 and cross2(hi[-2], hi[-1], p) <= 1e-12:
            hi.pop()
        hi.append(p)
    return lo[:-1]+hi[:-1]

def shoelace(poly):
    return abs(sum(poly[i][0]*poly[(i+1) % len(poly)][1] -
                   poly[(i+1) % len(poly)][0]*poly[i][1]
                   for i in range(len(poly))))/2

def contained(poly, container):
    return all(all(cross2(container[i], container[(i+1) % len(container)], p)
                   >= -1e-7 for i in range(len(container))) for p in poly)

def rectangular_union(rectangles):
    """Exact coordinate-strip rectangle union, not a sum of overlapping boxes."""
    xs = sorted(set(round(x, 8) for r in rectangles for x in (r[0], r[1])))
    area = 0.
    for a, b in zip(xs, xs[1:]):
        mid = (a+b)/2
        spans = sorted((r[2], r[3]) for r in rectangles
                       if r[0] <= mid <= r[1])
        if not spans:
            continue
        start, end = spans[0]
        length = 0.
        for c, d in spans[1:]:
            if c <= end + 1e-8:
                end = max(end, d)
            else:
                length += end-start
                start, end = c, d
        area += (b-a)*(length+end-start)
    return area

def inline_vertices(body):
    n = int(re.search(r"m_VertexCount: (\d+)", body)[1])
    raw = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-fA-F]+)", body)[1])
    stride = len(raw)//n
    channels = re.search(r"m_Channels:\s*\n(.*?)    m_DataSize:", body, re.S)[1]
    first = re.search(r"- stream: (\d+)\s*\n\s*offset: (\d+)\s*\n"
                      r"\s*format: (\d+)\s*\n\s*dimension: (\d+)", channels)
    if tuple(map(int, first.groups())) != (0, 0, 0, 3):
        raise ValueError("Unsupported inline vertex format; no estimation fallback.")
    return [struct.unpack_from("<fff", raw, i*stride) for i in range(n)]

def top_boundary(mesh_body, points):
    """Recover closed top-face boundary from the actual indexed triangles."""
    raw = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-fA-F]+)", mesh_body)[1])
    fmt = "H" if field(mesh_body, "m_IndexFormat") == "0" else "I"
    indices = struct.unpack("<"+fmt*(len(raw)//struct.calcsize(fmt)), raw)
    top = max(p[1] for p in points)
    edges, area, triangles = Counter(), 0., 0
    for k in range(0, len(indices), 3):
        pts = [points[i] for i in indices[k:k+3]]
        if len(pts) != 3 or not all(abs(p[1]-top) < 1e-6 for p in pts):
            continue
        p2 = [(round(p[0], 8), round(p[2], 8)) for p in pts]
        area += shoelace(p2)
        triangles += 1
        for a, b in zip(p2, p2[1:]+p2[:1]):
            edges[tuple(sorted((a, b)))] += 1
    boundary = [e for e, count in edges.items() if count == 1]
    graph = defaultdict(list)
    for a, b in boundary:
        graph[a].append(b)
        graph[b].append(a)
    if not graph or any(len(v) != 2 for v in graph.values()):
        raise ValueError("Top mesh boundary not one simple closed polygon.")
    start, poly, prev, cur = min(graph), [], None, min(graph)
    while not poly or cur != start:
        poly.append(cur)
        nxt = next(v for v in graph[cur] if v != prev)
        prev, cur = cur, nxt
        if len(poly) > len(graph):
            raise ValueError("Invalid boundary loop.")
    return {"boundaryXZ": poly, "shoelaceAreaM2": shoelace(poly),
            "topTriangleAreaM2": area, "topTriangleCount": triangles,
            "boundaryClosed": len(poly) == len(graph)}

class Audit:
    def __init__(self, repo):
        self.repo = repo
        self.scene_bytes = (repo/SCENE).read_bytes()
        self.objects = parse_unity(self.scene_bytes.decode("utf-8-sig"))
        self.assets = {}
        for p in (repo/"Assets").rglob("*.meta"):
            m = re.search(r"^guid: (\w+)", p.read_text(
                encoding="utf-8-sig", errors="ignore"), re.M)
            if m:
                self.assets[m[1]] = Path(str(p)[:-5])

    @lru_cache(None)
    def world(self, fid):
        if not fid:
            return identity()
        body = self.objects[fid][1]
        return multiply(self.world(reference(body, "m_Father")), matrix(body))

    @lru_cache(None)
    def path(self, fid):
        if not fid:
            return ""
        body = self.objects[fid][1]
        return self.path(reference(body, "m_Father")) + "/" + field(
            self.objects[reference(body, "m_GameObject")][1], "m_Name")

    @lru_cache(None)
    def fbx(self, guid):
        p = self.assets[guid]
        text = p.read_text(encoding="utf-8")
        if not text.startswith("; FBX"):
            raise ValueError("Only explicitly supported ASCII FBX can be measured.")
        meta = Path(str(p)+".meta").read_text(encoding="utf-8-sig")
        gs = float(re.search(r"globalScale: (.+)", meta)[1])
        us = float(re.search(r'P: "UnitScaleFactor",[^\n]*?,([^,\n]+)\s*\n', text)[1])
        if gs != 1 or us != 1:
            raise ValueError("Non-unit importer scale requires a separate calibration: "+str(p))
        geometry = {}
        for m in re.finditer(r"Geometry: (\d+),.*?\{\s*Vertices: \*\d+\s*\{\s*a:\s*(.*?)\}", text, re.S):
            a = [float(v) for v in re.split(r",\s*", m[2].strip())]
            geometry[m[1]] = [(-a[i]*.01, a[i+1]*.01, a[i+2]*.01)
                              for i in range(0, len(a), 3)]
        models = {m[1]: m[2] for m in re.finditer(
            r'Model: (\d+), "Model::([^"]+)", "Mesh"', text)}
        ids = {m[2]: int(m[1]) for m in re.finditer(
            r"^    (43\d+): (.*)$", meta, re.M)}
        result = {}
        for src, target in re.findall(r'C: "OO",(\d+),(\d+)', text):
            if src in geometry and target in models and models[target] in ids:
                result[ids[models[target]]] = (geometry[src],
                    str(p.relative_to(self.repo)).replace("\\", "/"))
        return result

    def furniture(self, instance):
        body = self.objects[instance][1]
        name = re.search(r"propertyPath: m_Name\n      value: (.*)", body)[1]
        par = int(re.search(r"m_TransformParent: \{fileID: (\d+)\}", body)[1])
        guid = re.search(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", body)[1]
        pf = parse_unity(self.assets[guid].read_text(encoding="utf-8-sig"))
        mods = {(int(m[1]), m[2]): m[3] for m in re.finditer(
            r"- target: \{fileID: (-?\d+), guid: \w+, type: 3\}\n"
            r"      propertyPath: (.*?)\n      value: (.*?)\n", body)}
        ts = {}
        for fid, (cls, tb) in pf.items():
            if cls != 4:
                continue
            for (target, prop), value in mods.items():
                if target == fid and "." in prop:
                    key, axis = prop.split(".")
                    tb = re.sub(r"(^  "+key+r": \{[^\n]*?"+axis+r": )[^,}]+",
                                lambda m: m[1]+value, tb, flags=re.M)
            ts[fid] = tb
        @lru_cache(None)
        def world(fid):
            tb = ts[fid]
            father = reference(tb, "m_Father")
            return multiply(world(father) if father else self.world(par), matrix(tb))
        roots = [i for i, b in ts.items() if not reference(b, "m_Father")]
        if len(roots) != 1:
            raise ValueError("Ambiguous prefab root.")
        rt = roots[0]
        go_to_tr = {reference(tb, "m_GameObject"): i for i, tb in ts.items()}
        vertices, parts = [], []
        for fid, (cls, tb) in pf.items():
            if cls != 33:
                continue
            mr = re.search(r"m_Mesh: \{fileID: (\d+), guid: (\w+)", tb)
            source, path = self.fbx(mr[2])[int(mr[1])]
            points = transform(source, world(go_to_tr[reference(tb, "m_GameObject")]))
            vertices.extend(points)
            parts.append({"meshFileID": int(mr[1]), "source": path,
                "name": field(pf[reference(tb, "m_GameObject")][1], "m_Name"),
                "vertexBoundsWorld": bounds(points)})
        return {"instanceFileID": instance, "path": self.path(par)+"/"+name,
            "prefab": str(self.assets[guid].relative_to(self.repo)).replace("\\", "/"),
            "rootSourceTransformFileID": rt,
            "rootWorldPosition": [world(rt)[i][3] for i in range(3)],
            "rootLocalScale": vector(ts[rt], "m_LocalScale"),
            "vertexBoundsWorld": bounds(vertices),
            "projectedVertexConvexHullXZ": hull2([(p[0], p[2]) for p in vertices]),
            "parts": parts}

    def architectural(self):
        items = {}
        for name, (go, tr, mesh) in FLOOR_MESHES.items():
            points = transform(inline_vertices(self.objects[mesh][1]), self.world(tr))
            items[name] = {"gameObjectFileID": go, "transformFileID": tr,
                "meshFileID": mesh, "path": self.path(tr),
                "active": field(self.objects[go][1], "m_IsActive") == "1",
                "vertexBoundsWorld": bounds(points),
                "topSurfacePolygon": top_boundary(self.objects[mesh][1], points)}
        base = items["base"]["topSurfacePolygon"]["boundaryXZ"]
        for name in ("bath", "balcony", "kitchen"):
            items[name]["containedInBaseFloorXZ"] = contained(
                items[name]["topSurfacePolygon"]["boundaryXZ"], hull2(base))
        go_to_tr = {reference(b, "m_GameObject"): i for i, (cls, b)
                    in self.objects.items() if cls == 4 and field(b, "m_GameObject")}
        walls = []
        for fid, (cls, body) in self.objects.items():
            if cls != 33 or "guid:" in field(body, "m_Mesh"):
                continue
            go = reference(body, "m_GameObject")
            gb = self.objects[go][1]
            if not field(gb, "m_Name").startswith("Wall") or field(gb, "m_IsActive") != "1":
                continue
            tr = go_to_tr[go]
            points = transform(inline_vertices(self.objects[reference(body, "m_Mesh")][1]),
                               self.world(tr))
            hull = hull2([(p[0], p[2]) for p in points])
            bb = bounds(points)
            rect = (bb["min"][0], bb["max"][0], bb["min"][2], bb["max"][2])
            rectangle_area = (rect[1]-rect[0])*(rect[3]-rect[2])
            if abs(shoelace(hull)-rectangle_area) > 1e-5:
                raise ValueError("Wall footprint not an axis-aligned rectangle: "+self.path(tr))
            walls.append({"path": self.path(tr), "rect": rect,
                          "bottom": bb["min"][1], "top": bb["max"][1]})
        wall_area = rectangular_union([w["rect"] for w in walls])
        slice_area = rectangular_union([w["rect"] for w in walls if w["bottom"] < .1 < w["top"]])
        slab = [(2.7, -2.6), (5.8, -2.6), (5.8, 0.), (2.7, 0.)]
        return {"floorSurfaces": items,
            "floorAreaMethod": "Actual top indexed-triangle boundary + shoelace; overlays contained in base, never added.",
            "walls": {"activeMeshCount": len(walls),
                "fullHeightProjectedRectangleUnionM2": wall_area,
                "horizontalSliceY010mWallUnionM2": slice_area,
                "baseMinusFullProjectedWallsM2": items["base"]["topSurfacePolygon"]["shoelaceAreaM2"]-wall_area,
                "qualification": "Model-only projected geometry; NOT statutory saleable/GFA/net area or a compliance certification."},
            "entrancePlatform": {"slabTransformFileID": 1038451789,
                "mesh": "Unity built-in Cube 10202; identity rotation",
                "slabBoundaryXZ": slab, "slabShoelaceAreaM2": shoelace(slab),
                "outsideHouseBaseApartFromSharedEdge": True,
                "thresholdInsideBaseOrSlabUnion": True,
                "qualification": "External safety spawn platform, not extra dwelling floor area."}}

def run(repo):
    audit = Audit(repo)
    furniture = [audit.furniture(i) for i in SELECTED]
    by_id = {o["instanceFileID"]: o for o in furniture}
    rug = by_id[RUG]
    rug_thickness = rug["vertexBoundsWorld"]["max"][1]-rug["vertexBoundsWorld"]["min"][1]
    predicted_rug_top = .001+ rug_thickness
    rug_hull = rug["projectedVertexConvexHullXZ"]
    for obj in furniture:
        iid = obj["instanceFileID"]
        floor = .012 if "/Balcony/" in obj["path"] else 0.
        base = floor
        policy = "base floor"
        if iid in (SOFA, COFFEE):
            base = predicted_rug_top
            policy = "rug top; full footprint contained" if contained(
                obj["projectedVertexConvexHullXZ"], rug_hull) else (
                "partial rug overlap; maximum support height chosen. Bare-floor ends retain approximately 7.86 mm clearance.")
        bb = obj["vertexBoundsWorld"]
        delta = base+.001-bb["min"][1]
        obj["supportCorrectionPrediction"] = {"supportPolicy": policy,
            "floorY": floor, "supportHeightY": base, "bottomClearanceM": .001,
            "desiredBottomY": base+.001,
            "rootY": obj["rootWorldPosition"][1]+delta,
            "deltaFromCurrentY": delta, "predictedTopY": bb["max"][1]+delta,
            "qualification": "Candidate source geometry only; Editor mesh bounds/physics and Quest still require verification."}
    return {"scene": SCENE,
        "sceneSHA256": hashlib.sha256(audit.scene_bytes).hexdigest(),
        "readOnly": True,
        "method": "Offline ASCII FBX vertices with unit scale 1 and Y-up, Unity X mirror, prefab TRS and scene overrides. Not runtime measurement.",
        "limitations": ["Selected furniture only, excluding KitchenSet_1/3 non-unit importer and pivot remapping.",
            "Furniture XZ hull is a projection, not exact foot contact area or capsule clearance.",
            "No runtime scripts executed, Unity imported mesh/physics or Quest validation."],
        "architecture": audit.architectural(), "furniture": furniture}

if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    args = ap.parse_args()
    print(json.dumps(run(args.repo.resolve()), ensure_ascii=False, indent=2))
