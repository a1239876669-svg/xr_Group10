# Phase 3 spatial stabilization — implementation and handoff

26 October 2026. **Pre-evaluation developer inspection / owner-symptom correction**, not a participant-evaluation finding or final study version. AI assistance: Codex supported implementation, geometry analysis, tests and documentation; owner Quest acceptance remains outstanding.

## Version, scope and preservation

- Source: origin/main `2dd047056e661512c831601481d3b82519056a1c`, safely fetched before implementation; no newer remote Phase 3 change was found then.
- Isolated worktree: `D:/Documents/GitHub/xr_Group10-phase3-stabilization`; branch `phase3/stabilization-apartment-layout`.
- Local scene commit: `be9f20cd5fef102534f967ee24b552e0e4acfeac` (`fix: stabilize apartment supports, circulation and door geometry`). The subsequent verification/docs commit is identifiable in this branch's local log; this file cannot embed its own future commit SHA.
- Formal scene SHA-256: before `2294e27162eeefd988f1ad6d31ccaf004d63f2d4a40320ff472711f26086683d`; after `23a9813c3479d699846a03a277d9368b3482bd5eb5f70383c6f81dc0bc710da5`.
- The last actual Unity-tested geometry hash is `412911e221b92acd6720bd12a41eb92d4e4462c4da7529c5bf6039b05d5f936f`. Subsequent saved-scene changes only remove 11 trailing spaces on empty `m_Name` values; object-wise semantic equality was checked. `git diff --check` is clean for the scene.
- No Push, PR, Merge, Rebase or source FBX/shared-Prefab edit. Original workspaces, Phase 2 release and historical feedback remain protected. No OpenXR, Packages, input mapping, player size, bag size, schema, ceiling or lighting change.

## Implemented changes

| Batch | Explicit scene-instance change | Reason / dependency |
|---|---|---|
| Grounding | Corrected actual imported mesh bottoms of rug, sofa, coffee table, armchair, TV unit, dining table, four dining chairs, two balcony chairs, balcony table and deck chair | 14 instances, each relative to its real floor/rug support; scales, rotations and existing chair-child X/Z overrides retained; ~1mm prototype offset, not a construction standard |
| Carpet | Disabled only `CarpetGroundSupport` BoxCollider 140617042; base Floor 1448635157 remains enabled and continuous | Removes ~.12586m raised walking support; rug visual top is now ~.00686m; no global collision-layer change |
| Living layout | Armchair (+1.05X,-.50Z); sofa (+.18X,-.75Z); coffee table (+.52X,-.75Z); rug -.12Z | Coordinated arrangement opens the west/north path and retains sofa/coffee fully over the rug; no furniture shrinking |
| Balcony | Left chair and table +.35Z; left plant +2.15X; right chair +.10X | Avoids transferring furniture collisions to the north reveal/planter; final real mesh table/right-chair X gap ~.06911m and offline intersecting triangle-pair candidates 40 → 0 |
| Entrance | Opening X=3.9..4.9; coordinated Wall_S4/5, existing leaf/hinge/solid/aim trigger; flush threshold | Fully-open actual solid clearance .638735 → .974500m; threshold .060 → .000m; original exterior platform unchanged; leaf sits on the outside face and swings outward |
| Bath | Coordinated Wall_LC3a/b and Tile_E1/E2 opening Z=3.13..4.05; existing leaf/hinge/solid/aim trigger | Finished opening .682450 → .875000m fully open; -90 degree inward swing; incomplete sanitary layout remains out of scope |
| Bedroom 2 | Existing door open angle +90 → -90 degrees | Swing into bedroom instead of living circulation; existing state, blocker and recovery code retained |
| Contact coverage | Eight existing-style explicit `BagContactTarget` components on modified local walls/tiles/leaves | Collider and Renderer paired locally; original warning material reused; no whole-scene auto-registration |

All 6,985 original serialized scene objects remain; eight contact components were added, giving 6,993 documents. Stable original component IDs and references are retained. The 178-row [before/after CSV](Spatial-Before-After.csv) records individual dimensions, positions, angles, supports, door widths and area calculations.

### Selected measurements and remaining bottlenecks

- Closed floor polygon 72.000002850m², balcony 5.435999916m² already included, external platform 8.06m² excluded. At Y=.10m, wall union 10.418225646 → 10.328625678m²; geometric floor-minus-wall 61.581777204 → 61.671377172m². These are not certified saleable/net areas.
- Sofa west axis gap ~.7514 → .931429m. Sofa north to near-left dining chair gap ~.0195 → .769407m. Those axis gaps are not a full corridor-width certificate.
- Intended core passage goes west/north of sofa. Coffee-table/TV gap (~.19m) is not a designated route. The .90m full-route design aim remains **partial/unproven**; .60m-diameter query routes do not establish general accessibility or comfort.
- Rejected unsaved candidates: moving only east pinched the TV-side route; moving dining table/selected chairs caused real furniture interference; pushing balcony items north hit the planter/window reveal. The adopted coordinated positions avoid those observed transfers. This does not prove that every possible use, head height, chair pull-out or carrying posture is clear.
- No bedroom bed/wardrobe or full toilet/shower furnishing was added. Do not claim whole-home usability or building/fire/accessibility compliance.

## VR and data invariants

RoomMap, G00–G11 goal references, balcony slider, cabinet interactions, menu, default English, Grip/Rearm, safe recovery and core runtime scripts remain unchanged. Drop zone remains (2.1,.012,5.5), size 1.3×1×.9m; safe reset remains (4.25,0,-1.55). Review scene key stays `2129619AB63EE0D1`.

BagContactTarget IDs added: 331741836, 470823414, 527504765, 846168701, 962122316, 1030523190, 1373652530, 1747743422. Main Floor, moved object colliders and renderers follow their retained hierarchy. An unregistered object's lack of red is not proof of no contact.

S/M/L remain .30×.30×.15, .45×.40×.22 and .60×.48×.30m. Held bags still sense rather than rigidly prevent travel; red overlays and holding-hand haptics are unchanged. New-layout S/M/L task results are **Not Run**.

Two original historical JSON byte streams were backed up and their hashes rechecked unchanged. Never silently migrate old world-coordinate markers. Raw backups and potentially identifiable feedback remain under ignored local Logs, excluded from commits. Phase 2 EXE/ZIP hashes still match the saved manifest; no new Player build was produced.

An Editor-only, explicit opt-in storage adapter protects development runs. Before Play select **Apartment Phase 3 > Review Test Storage > Use New Isolated Development Folder**, then confirm the pre-Start isolation log/path. Repeated Play in the same Editor reuses that temporary folder; a restarted Editor requires a new selection. No file is deleted. Actual Play timing still requires the first manual smoke check; Windows Player is not covered by this adapter. See [baseline notes](Evaluation-Baseline-Notes.md).

## Files authored

- Modified only existing runtime asset: `Assets/Scenes/Apartment_Detailed_VR.unity`.
- New Editor-only helpers (+matching .meta): `Assets/Scripts/Editor/Phase3SpatialStabilization.cs`, `Phase3SpatialDoorAndRoute.cs`, `Phase3SpatialLayout.cs`, `Phase3SpatialRouteChecks.cs`, `Phase3SpatialVerification.cs`, `Phase3ReviewTestStorage.cs`. Geometry mutations require explicit calls and guarded expected values; importing these helpers does not reapply scene edits automatically.
- New tests (+matching .meta): `Assets/GuidedJourney/Tests/SpatialStabilizationTests.cs` (7 groups) and `Phase3ReviewStorageTests.cs` (7 cases).
- Read-only source geometry tool: `Tools/Phase3/geometry_check.py` (Python standard library; outputs JSON to stdout; no dependency installation).
- Five handoff documents, measured SVG/PNG plan and selected machine-verifiable results / actual Unity before-and-after renders under `Docs/Phase3/`.

## Actually executed verification

| Evidence | Result | What it does not establish |
|---|---|---|
| Unity 6000.6.4f1, DX11 import / C# | Final run has zero CS compile errors | Not a runtime/session stability guarantee |
| Full EditMode suite on final geometry | **186 passed, 0 failed, 0 skipped**; original 172 + 7 spatial + 7 storage | Not headset or Player acceptance |
| Guided Journey state tests | 47/47 included in 186 | Not physical G00–G11 task completion |
| Imported mesh / spatial groups | 7/7, with actual meshes and Physics | Not all room ergonomics |
| Existing door API fixtures | Ten doors open, close, blocker pause and fresh-trigger resume via real methods | Automated fixture disables tracked-head protection, so owner standing position/safety remains to verify |
| Physics core routes | Exact entrance→living, living→kitchen, living→balcony-front endpoints reached | OverlapCapsule path search, not CharacterController.Move / Quest / whole-route .90m certificate |
| Route method | radius .30m, height1.8m, foot lift .025m, step .20m; grid .05m and edge samples <=.025m; diagonal corner cutting prohibited; floor rays checked | Assumed body, height and step profile only; no bag sweep or comfort guarantee |
| Scene references / invariants | Existing documents, door references, anchors, size configurations and explicit contact references retained | Actual menu/measurement/marker/save flow still needs Play |
| Camera images | Four actual Unity scene renders saved; inspected for layout and missing/pink materials | Not Quest stereoscopic views or comfort evidence |
| Historical data / release artifacts | Original two JSONs and Phase 2 artifact hashes unchanged | Not new-layout persistence acceptance |

Test XML timestamps are raw UTC (25 October); local execution date is 26 October in Asia/Shanghai. Exact logs, baseline bytes and unsuccessful tooling attempts remain under ignored `Logs/Phase3Stabilization-20261026/`. Final XML, summary, route JSON and images are copied into [Verification](Verification/README.md).

### Unresolved exceptions and automatic side effects

The final Unity process exited 0 and tests passed, but Editor shutdown still logged **TMP_FontAsset.m_AtlasTextures UnassignedReferenceException**, also observed in the pre-edit baseline probe. Existing CS0252, UAC0005 and old DynamicsManager serialization-version warnings remain. They are not suppressed, fixed or called a new XR crash. Earlier helper compilation errors were corrected; failed-attempt logs are retained.

Unity also changed seven unrelated tracked files: Chinese font asset, four Performance JSON/.meta files (deleted by Test Runner), ProBuilder and Code Coverage settings. Original and surviving after-bytes are backed up and hashes recorded. They are **excluded from local commits**. Exact-byte restoration has been separately requested because the instruction prohibits silently discarding automatic changes; until an approval/result is recorded, do not consider the working tree clean. OpenXR and Packages were not changed.

## Owner handoff and next gate

Open this isolated worktree in Unity Hub with 6000.6.4f1. Select isolated development storage, verify its actual path, then execute the [28-item Quest sheet](Quest3-Spatial-Stabilization-Acceptance.csv). Begin no-bag routes in both directions, without using Safe Reset to pass a blocked route. Test virtual door blocking with a controller at a safe physical standing position; never deliberately walk into real or virtual walls. Then test bags, G00–G11, review/save/reopen, language/focus and second Play. Keep all initial results Not Run until observed.

**Not frozen for formal participant evaluation yet.** If reasonable space still traps movement, capture a safe reproduction and request a separate movement-recovery patch rather than shrinking the player/disabling wall protection. After Quest acceptance, decide Draft PR and version freeze. Formal participant research, coding, prioritisation and evidence-driven refinement remain separate subsequent work.

Course/source PDF hashes and authoritative rubric/application limits are in [Evaluation baseline notes](Evaluation-Baseline-Notes.md) and [design acceptance](Spatial-Design-Acceptance.md). Group rubric 3/2/3, individual 2/1/1; local Canvas due date not live-reverified. None of this work is labelled non-developer evaluation evidence.
