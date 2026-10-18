# Phase 2 Prototype Design and Technical Note

Scenario C supports non-technical resident advisers reviewing a simplified apartment for Cedar & Stone Architects and Riverside Community Housing. Our core question is whether carrying shopping from the entrance through the living room to the kitchen exposes circulation or furniture issues. This integrated prototype supports discussion and located feedback; assumed dimensions are not construction or compliance evidence.

## Requirements and implemented scope

Phase 1 IDs and priorities are retained. F/P/N mean Fully/Partially/Not Implemented within prototype scope, not proven usability. M = Must, S = Should, C = Could. Q = owner-reported Quest acceptance; A = automated/source evidence. Source keys below identify actual files.

| ID / P | Integrated feature and remaining scope | Status |
| --- | --- | --- |
| R1 M | Complete design-review journey: G00-G11, feedback save and Exit [J,R]. | F; A/Q |
| R2 M | Scale, movement, circulation and reach: metre scene, bag volumes and measurement [S,B,R]. | F; A/Q |
| R3 M | Everyday task and decision: carry shopping and identify circulation/furniture issues [J,B]. | F; A/Q |
| R4 M | Located feedback: marker position, room/object path, measurement link and storage [R]. | F; A/Q |
| R5 M | Explain experience and impact: aspect/rating/importance exist; specific narrative remains limited [R,L]. | P; A/Q |
| R6 M | Self-guided completion: task cards, Help, distinct skip and recovery; novice study pending [J,U]. | F; A/Q |
| R7 M | Simple interaction: B menu, Trigger, Grip, clear bilingual labels and rearm [U,B,L]. | F; A/Q |
| R8 M | Comfort/access: movement options, presets, vignette, safe reset and Exit; no universal claim [M]. | F; A/Q |
| R9 M | Model disclosure: fixed structure, operable elements, simplified sizes and limitations [J,L]. | F; A/Q |
| R10 S | Relative importance: Low/Medium/High retained with feedback [R]. | F; A/Q |
| R11 C | Alternative apartment/interior options: deliberately deferred; bag-size comparison is distinct. | N |
| R12 C | Direct apartment editing: deliberately deferred; door operation is not redesign. | N |


<!-- Page break -->

## User journey and XR interaction design

The route progresses from instruction to a real daily activity and a review record. Transitions depend on scene observations and new actions, rather than elapsed time or a prefilled success flag. Help may explicitly skip a stage; skipped and completed outcomes remain distinct.

| Stages | Required action and evidence |
| --- | --- |
| G00-G03 | Learn movement/menu; enter through the entrance; explore living room; open the real balcony sliding door. |
| G04-G06 | Open and close the kitchen cabinet; return to the entrance; select S/M/L while released, then actually Grip the bag. |
| G07-G08 | Carry via the living room into the kitchen; release and settle inside the marked kitchen floor area. This does not implement cabinet storage. |
| G09-G11 | Create a fresh two-point measurement; create a marker and save feedback; enter Free Exploration. Exit uses the menu confirmation. |

### Interaction rationale

Three standard bags (0.30 x 0.30 x 0.15, 0.45 x 0.40 x 0.22 and 0.60 x 0.48 x 0.30 m) create repeatable comparisons on one route. Inspector profiles change both geometry and collider; outcomes follow actual contact, not scripted size-based success. Near Grip, release-to-drop and single ownership keep the task physically understandable. Resizing while held is rejected.

Red feedback on registered furniture and a short pulse in the holding controller communicate an issue without universally stopping locomotion. This supports comparison and recovery while avoiding a false physical safety guarantee. Explicit collider/renderer registration limits warnings to relevant objects. Controller feedback, contact cleanup and movement remain separate responsibilities.

B opens the existing menu; right ray/Trigger selects controls, doors, measurement points or markers according to mode. Left stick moves, right stick turns; hold/release right A aims/executes valid teleport. Menu, tutorial, teleport aim, tracking and focus restrictions block new grabs. A current holder can release; each hand must return Grip to neutral before rearming, preventing an unexpected pickup after interruption.

### Instructions and recovery

Each Play/Player starts English; current-run Chinese switching refreshes open panels immediately. Stable guide cards avoid continuous head-locked motion. Active tools retain a compact task/readout hint. Cancel start point creates no measurement; Undo last display/Hide measurements retain records. Cancel draft removes only a current unsubmitted marker; Hide marker retains saved feedback. B > Settings > Teleport to Safe Position provides checked recovery. Hardware boundaries and stopping on discomfort remain essential.


<!-- Page break -->

## Architecture and technical decisions

The deployment target is Windows x86-64 PCVR using Quest 3 and Quest Link, Unity 6000.6.4f1, Meta XR Core/Interaction/OVR 205.0.0, OpenXR 1.18.0, XR Management 4.7.0 and Input System 1.20.0. DirectX 11 and the existing Standalone OpenXR loader are retained. The formal apartment is the first enabled build scene; no second rig or input system is added.

A pure JourneyStateMachine consumes bounded observations from ApartmentGuidedJourney. Tracking/focus invalidity pauses evidence; discontinuous movement does not fabricate walking distance. New size-selection, measurement and saved-feedback revisions prevent history from satisfying the current task. This boundary makes progression testable independently of hardware while the scene adapter observes actual doors, bag and review events.

ShoppingBag owns size, holder and near-grab behavior in its own assembly. ApartmentShoppingBagBridge, in the default assembly, implements the public context boundary for the existing menu, tutorial, teleport and review state. The safety filter acts before a new SDK selection, with raw same-frame intent handling. This avoids an Assembly-CSharp dependency from the reusable module and preserves the team menu/movement ownership.

ReviewStorage writes version-2 scene-keyed JSON, temporary files and a backup; save success follows the disk result. ReviewTools separates retained records from current-run visuals. Restore does not instantiate all historical labels; hiding a visual does not delete its record. Authored labels translate at the display boundary; original participant text and canonical data remain unchanged. Preset feedback avoids keyboard dependence but limits R5 detail.

### Source and integration traceability

[S] Assets/Scenes/Apartment_Detailed_VR.unity. [J] Assets/GuidedJourney/Core/JourneyStateMachine.cs and Assets/Scripts/GuidedJourney/ApartmentGuidedJourney.cs. [B] Assets/ShoppingBag/Scripts (ShoppingBag, BagInteractionGate, BagGripFilter, BagContactSensor, BagHaptics) and Integration/ApartmentShoppingBagBridge.cs. [R] Assets/Scripts/Review (ReviewTools, ReviewStorage, ApartmentReviewController). [U,L,M] Assets/Scripts/ApartmentMenuController.cs, Localization/ApartmentLanguage.cs and ApartmentVRLocomotion.cs.

PR #10 preserved the guided core; #11 repaired the single OpenXR asset; #12 added UI/session refinements. Normal merges retain their histories in d0f512e. Its OpenXR asset remains SHA256 1779ef7d...50b78ff (full value in Verification_Summary). Git commits, imported provenance and Codex assistance are recorded in Contribution_Evidence and Weekly_Activity_Record; third-party assets are not student-original development.


<!-- Page break -->

## Verification limitations and Phase 3 priorities

| Evidence class | Actual result and boundary |
| --- | --- |
| Automated | Clean integrated d0f512e: actual Unity DX11 EditMode 172/172, 0 failed/skipped. 47 Journey, 2 lifecycle, 13 language, 6 placement, 3 billboard, 13 Session, 2 hint and 86 bag cases. |
| Quest 3 report | Owner confirmed f3b2507 language/new-Play English, G00-G11, bag, measurement, markers, Session display, save/reopen, safe teleport and second Play. User-reported Pass; no invented times or screenshots. |
| Windows delivery | Windows x86-64 generation succeeded from d0f512e; complete-package checks are recorded in Verification_Summary. Quest Link Player acceptance and PQ604A/B/C compatibility remain NOT VERIFIED. |

The clean final tests ran on 18 October 2026 at 22:36:58-22:36:59 China time. They exercise logic, rendered UI components and temporary-file persistence; they do not substitute for headset perception or Player execution. Earlier module tests and f3 hardware checks retain their own source/configuration scope. Full logs, source hashes, build identity and outstanding checks are in Verification_Summary.

Two existing compiler warnings remain (CS0252 in a migration script; UAC0005 in a test helper). TMP logs an m_AtlasTextures exception during pre-build and Editor shutdown callbacks; Player font readability remains unverified. Historical UnityOpenXR native crashes have not recently recurred, but their cause is unproven. Stop on session failure/error floods and preserve logs. Known Editor-generated font/performance/settings changes are backed up and excluded from the frozen source.

### Evaluation priorities

R5 is the clearest functional limit: presets can rate an opening but cannot fully explain what action failed and its everyday consequence. Phase 3 should test a short accessible explanation or facilitated follow-up before changing persistence. Interior alternatives and apartment editing remain deferred Could features. Supplied plan images and approximate colliders do not establish exact net area, reach or certified clearance.

Recruit non-developer resident advisers with varied XR experience. Observe unprompted task completion, assistance/skip/error counts, recovery success and interpretation of red/haptic cues. Assess spatial understanding with route/clearance questions and review the specificity/usefulness of saved feedback with a design stakeholder. Record comfort and reasons for stopping; compare supported movement settings without claiming they prevent sickness. Repeat actual Player save/reopen, second launch and course-PC checks before treating deployment as accepted.

Course basis: Group10_Phase1_0920, Scenario C description, Phase 2 assessment rubric and project description, plus supplied Canvas format instructions. The full source IDs/priorities, exact filenames and hashes are in Requirements_Traceability. The group rubric weights functionality 4, XR rationale 2 and technical traceability 2; mechanical passes alone do not demonstrate user value.
