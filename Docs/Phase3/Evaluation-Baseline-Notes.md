# Evaluation baseline notes

Prepared 26 October 2026 for pre-evaluation scene stabilization.

## Version and evidence boundary

Source baseline: published Phase 2 main 2dd047056e661512c831601481d3b82519056a1c. Isolated worktree: D:/Documents/GitHub/xr_Group10-phase3-stabilization, branch phase3/stabilization-apartment-layout. Formal scene: Assets/Scenes/Apartment_Detailed_VR.unity, Unity 6000.6.4f1. Record the final local stabilization commit and build identity before evaluation; the old SHA does not identify a subsequently modified scene.

This batch addresses developer inspection and owner-reported symptoms before participant evaluation. It is not an improvement inferred from non-developer participant research. Separate serialized facts, offline geometry, Unity execution, user-reported Quest results and Windows Player results. Historical Phase 2 172/172 tests do not establish a pass for this batch.

Targets, sources, tolerances and applicability are in [Spatial design acceptance](Spatial-Design-Acceptance.md). Before/after records and the change log must identify actual changes and remaining limits.

## Authoritative course requirements

Original files are in D:/Documents/COMP5424 XR/.

| File | Relevant pages and requirements | SHA-256 |
|---|---|---|
| COMP5424-Group-Project-Assessment-Rubric-P3-202627A.pdf | p2 group: evaluation design/conduct 3, analysis/evidence 2, prioritisation/integrated outcome 3. p3 individual: repository-evidenced technical refinement 2, evaluation contribution 1, critical reflection 1. | 696251ce514da93b229dae95caa94b0931c21f51325ab75a6fea21ba609ee60c |
| COMP5424-Group-Project-Description-202627A.pdf | pp8-9: focused tasks, moderator/observation/interview instruments; normally at least five non-developer participants; consent, anonymity and minimal data; systematic analysis and contradictory evidence; prioritised findings-to-requirements backlog; substantive technical/interaction refinement, before/after and regression checks. Report body at most six A4 pages; individual essay at most 750 words. | 43ae4d87559ac5b3f3e45022df39361a0f720416c3ab8f38450c6b057cacb99b |
| COMP5424-Scenario-Description-Virtual_Apartment_Design-202627A.pdf | p2: approximately 72 square metres, no area convention defined. p3: no BIM/CAD supplied, fixed envelope/structural core, selected interiors reviewable, deviations disclosed; design discussion rather than compliance certification. | e9f615f884e4c816d918e76b703c516b33c08e1ea3b9cf34dd7f091eab6b5067 |

Saved Phase3_Group Component.png and Phase3_Individual Component.png show 22 November 2026 at 23:59. Group submission: improved executable ZIP for PQ604A/B/C, accessible repository and single PDF report, at most six A4 body pages, 2.5 cm margins and Times New Roman at least 12 pt. Individual: Canvas Text Entry, at most 750 words. Later live Canvas announcements have not been checked.

The 20 October automated classroom summary suggests Week 8 stabilization/preparation, Week 9 evaluation and Weeks 10-11 analysis/refinement. This is scheduling guidance, not a substitute for official instructions. Its ambiguous 6-8-page wording does not override the six-page limit. Week 8 developer fixes remain distinct from later participant-evidence-driven refinements.

## Area and scope

The model has an approximate outer footprint of 72 m², including wall footprints and the balcony. This is a modelling assumption, not a certified saleable or net internal area.

Recompute closed boundaries and the planar wall union after local edits. Keep the outer perimeter and metre scale. Report balcony, external safety platform, wall section and floor reference separately. Do not enlarge the apartment to produce 72 square metres of internal clear floor. No complete bedroom furnishing, sanitary-fixture use, architectural/fire/accessibility compliance or universal comfort claim is made.

## Historical review data

ReviewTools resolves storage under Application.persistentDataPath/ReviewSessions; clones can share that directory. Original files on this machine:

- C:/Users/Kai/AppData/LocalLow/DefaultCompany/Quest Guidence/ReviewSessions/2129619AB63EE0D1_current.json
- The adjacent 2129619AB63EE0D1_current.json.bak.

Both byte streams are backed up under Logs/Phase3Stabilization-20261026/HistoricalReviewData/. The adjacent historical-data-hashes.json records original paths and SHA-256 baselines. These logs and raw feedback are local protection evidence, not material to publish. Source and backup hashes matched when these notes were created; repeat the check at handoff.

Do not delete, migrate, rewrite or renumber historical records, change their schema, or clear persistentDataPath. Old world-coordinate markers may no longer identify the same object after furniture moves. Preserve their original scene/source context and disclose that limitation rather than moving them silently.

For Editor development tests, use Apartment Phase 3 > Review Test Storage > Use New Isolated Development Folder before Play. Phase3ReviewTestStorage is an Editor-only adapter that invokes the existing non-serialized ConfigureStorageForTests seam before ReviewTools.Start. It creates a new Group10-Phase3Review-<GUID> child of the system temporary directory without copying old feedback. Repeated Play runs in the same Editor reuse that directory, allowing development save/reopen checks. Show Current Development Folder reports the exact current.json path. Selecting Use New again creates another dataset and preserves the old one; Forget Folder removes the selection without deleting any data.

The selection uses SessionState: it survives assembly reload but is cleared when the Editor exits. Select a new development folder after restarting Unity. With no valid selection and an existing shared ReviewSessions directory, formal-scene Play is cancelled. A late formal-scene load/setup failure disables its ReviewTools objects and stops Play before Start. Scene Reload must remain enabled; the adapter does not change ProjectSettings. A script assembly reload during formal-scene Play stops that development run before the non-serialized override could be lost. Temporary data and the SessionState selection remain for the next Play. These fail-closed paths need actual Editor validation before hardware use.

The adapter is excluded from Windows Players and does not isolate a standalone EXE. Use a separate operating-system user/profile or an explicitly approved Player data-isolation plan before any Player test that could write the shared historical location. Developer test IDs/data and consented participant IDs/data remain separate. Do not upload identifiable raw feedback publicly.

Lifecycle basis: Unity 6000.0 documents sceneLoaded after OnEnable and before Start, BeforeSceneLoad before Awake (also in Editor Play), and SessionState clearing when Unity exits:
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SessionState.html

Adapter smoke acceptance: (1) with no selection and protected historical data present, formal Play must stop without changing either historical hash; (2) select a new private temp folder and confirm the before-Start log and actual SavePath; (3) save development feedback and reopen in a second Play, verifying the same temp path/data; (4) confirm a fresh Editor requires opt-in again; (5) repeat original/backup hashes. EditMode logic tests must remain unaffected. Static implementation and documented callback order alone are not a Play-mode PASS.

## Evaluation freeze and next steps

Freeze the evaluation candidate only after geometry/reference checks, applicable automated tests and owner Quest acceptance. Record commit, scene hash, build hash, hardware/runtime and supported controls. New-layout hardware checks start as Not Run in [the acceptance sheet](Quest3-Spatial-Stabilization-Acceptance.csv); do not carry Phase 2 passes forward.

Pilot the complete journey without using safe reset to bypass normal travel. Evaluate normally at least five non-developers with consistent tasks and low-risk procedures. Record assistance, confusion, comfort, bag contacts and feedback usefulness, including contradictory cases. Retain consent, anonymised notes, coding, findings, priority rationale and change/verification links. If layout changes during evaluation, record affected participant/version assignments and do not pool conditions silently.

Retain Phase 2 runtime directory D:/Documents/COMP5424 XR/Phase2Deliverables/WindowsBuild/Group10_Phase2_d0f512e/ and ZIP D:/Documents/COMP5424 XR/Phase2Deliverables/Group10_Phase2_WindowsBuild.zip. They are historical deliverables, not new-layout evidence. The retained Phase 2 record says Build Generated, with Player acceptance unverified. Any authorised Phase 3 build needs a new output directory and its own runtime verification.
