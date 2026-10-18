# Phase 2 remaining delivery risks

2026-10-18. This branch implements the guide; it does not certify a final prototype or Windows executable. Prior user-reported base T01–T06 successes and bag standalone validation are not new-branch results.

| Priority | Remaining risk | Minimum next action / owner |
|---|---|---|
| P0 | Fresh clone still has the shared OpenXR Feature asset defect; the local repaired asset in active final-verification is not a Git fix | Integration owner separately approves and checks local setup before starting this clone. Do not mix OpenXR repair into guide PR, copy active workspace data, or click Fix All. |
| P0 | New complete scene has not been imported or run in Unity; offline references come from an existing local Unity cache | Open with 6000.6.4f1 after the active session ends; verify no compiler/missing-script/import errors, run Editor tests, then the route in Quest Link. |
| P0 | No new Windows Player build or final artifact acceptance | Build owner creates the approved Windows DX11 PCVR candidate, records commit/hash/artifact, and runs immersive entry → saved feedback → restart → exit plus second-session stability. No artifact is claimed here. |
| P0 | Physical furniture blockage and floor-drop clearance/settling not verified | Run the specific carpet/sofa/chair and kitchen tests. If blocked, propose one-object pose/collider delta for new approval; do not globally move furniture or disable collisions. |
| P0 | Feedback persistence/reopen and failure UI need real validation | Confirm saved JSON survives restart at the same scene key and fields/number/location are restored. Simulate a safe isolated save failure only with tester approval. Guide cannot claim success from an in-memory submission. Existing global exit-after-save-failure behavior is unchanged. |
| P1 | Quest Link haptics cause identified for Editor but no physical recovery tested | Left/right held-hand pulses and stop/focus/menu cleanup; Windows Player separately. Prefab flag is not evidence of hardware success. |
| P1 | Historical UnityOpenXR native crash root cause remains unproven | Two Play cycles, normal focused session, no warning flood/limit-reached; preserve logs if reproduced. No SDK upgrade or warning suppression was performed. |
| P1 | Nonmodal card/cabinet label, dynamic font, cyan zone visibility and novice comprehension | Check actual Quest readability/occlusion and menu selection; static font-source glyph coverage is not rendered-text proof. |
| P1 | Two added wall segments are only a local coverage improvement | Confirm exactly the selected segment turns red, simultaneous contacts restore independently; other walls remain explicitly unregistered. |

## Executed vs pending

Executed: 47 pure-state tests; seven offline C# assembly compilations; four Python asset/integration/scene checks; changed/new YAML parsing. These do not exercise Unity lifecycle, physics, SDK native code, persistence in the final Player or performance. Two adapter lifecycle tests and existing A/B/C1 suites are compiled but their Unity Test Runner execution is pending. No automated Play or active Editor was started.

## Scope and assessment relevance

Core Functionality gains a connected route with real door/bag/measurement/save conditions, rather than disconnected features. UX gains short off-centre tasks, living-room free exploration, reversible practice/skip/help and safe-recovery instructions. Traceability gains separate actual action evidence, explicit skipped outcomes, test sources and an unfilled hardware regression table. These changes address the requested Phase 2 criteria but do not predict marks or substitute for evaluator testing.

Full cabinet storage is deliberately deferred in favor of the stated kitchen floor-drop alternative. No height mode, alternative route, furniture editor, extra Rig, extra bindings or optional XR features were added. Guide progress is session-only; review data retains the existing version-2 store. A novice-user study has not been conducted by this task.

## Release order

1. Review the Draft PR and scene delta without merging automatically.
2. On an available approved test workspace, resolve local OpenXR setup separately, import, compile and run Editor tests.
3. Execute the regression CSV through the entire route, including Skip/Help/focus/re-grab/save/reopen; record actual result/evidence instead of filling inferred passes.
4. Integration owner decides merge and freezes the candidate. Build owner validates the exact Windows artifact and updates final README/evidence.
5. If a blocker appears, keep the last known working candidate and narrow the fix; do not spend the deadline on optional interactions.
