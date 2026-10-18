# Phase 2 Requirements Traceability

Source implementation baseline: `merge-apartmentguidence@d0f512eb453a19d47352dccf2dc4979bd094a90d`, the normal Merge Commit for PR #12. The requirement identifiers and priorities below are copied from the team's **Group10_Phase1_0920.pdf**, PDF page 2; their interpretation uses pages 3-5, not an invented feature checklist.

## Status and evidence definitions

**F = Fully Implemented**, **P = Partially Implemented**, **N = Not Implemented** describe the integrated implementation within the stated prototype scope. F does not mean independently evaluated with novice participants, proven comfortable, dimensionally certified, or passed in a Windows executable. Implementation and verification are separate columns.

- **Source / automated:** inspected integrated source; actual Unity 6000.6.4f1 DX11 EditMode run passed **172/172**, with no failed or skipped tests, on merged feature source `8848bf583f1d2d73dead55b8f2889566dcfb4551`. Its exact evidence is in `Docs/UISessionRefinement/Verification/PR12-Integration-Results.json`. The later submitted feature head was `f3b2507`; the integration Merge Commit is `d0f512e`. Additional final-build results belong in `Verification_Summary.md`.
- **User-reported hardware:** the owner reported successful Quest 3 / Quest Link validation of the current PR #12 candidate: new-Play English and live Chinese switching, UI/tutorial, G00-G11, shopping bag, measurement, markers, Session display, save/reopen and safe teleport, without a blocking issue. This is a human report, not automatically recorded execution, participant research, or a Windows Player pass. Exact timestamps, footage and individual measurements were not supplied.
- **Windows Player:** build generation, executable launch, Quest Link behavior and persistence must each have their own recorded result. No F in the table establishes these results.

## Exact requirements and present status

| ID | Phase 1 priority | Original requirement | Status | Integrated implementation and source keys | Verification and remaining limitation |
|---|---|---|---|---|---|
| R1 | Must | Provide one complete and testable XR design-review journey. | F | Formal scene connects G00-G11, bag task, measurement, marker feedback, disk save and Exit; [J], [S], [R]. | Automated state tests and user-reported complete headset journey. Final executable and independent participant completion require separate validation. |
| R2 | Must | Help non-technical reviewers understand relevant spatial aspects such as scale, movement, circulation and reach. | F | Metre-based scene, real movement/teleport, three bag volumes and two-point distance; [S], [M], [B], [R]. | Module/headset behavior reported working. Model dimensions and collider fidelity are assumptions, not supplied BIM/CAD; improved spatial understanding or reach accuracy has not been established by a study. |
| R3 | Must | Organise the review around a specific everyday task and design decision. | F | Select a bag at the entrance, carry it through the living room to the kitchen, notice furniture/circulation conflicts, then measure and mark an issue; [J], [B], [R]. | Real size selection/hold, living-room route and designated kitchen floor release gate progress. The decision concerns circulation and furniture positioning; it is neither cupboard-fit certification nor alternative apartment redesign. |
| R4 | Must | Allow reviewers to record feedback linked to a specific location, object or design decision. | F | Marker ID, room, 3D coordinates, object path, timestamps, measurement ID and scene-specific storage; [R]. | User-reported marker and save/reopen acceptance; storage and Session tests. Final Player path, room calibration and actual failure recovery need their own checks. |
| R5 | Must | Make feedback specific enough to explain what was experienced and why it matters. | P | Located feedback chooses an aspect, rating and importance, linked to a measurement where available; existing participant text is preserved; [R], [L]. | The new-record UI supplies controlled phrases rather than a specific account of the experience and its everyday consequence. Non-empty `observation`/`everydayImpact` JSON fields alone do not prove requirement fulfilment. A supported short explanation or facilitated follow-up is a Phase 3 priority. |
| R6 | Must | Allow the core review task to be completed without continuous facilitator assistance. | F | In-headset G00-G11 guidance, model/safety explanations, Help, skip-with-recorded-outcome and recovery; [J], [U]. | Owner-reported complete journey; no independent novice study. Headset fitting and physical safety assistance remain allowed. Help/skip is not recorded as successful completion of a skipped task. |
| R7 | Must | Provide simple and understandable XR interactions suitable for users with different levels of XR experience. | F | Existing B menu, Trigger pointing, Grip near-grab, single ownership, rearm/gates, bilingual labels and contextual recovery; [U], [B], [L]. | Automated and owner headset regression reported. Ease of learning and terminology for different users remain evaluation questions, not established findings. |
| R8 | Must | Support comfortable and accessible completion of the core review task. | F | Smooth/teleport choice, speed/turn controls, comfort presets, vignette, stop/Exit and checked safe-position recovery; [M], [U]. | Safe recovery reported working. Default Both differs from the proposed teleport default; options do not prove reduced sickness, universal accessibility or regulatory compliance. Representative comfort/barrier testing remains necessary. |
| R9 | Must | Clearly communicate fixed, reviewable and simplified design elements and relevant model limitations. | F | G00 and Help explain fixed structure/walls, selected operable doors/cabinets and simplified furniture/dimensions with no certified-clearance claim; English/Chinese `ModelHelp`; [J], [L], [A]. | The previous missing participant-visible notice is now implemented. Source and headset UI acceptance support availability, not correct comprehension by future reviewers. Original spatial assumptions remain documented. |
| R10 | Should | Allow reviewers to indicate the relative importance of identified issues. | F | Low/Medium/High importance in marker feedback and saved record; [R]. | Persistence/state tests and owner-reported feedback workflow. Meaningfulness of priorities to the architect is not yet evaluated. |
| R11 | Could | Support comparison between alternative interior design options. | N | No two-layout/interior-design comparison workflow. | Explicitly deferred in Phase 1. Three bag sizes compare belongings on one layout; they do not implement R11. |
| R12 | Could | Allow participants to directly modify selected design elements. | N | No participant apartment-design editing workflow. | Explicitly deferred in Phase 1. Opening a door, placing an issue marker or resizing a bag does not count as directly modifying the apartment design. |

## Source-key map

Paths are relative to this repository and refer to the implementation baseline above.

- **[S] Formal scene:** `Assets/Scenes/Apartment_Detailed_VR.unity`; `Assets/ShoppingBag/Prefabs/ShoppingBag.prefab`.
- **[J] Journey:** `Assets/GuidedJourney/Core/JourneyStateMachine.cs`; `Assets/Scripts/GuidedJourney/ApartmentGuidedJourney.cs`; `Assets/GuidedJourney/Tests/JourneyStateMachineTests.cs`; `Assets/GuidedJourney/Tests/AdapterLifecycleTests.cs`.
- **[B] Shopping bag:** `Assets/ShoppingBag/Scripts/ShoppingBag.cs`; `BagInteractionGate.cs`; `BagGripFilter.cs`; `BagContactSensor.cs`; `BagHaptics.cs` within that Scripts folder; `Assets/ShoppingBag/Integration/ApartmentShoppingBagBridge.cs`.
- **[R] Review/data:** `Assets/Scripts/Review/ReviewTools.cs`; `ApartmentReviewController.cs`; `ReviewStorage.cs`; `ReviewRoomMap.cs` within that Review folder; `Assets/GuidedJourney/Tests/SessionVisualsTests.cs`.
- **[U] UI/instructions/recovery:** `Assets/Scripts/ApartmentMenuController.cs`; `Assets/Scripts/ApartmentTutorialController.cs`; `Assets/Scripts/GuidedJourney/GuidancePanelPlacement.cs`; `Assets/Scripts/Review/ReviewBillboard.cs`; `Assets/GuidedJourney/Tests/ToolHintDisplayTests.cs`.
- **[L] Language:** `Assets/Scripts/Localization/ApartmentLanguage.cs`; `ApartmentLanguage.Catalog.cs`; `ApartmentLanguage.Journey.cs` within that Localization folder; `Assets/GuidedJourney/Tests/LocalizationTests.cs`.
- **[M] Movement/comfort:** `Assets/Scripts/ApartmentVRLocomotion.cs`; `Assets/Scripts/ApartmentComfortSettings.cs`; `Assets/Scripts/ApartmentComfortVignette.cs`.
- **[A] Assumptions:** `Docs/ApartmentGeometryAssumptions.md`; `Docs/ApartmentFurniturePlan.md`.

## Scope and interpretation that the report must retain

The authoritative Scenario C is **Virtual Apartment Design Review** for fictional Cedar & Stone Architects / Riverside Community Housing. The goal is design discussion by non-technical stakeholders, not property sales. Resident advisers and the entrance-living-kitchen shopping task are the team's justified focus, not a client-mandated feature list. No BIM/CAD is supplied. The reference 72 square metres is not proof that this model accurately reproduces usable area. Building envelope/structural core are fixed; selected interiors are reviewable. Dimensions, reach and clearance claims must identify model assumptions. Do not claim construction readiness, code/accessibility certification, sickness prevention or completed user research.

The original proposal describes measurement as optional. G09 provides guided measurement practice, but Help can explicitly skip it; skipped tasks retain a distinct outcome and are not falsely labelled completed. G08 releases the bag into a marked kitchen floor area, not into a cupboard. R11/R12 are deliberately deferred Could requirements; they are not missing mandatory features.

For new feedback, the aspect/rating form is a purposeful keyboard-dependence trade-off, with an R5 cost: e.g. a stored rating of 'too narrow' does not explain what action failed or its everyday consequence. Preserving existing free text is important but does not create a new accessible free-text entry flow. This limitation should stay visible even after all mechanical tests pass.

## Official assessment and format cross-check

The **COMP5424-Group-Project-Assessment-Rubric-P2-202627A.pdf**, page 2, allocates the group **8 marks**:

1. **Core prototype functionality (4):** reliable complete journey on the specified platform; working principal interactions, instructions, feedback, error handling and recovery; suitable for Phase 3 evaluation.
2. **XR interaction and user-experience rationale (2):** interaction/spatial presentation/feedback suit intended users; deliberate comfort, accessibility and safety decisions connected to requirements.
3. **Technical quality and traceability (2):** sound, organised, scoped implementation; meaningful attributable history; requirement-feature mapping; documented external resources/limitations; source-build consistency.

Page 3 allocates each individual **4 marks**: repository-evidenced contribution **3**, technical/design reflection and understanding **1**. Commit quantity is explicitly not a score. Attribute significance, correctness, authorship, integration and learning. Initial Baidu model sharing needs separate source/share evidence; Meta SDK and third-party assets are not student-original development. Preserve original/cherry-picked/imported provenance.

The **COMP5424-Group-Project-Description-202627A.pdf**, pages 6-7, requires a working executable/deployable build, accessible source repository, a design/technical note and concise weekly activity record. The note maps requirements to implementation, explains major decisions and identifies limitations/evaluation priorities. Pages 11-13 require course-platform compatibility, external-resource source/licence/purpose/modification records and applicable assistance declarations. Final Canvas instructions govern exact upload formats and access.

The provided Canvas screenshot **Phase2_Group Component.png** was read directly. It specifies:

- No more than **four A4 body pages**, with **2.5 cm margins** on all sides; cover, references and appendices excluded.
- **Times New Roman, at least 12 pt**; a single report PDF.
- A full executable ZIP suitable for PCs in **PQ604A/B/C**; not a ZIP of the EXE alone.
- Root README containing members' student IDs and accounts; public repository or teaching-team access.

**Phase2_Individual Component.png** specifies a maximum **750-word** individual essay submitted through Canvas text entry, citing direct repository evidence. Individual reflection must be reviewed and owned by its student; a team report does not replace it.

Suggested four-page body: (1) context and compact R1-R12 implementation matrix, (2) actual journey and XR interaction rationale, (3) architecture/decisions including input ownership and data/display separation, (4) distinct automated/hardware/Player verification, limitations and Phase 3 priorities. Keep full source paths and evidence detail here as supporting material; do not shrink the body below 12 pt to fit it.

## Exact original course sources

These local originals were reread, and their text matches the earlier extracted evidence:

| Original file in `D:/Documents/COMP5424/` | Pages used | SHA-256 |
|---|---|---|
| `Group10_Phase1_0920.pdf` | 2: R1-R12/priorities; 3-5: assumptions, traceability and journey; 7: planned leads | `3b14aaa112f8dfbc1d0d318b1654a7f96be9b36662113f4ecef4ef1a43e999bc` |
| `COMP5424-Scenario-Description-Virtual_Apartment_Design-202627A.pdf` | 1-4: client context, assumptions and boundaries | `e9f615f884e4c816d918e76b703c516b33c08e1ea3b9cf34dd7f091eab6b5067` |
| `COMP5424-Group-Project-Assessment-Rubric-P2-202627A.pdf` | 2: group 4+2+2; 3: individual 3+1 | `13031d13108fa981a5c76173d96174ec6535f6d1e70b729e604c36e1ea167797` |
| `COMP5424-Group-Project-Description-202627A.pdf` | 6-7: Phase 2; 11-13: platform, attribution and submission | `43ae4d87559ac5b3f3e45022df39361a0f720416c3ab8f38450c6b057cacb99b` |

The two Canvas screenshots named above are also in that source folder. Exact text extractions and hashes are archived locally under the original worktree's ignored `Logs/Phase2FinalDelivery-20261018/CourseSources/`. They are supporting course evidence, not project runtime assets. Older matrices and reports describing `c3408f4` remain historical snapshots; do not copy their former missing-Guided-Journey, missing-R9-notice or unmerged-OpenXR claims into the final report.

Final clean integrated-source confirmation: actual DX11 EditMode on `d0f512e` also passed 172/172, 0 failed/skipped. See [Verification Summary](Verification_Summary.md) and [final raw result](Verification/Final-EditMode-Results.xml). Windows build generation and ZIP integrity passed; all independent EXE manual functionality remains NOT RUN / NOT VERIFIED as expressly clarified by the owner.
