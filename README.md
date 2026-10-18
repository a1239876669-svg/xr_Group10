# COMP5424 Group 10 - Virtual Apartment Design Review

**Scenario C: Virtual Apartment Design Review.** Our Phase 2 VR prototype helps resident advisers and other non-technical reviewers discuss apartment scale, circulation and everyday use before interior decisions become expensive to change. It supports architectural design review rather than property sales. The central task is carrying shopping along **Entrance -> Living Room -> Kitchen**, then recording a located issue for the design team.

The apartment is representative and simplified: no client BIM/CAD was supplied. Walls/structure are fixed; selected doors and cabinets are operable. Dimensions are stated model assumptions, not construction information or certified clearance. Alternative interior layouts and direct furniture redesign remain deferred.

## Team and responsibilities

| Member | Student ID | Recorded GitHub account | Main contribution area |
|---|---|---|---|
| Guo Jikang | 26125103g | YuzYue | Measurement, markers, feedback and data |
| Fan Zujian | 26124914g | fanfanzujian-oss | Detailed apartment UI, onboarding and interaction foundation |
| Yang Jiale | 26042226g | YJL-196 | Movement comfort, settings and fonts |
| Zhuang Kaiyu | 26109544g | KaiZ755 | Apartment graybox/layout, shopping bag, later journey/UI integration and validation coordination |
| Xu Boyan | 26052354g | a1239876669-svg | Repository/project setup, team integration and formal-scene assembly |

These are collaborative areas, not claims of sole authorship of every imported file. The original `YuzYue` label is preserved; measurement commits currently link to **YuzYue7**, a different account. Guo should confirm the correct account before submission. See [attributable contribution evidence](Docs/Phase2/Contribution_Evidence.md), including original, imported and cherry-picked work. SDKs and third-party models are credited separately.

## Version, platform and release status

- Integrated feature source: [`d0f512eb453a19d47352dccf2dc4979bd094a90d`](https://github.com/a1239876669-svg/xr_Group10/commit/d0f512eb453a19d47352dccf2dc4979bd094a90d), on [`merge-apartmentguidence`](https://github.com/a1239876669-svg/xr_Group10/tree/merge-apartmentguidence).
- Guided Journey [PR #10](https://github.com/a1239876669-svg/xr_Group10/pull/10), OpenXR reference repair [PR #11](https://github.com/a1239876669-svg/xr_Group10/pull/11) and UI/session refinement [PR #12](https://github.com/a1239876669-svg/xr_Group10/pull/12) are merged through normal Merge Commits. This is not a release announcement or a claim that `main` contains these features.
- **Build Generated:** Windows x86-64 executable and complete ZIP from `d0f512e`; archive integrity verified. **Actual EXE launch and every Windows Player / Quest Link / PQ604A/B/C acceptance item: NOT RUN / NOT VERIFIED.** Publication/download link remains Pending. See the [verification summary](Docs/Phase2/Verification_Summary.md).
- Formal entry scene: **`Assets/Scenes/Apartment_Detailed_VR.unity`**. Development test scenes are not the submitted experience.

| Dependency / environment | Actual project setting |
|---|---|
| Unity Editor | **6000.6.4f1** |
| Target | Windows x86-64 PCVR, DirectX 11 |
| Headset / connection | Meta Quest 3 through Meta Quest Link |
| OpenXR runtime | Meta Quest Link runtime; confirm on the test PC |
| Meta XR Core / Interaction / Interaction OVR | **205.0.0** |
| Unity OpenXR / XR Plug-in Management | **1.18.0 / 4.7.0** |
| Unity Input System | **1.20.0** |
| Render Pipeline Core / Shader Graph | **17.6.0 / 17.6.0**, resolved by Unity |
| Intended classroom platform | PQ604A/B/C; actual executable compatibility **NOT VERIFIED** |

Versions are recorded in `ProjectSettings/ProjectVersion.txt` and `Packages/manifest.json` / `packages-lock.json`. Package import, a successful build and actual runtime acceptance are separate checks. Android APK delivery is outside the current target.

## Start here: teachers and first-time reviewers

Use the [one-page participant guide](Docs/Phase2/Participant_Quick_Start.md). A facilitator should prepare Quest Link, headset fit and a clear physical safety boundary. The review itself provides English/Chinese guidance and Help. Stop if uncomfortable; software collisions are not physical protection.

### Open and run in Unity

```powershell
git clone --branch merge-apartmentguidence --single-branch https://github.com/a1239876669-svg/xr_Group10.git xr_Group10
git -C xr_Group10 rev-parse HEAD
```

1. In Unity Hub choose **Add project from disk**, select the cloned root and use **6000.6.4f1**. Open one Editor per project folder. If the checkout contains Git LFS pointer files, obtain their real contents with Git LFS before importing.
2. Allow package resolution and import to finish. Inspect **Window > General > Console** for blocking errors. Preserve diagnostics rather than applying Meta/OpenXR **Fix All**, upgrading packages or deleting caches.
3. Connect Quest 3 to Meta Quest Link and enter Link in the headset. Confirm the intended Meta OpenXR runtime and working tracking. A large desktop mirror alone is not immersive VR.
4. Open **only** `Assets/Scenes/Apartment_Detailed_VR.unity` and press Play manually. The scene already contains the rig, bag and registered furniture; do not add another rig or load `ShoppingBag_Test` / `ShoppingBag_UI_Test` additively.
5. Each new Play begins in **English**. Change **B > Settings > Language** to **简体中文** if desired; the current menu and tutorial update immediately. Closing/reopening the menu retains this session's choice. A new Play returns to English without deleting review data.

### Run the Windows executable

**Build Generated, not yet launched or accepted in Windows Player.** The actual output is `Group10_Phase2_d0f512e/Group10_Phase2.exe`, built with Unity 6000.6.4f1 / DX11 from the integrated source above. The complete package is **`Group10_Phase2_WindowsBuild.zip`**, **129,428,303 bytes**; its SHA-256 is:

```text
870f3f25564fda1b096cc6f65e013066ba86972b893d62a30841e8d58418d2aa
```

CRC, full extraction and SHA comparison of all **249 runtime/package files** passed. See the [build manifest](Docs/Phase2/Build_Manifest.json). The owner holds the local ZIP under `D:/Documents/COMP5424/Phase2Deliverables/`; upload/download publication remains Pending. Build output is deliberately outside Git source history.

Extract the **whole ZIP** into a writable directory. Keep the EXE, `_Data`, `UnityPlayer.dll`, plugins and every supplied dependency together. Start Quest Link, enter Link, then launch **`Group10_Phase2.exe`** from the extracted build directory. Do not run an EXE copied away from its runtime files.

The owner confirmed that this EXE **has not yet been run**. Actual immersive view, tracking, controller behavior, English/Chinese font readability, complete journey, save/Exit/restart and PQ604A/B/C compatibility are **NOT RUN / NOT VERIFIED**. A successful Editor session or complete ZIP does not fill these acceptance items.

## Controls and recovery

| Control | Action |
|---|---|
| Left thumbstick | Smooth movement when the selected mode permits it |
| Right thumbstick | Smooth/snap turning; up/down adjusts a pointed comfort setting while its menu owns input |
| Right **A** | Hold to aim teleport; release at a valid green landing when teleport is enabled |
| Right **B** | Open/close the menu and follow modal confirmation prompts |
| Right ray + front index **Trigger** | Select menu items, operate nearby doors/cabinets, or place measurement points/markers according to the active tool |
| Left or right side **Grip** | Near the bag handle, hold Grip to pick up and carry; release to put down. One holding hand at a time |
| **My Items** | Choose Small/Medium/Large only while the bag is released |

New grabs are blocked by menu/tutorial restrictions, teleport aiming, unavailable tracking or input focus, and text-editing restrictions. An existing holder can release. After an interruption, fully release Grip and return controls to neutral before pressing again; keeping Grip held must not cause automatic pickup.

- **Unfinished measurement:** B > Measurement > **Cancel start point**.
- **Mistaken current measurement:** **Undo last display** hides the latest current display; **Hide measurements** hides current measurement displays. These do not erase historical saved measurements.
- **Mistaken new marker:** **Cancel draft** removes only the eligible current-run draft. **Hide marker** hides a saved marker's presentation while preserving feedback and links.
- **Stuck virtual position:** B > Settings > **Teleport to Safe Position**, confirm, release controls, then resume. Remain within the physical Quest boundary.
- **Help:** B > Controls > **Review guide**. You may skip practice or explore freely; skipped tasks remain distinguished from completed tasks.

## Guided review journey and implemented features

| Stage | What the reviewer does |
|---|---|
| G00 | Read task/model/safety guidance and practise movement/menu controls |
| G01-G02 | Open the entrance door, enter and explore the living room |
| G03-G04 | Operate the balcony sliding door; open and close the designated kitchen cabinet |
| G05 | Return to the entrance |
| G06 | Choose a bag size in My Items, close the menu and near-grab the handle |
| G07 | Carry shopping through the living room to the kitchen |
| G08 | Release the bag into the **outlined kitchen floor area**; this is not cupboard storage or a bag-fit certification |
| G09 | Make a real two-point measurement; cancel/retry or explicitly skip through Help |
| G10 | Place an issue marker, choose feedback aspect/rating and importance, optionally link a measurement, then **Save feedback** |
| G11 | Explore freely, add more review records or exit through the menu confirmation |

Small/Medium/Large body dimensions in metres (width x height x depth) are **0.30 x 0.30 x 0.15**, **0.45 x 0.40 x 0.22**, and **0.60 x 0.48 x 0.30**. Geometry and contact volumes resize together; resizing is locked while held. Compare sizes on the same route. Contact with registered furniture gives red visual feedback and a short pulse in the **holding** controller; appearance recovers when contact ends. Warnings communicate contact instead of universally blocking the user's movement.

A compact tool hint retains current instructions while measuring or marking. The readout shows current/last visible measurement or marker context; **Stop tool** restores the full task card. Existing furniture hover feedback is preserved beneath bag contact feedback. Measurement and marking share the established right ray/Trigger according to tool state.

## Feedback, persistence and session display

The current form uses feedback **aspects/ratings** and **Low/Medium/High importance**, with location/object context and optional measurement links. This reduces keyboard dependence but limits detailed accounts of everyday impact. Existing unrecognised participant text is preserved; choosing **Use preset feedback** explicitly replaces it. Voice/free-text entry is not claimed as a validated new-record workflow.

Records use version-2 scene-specific JSON containing IDs, room, coordinates, timestamps and linked fields. `ReviewTools.SavePath` resolves under `Application.persistentDataPath/ReviewSessions`. For the formal scene, the current file is `2129619AB63EE0D1_current.json`; on this Windows configuration:

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Quest Guidence\ReviewSessions
```

`ReviewStorage` uses temporary-file replacement, a last-complete `.bak` and validated recovery. Read the result of **Save feedback**; on an error, retain the record and retry rather than assuming a success message or Exit proves persistence.

**Saved data persists, but previous markers and measurement visuals are hidden by default on a new run.** Hidden objects should not reappear as a wall of historical labels or complete new tutorial tasks. Reopening data is distinct from displaying all historic objects. Clones can share this persistent-data directory: back it up before testing; do not delete it to obtain a clean screen. Comfort preferences are separate from the per-run display language. Export exists as a code capability; an accessible export-menu/Player workflow is not claimed verified.

## Verification, integration and remaining limits

| Evidence | Actual scope / status |
|---|---|
| Historical ShoppingBag A/B/C1 | 78/78 EditMode including the earlier 26 A/B, plus 2/2 simulated PlayMode; isolated Quest checks reported Pass (five grabs/releases per hand, four C1 and eight Phase B checks) |
| PR #12 integrated automated tests | Actual Unity DX11 EditMode **172/172**, 0 failed/0 skipped on `8848bf5`; includes Journey, safety, language, Session/readouts and recovery |
| Fresh final-source automation | Actual clean `d0f512e` Unity DX11 EditMode **172/172**, 0 failed/0 skipped, 18 Oct 2026 14:36:58-14:36:59 UTC; detailed evidence in the verification summary |
| Current PR #12 headset candidate | **User-reported Pass** on `f3b2507`: language/new-Play English, UI/tutorial, G00-G11, bag, measurement, Marker, Session display, save/reopen, safe teleport and second Play. Exact execution timestamps/screenshots were not provided |
| Windows build / complete ZIP | **Build Generated:** Unity reports Success, exit 0; complete archive CRC/extraction/all-file SHA verification passed; [manifest](Docs/Phase2/Build_Manifest.json) |
| Actual Windows EXE / Quest Link / course PCs | **NOT RUN / NOT VERIFIED**; owner has not launched this EXE. Includes font readability, journey, persistence, and PQ604A/B/C |
| Representative-user study | **Not conducted**; no claim of independent novice success, improved understanding, reduced sickness or accessibility compliance |

PR #11's committed single-asset OpenXR repair preserves approved feature states and removes invalid references. Its SHA-256 is `1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff`; do not replace it with an older local configuration. This does not prove the root cause of historical `UnityOpenXR.dll` / `XR_ERROR_LIMIT_REACHED` crashes is solved. Report persistent grey view, tracking/session failure or a warning flood and retain logs.

Known automated-run diagnostics include **CS0252**, **UAC0005**, and a TMP **`m_AtlasTextures` exception during pre-build processing and Editor shutdown**. The build still reports Success; English/Chinese glyph rendering and task/readout visibility in the actual Player are specifically **NOT VERIFIED**. Tests passed, but this is not a claim of an exception-free Editor. Scene/collider calibration, stereo readability, comfort, feedback depth and Windows persistence remain evaluation concerns. Smooth/teleport choice, ramps, vignette, presets and safe recovery are available; their effectiveness needs representative testing. No full mobility-aid simulation, seated-accessibility guarantee, alternative-layout comparison or free apartment editing is claimed.

## Documentation and repository structure

| Location | Content |
|---|---|
| `Assets/Scenes/Apartment_Detailed_VR.unity` | Formal review scene |
| `Assets/ShoppingBag/` | Reusable bag, profiles, feedback, safety and module tests |
| `Assets/GuidedJourney/` / `Assets/Scripts/GuidedJourney/` | Pure journey state and scene adapter / display |
| `Assets/Scripts/Review/` / `Assets/Scripts/Localization/` | Located records, persistence and display language |
| `Docs/Phase2/` | Current delivery documents and evidence summaries |
| `Docs/UISessionRefinement/` | PR #12 implementation notes and historical automated evidence |

- [Design and technical note](Docs/Phase2/Phase2_Prototype_Design_and_Technical_Note.md), with [DOCX](Docs/Phase2/Phase2_Prototype_Design_and_Technical_Note.docx) / [PDF](Docs/Phase2/Phase2_Prototype_Design_and_Technical_Note.pdf) delivery copies.
- [Requirements R1-R12 and source mapping](Docs/Phase2/Requirements_Traceability.md), [weekly activity record](Docs/Phase2/Weekly_Activity_Record.md), [verification summary](Docs/Phase2/Verification_Summary.md), [submission checklist](Docs/Phase2/Submission_Checklist.md).
- [Participant quick start](Docs/Phase2/Participant_Quick_Start.md), [contribution evidence](Docs/Phase2/Contribution_Evidence.md), [external resources and licences](Docs/Phase2/External_Resources_and_Licences.md).
- [Geometry assumptions](Docs/ApartmentGeometryAssumptions.md), [detailed scene setup](Docs/ApartmentDetailedVR.md), [comfort design](Docs/ApartmentComfortSettings.md); bag [A](Assets/ShoppingBag/Documentation/PhaseA.md), [B](Assets/ShoppingBag/Documentation/PhaseB.md), [C1](Assets/ShoppingBag/Documentation/PhaseC1.md).

The original bag commits [470468d](https://github.com/a1239876669-svg/xr_Group10/commit/470468d), [5769964](https://github.com/a1239876669-svg/xr_Group10/commit/5769964) and [40193be](https://github.com/a1239876669-svg/xr_Group10/commit/40193be), and UI bridge commits [b8cbf42](https://github.com/a1239876669-svg/xr_Group10/commit/b8cbf42) / [9e0987e](https://github.com/a1239876669-svg/xr_Group10/commit/9e0987e), remain traceable; some integrated copies preserve authors through cherry-picks/imports. Open module PRs are historical evidence, not proof that a module is absent from the integrated source.

Meta XR/OpenXR, Unity/TMP, Noto Sans SC, Furniture Mega Pack and Real Materials are external resources. Existing [Noto](Assets/Apartment/Fonts/NotoSansSC-LICENSE.txt) and [Liberation Sans](Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt) notices do not license unrelated assets. The asset owners must confirm remaining model/material sources and redistribution permissions in the register. Codex-assisted coding/testing/documentation is distinguished from SDK, teammate and personal original work; follow the course's assistance-declaration policy. Final Canvas submission and release approval remain the team's responsibility.
