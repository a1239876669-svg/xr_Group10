# PR #12 integration verification — review candidate

The feature branch has a normal two-parent merge of `merge-apartmentguidence@e0ddc2c752d99aab36941615635caa5cc90c2695`. The merged source tested by Unity is `8848bf583f1d2d73dead55b8f2889566dcfb4551`; the preceding UI/language refinement commit is `913cc8bf91fe8a5742b3b9afe0747098a507b749`. PR #12 remains Draft and must not be merged until owner-confirmed Quest acceptance.

## Conflict decisions

Only `README.md` conflicted. The first region replaced the obsolete “shared OpenXR still broken” sentence with the accurate PR #11 configuration status, preserving the historical native-crash limitation. The second region retained the new Session-display and per-run English paragraphs after all upstream member, setup, licence and pending-release content. No whole-file ours/theirs selection was used. Guided Journey adapters, UI, language, review tools, Scene and Prefab had no cross-branch conflicts.

OpenXR had an identical existing local repair, which initially made Git refuse the merge. Its original/patched bytes were backed up; with Unity closed the upstream merge adopted the identical approved file. SHA256 remains `1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff`. The merge inherits the PR #11 change; this PR adds no further XR settings change. Git normalized two unrelated tool-settings files' line endings during merge; before/after bytes were retained and their exact protected original bytes restored. Their settings values were unchanged and excluded from staging.

## Preserved behavior and minimal fixes

- PR #10: original Scene/ShoppingBag Prefab, core G00–G11 state machine and saved-data schema remain unchanged.
- PR #11: Standalone valid references, repaired Android references, approved Foveation/Subsampled Layout off states and other original Feature states remain unchanged.
- PR #12: live bilingual UI, stable tutorial panel, session-only visuals, non-destructive recovery and data persistence are retained.
- Every new Play/Player startup resets display language to English at SubsystemRegistration. Within-run Chinese selection refreshes active UI and survives menu reopening; no language PlayerPrefs or feedback changes.
- Active measurement/marker tools retain a compact task hint and current tool readout; Stop returns the full task card. Marker label offsets move presentation only, preserving recorded positions and avoiding blanket visibility of historical labels through geometry.

## Actual automated verification

Unity 6000.6.4f1 / DX11 EditMode: **172 passed, 0 failed, 0 skipped**, 18 October 2026 14:07:59–14:08:00 UTC. Task state machine 47; adapter lifecycle 2; language 13; panel placement 6; billboard 3; Session/readouts 13; compact tool hint 2; ShoppingBag 86. The test runner logged exit code 0 and Tundra build success. This incremental compile emitted no C# errors or compiler warnings; earlier unchanged migration warnings are not claimed fixed.

One known **Editor-shutdown** TMP `m_AtlasTextures` UnassignedReferenceException remains. It is recorded and not suppressed; it is not a failed test, nor evidence of a clean headset/Player session. Meta Setup recommendations are not auto-fixed. Four generated Performance test resources were removed by the runner, then restored byte-for-byte from the verified pre-test backup. Existing local font/ProBuilder/Code Coverage bytes are preserved and excluded from the commit. See `Verification/PR12-Integration-Results.json` for test names, source/evidence hashes and exact limitations.

## Immediate Quest checklist — all Not Run for this final-fix candidate

1. Open only `Assets/Scenes/Apartment_Detailed_VR.unity` in this UI/session worktree, Unity 6000.6.4f1. Check normal immersive view, movement and B menu; preserve any new errors.
2. Fresh Play: English menu and tutorial. Switch to Simplified Chinese: open panels update immediately without changing Journey/bag/review state. Close/reopen the menu: current language stays Chinese.
3. Complete G00–G11: doors/cabinet, return entrance, select S/M/L while released, real Grip, carry, floor-zone release, fresh measurement, new marker, feedback disk save, free exploration and Exit.
4. Grip: both hands, single holder, menu/B+Grip gate, release while blocked, neutral/Rearm after menu/focus/teleport. Original movement and contact red/owner haptics remain correct.
5. Stage 9: real preview/completed metres readable against a wall. Cancel start, Undo last display and Hide measurements retain saved data; compact operation hint stays visible, and Stop restores the main card.
6. Stage 10: current marker label/readout readable; Cancel draft/Hide marker affect only the intended runtime/draft state. Saved feedback/link and hidden history remain intact. Both-language actual button names match Help.
7. New run: old visual objects stay hidden while disk records survive. Stop, wait 10 seconds, second Play returns English and normal XR; current new records still work. Monitor the known TMP shutdown exception and any native XR failure.
8. Stuck recovery: B > Settings > Teleport to Safe Position, neutral then move normally. Stay within the physical Quest boundary.

Historical user-reported G00–G11 Pass belongs to `07ba53a`; the prior UI aggregate of 8 Pass / 1 Partial / 3 Not Run is not assigned invented individual cases. It is not fresh final-fix acceptance. Current PlayMode, Quest and Windows Player/Build are **Not Run**. No PR merge/close, release or final delivery acceptance follows automatically from these EditMode results.

## Files changed in this integration operation

- `Assets/GuidedJourney/Tests/LocalizationTests.cs`
- `Assets/GuidedJourney/Tests/ReviewBillboardTests.cs`
- `Assets/GuidedJourney/Tests/ReviewBillboardTests.cs.meta`
- `Assets/GuidedJourney/Tests/SessionVisualsTests.cs`
- `Assets/GuidedJourney/Tests/ToolHintDisplayTests.cs`
- `Assets/GuidedJourney/Tests/ToolHintDisplayTests.cs.meta`
- `Assets/Scripts/GuidedJourney/ApartmentGuidedJourney.cs`
- `Assets/Scripts/GuidedJourney/GuidancePanelPlacement.cs`
- `Assets/Scripts/Localization/ApartmentLanguage.Catalog.cs`
- `Assets/Scripts/Localization/ApartmentLanguage.cs`
- `Assets/Scripts/Review/ApartmentReviewController.cs`
- `Assets/Scripts/Review/ReviewBillboard.cs`
- `Assets/Scripts/Review/ReviewTools.cs`
- `Assets/XR/Settings/OpenXR Package Settings.asset` — inherited approved PR #11 asset; no additional UI change.
- `Docs/UISessionRefinement/Changed-Files.md`
- `Docs/UISessionRefinement/PR12-Integration-Verification.md`
- `Docs/UISessionRefinement/Quest3-UI-UX-Regression.csv`
- `Docs/UISessionRefinement/README.md`
- `Docs/UISessionRefinement/Technical-Note-Addendum.md`
- `Docs/UISessionRefinement/Tutorial-Panel-Comfort-Fix.md`
- `Docs/UISessionRefinement/UI-Localization-Implementation.md`
- `Docs/UISessionRefinement/Verification/PR12-Integration-Results.json`
- `Docs/UISessionRefinement/check_refinement.py`
- `README.md`
