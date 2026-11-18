# Current Phase 3 spatial layout: release handoff

Prepared 18 November 2026. This note supersedes the 26 October layout descriptions where furniture has subsequently changed; the older evidence is retained as a historical record.

## Exact scene and scope

- Scene: `Assets/Scenes/Apartment_Detailed_VR.unity` (Unity 6000.6.4f1).
- Scene SHA-256: `50ddb8e97de068816f0a09cc7d1624e1caeac23bf6bd501ac03e4b5817b9580d`.
- Includes the committed floor/support and doorway corrections, aligned and tucked dining chairs, compact balcony seating/table, kitchen prop support and clearance adjustments, the corner Monstera-style plant, and the final living-room layout.
- The sofa is approximately 2.20 m long. The coffee table is approximately 1.00 x 0.45 m; its height is preserved and books are not stretched. The backless seat and sofa are supported on the rug.
- Recorded final clearances: sofa/table 0.454 m; backless seat/table 0.553 m; west-side route 0.831 m. Gaps beside the TV unit are furniture gaps, not designated walking routes.
- The approximately 72 m2 outer floor footprint is preserved. This is not a claim of net usable area, legal building compliance, or verified seated comfort.
- Scene-only compact furniture and plant meshes explain the large YAML diff. Existing ShoppingBag, menu, locomotion, measurement, marker and feedback component references are preserved.

## Evidence and limits

| Check | Evidence / status |
| --- | --- |
| Current seven spatial EditMode cases | Actually executed with Unity 6000.6.4f1, DX11 in an isolated temporary project on 18 November: 7 passed, 0 failed, 0 skipped. Raw results: [Spatial-EditMode-Results-20261118.xml](Verification/Spatial-EditMode-Results-20261118.xml). |
| Support-test adaptation | Existing 0.2 mm tolerance retained. The backless seat is tested against the rug. Four replaced balcony objects must be inactive; three compact replacements must be active, uniquely named and supported at Y=0.013 m. No test is skipped. |
| Final scene import and geometry | Retained 15 November developer evidence: exact-scene reimport, 10/10 routes, 237/237 west-side 0.40 m-radius capsule samples, preserved transforms and interaction references. These are geometry/Editor checks, not user trajectories. |
| Earlier complete EditMode suite | Historical 186/186 result applies to the 26 October scene, not this final layout. The full suite was not rerun for this publication. |
| Latest manual check | User reports having roughly tested the modified version. Device, individual cases and detailed results were not supplied; no comprehensive Quest acceptance is inferred. |
| Windows Player and participant evaluation | Not run for this layout. Existing Phase 2 builds were not rebuilt and do not contain these spatial changes. |

The first 18 November test attempt failed before test execution because the old temporary fixture lacked package files. Missing files were copied only into that disposable fixture from the existing same-version local cache; no SDK source or shared package configuration was changed. The successful rerun is recorded separately from that failed attempt.

The known TMP font-atlas shutdown exception is retained as a limitation; it is not a new C# compiler failure. English/Chinese display still needs an explicit current-layout headset check. Source-workspace font, ProBuilder and Code Coverage changes are excluded from this publication; OpenXR, Packages and other ProjectSettings remain unchanged from main.

## Entering Editor Play safely

When historical `ReviewSessions` exists, before Play select:

`Apartment Phase 3 > Review Test Storage > Use New Isolated Development Folder`

This Editor-only adapter creates a private temporary review file and does not copy or modify historical feedback. Repeated Stop/Play in the same Editor reuses it. Do not choose a new folder between save/reopen checks. A full Editor restart requires selecting a new folder; this temporary workflow is not Player persistence acceptance. If Scene Reload is disabled, isolation deliberately blocks Play rather than changing project settings automatically.

## Short remaining acceptance

1. Walk without a bag: entrance, west living route, kitchen, balcony and bedroom/bathroom doorways.
2. Compare S/M/L bags on the same route; verify contact red/recovery, both holding-hand haptics, release and Grip Rearm.
3. Complete Journey, measurement, marker, feedback save and repeat Play using the same isolated data folder. Check both languages.
4. Build and test a new Windows Player separately before redistributing a runtime build. A merge is not a Player or participant-study PASS.
