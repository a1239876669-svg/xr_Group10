# UI/session refinement handoff

Original Guided Journey base: `07ba53a`. PR #10 and OpenXR PR #11 have now been merged by normal Merge Commits into `merge-apartmentguidence` (`e0ddc2c`). PR #12 targets that branch and remains Draft until fresh Quest acceptance. The PR #9 README is retained together with the session-display note; Guided Journey history is not rewritten.

## Open the correct copy

1. Finish other tests and close other project Editors. In Unity Hub add **D:\Documents\GitHub\xr_Group10-ui-session-refinement**; use **6000.6.4f1**.
2. Open **Assets/Scenes/Apartment_Detailed_VR.unity** alone. It already includes the integrated bag, explicit furniture targets and one player system; do not add ShoppingBag_Test or another Rig.
3. The merge incorporates the separately approved PR #11 OpenXR asset, SHA256 `1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff`. Its content must remain byte-identical to PR #11. It is inherited from the integration baseline, not an additional UI configuration change. Do not use Fix All.
4. Preserve existing data before manual save tests: this project's company/product resolves on this Windows account to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Quest Guidence\ReviewSessions`. Different clones may share it. Never clear it to obtain a clean display.
5. Connect Quest Link, enter Play manually, and use `Quest3-UI-UX-Regression.csv`. Fresh final-fix hardware rows remain Not Run. Historical user-reported Guided Journey G00–G11 passes apply to `07ba53a`, not this new candidate. First check fresh English and live language switching, tool hints/readouts and second Play; then the complete Journey and disk save/reopen.
6. Recovery: **B > Measurement > Cancel start point / Undo last display / Hide measurements**. A draft marker offers **Cancel draft**; saved feedback offers **Hide marker**. **B > Settings > Teleport to Safe Position** uses the existing confirmation. Release controls before resuming. Do not test physical entrapment by stepping outside the Quest safety boundary.
7. Stop, wait 10 seconds, second Play. Verify old visuals stay hidden while data remains; create and save new records. Windows Player acceptance is still pending after the stable source/configuration is frozen.

## Evidence

- `UI-Localization-Implementation.md`, `Tutorial-Panel-Comfort-Fix.md`, `Session-Visuals-Persistence-Fix.md` explain causes, code and limits.
- `Verification/Actual-EditMode-Results.json`: actual Unity DX11 **160/160 Passed**, source hashes and raw-XML hash. This is not offline-only compilation or a hardware result.
- The original 160-case evidence is retained unchanged. The later pre-integration run passed 172 cases (language 13, session/readout 13, placement 6, billboard 3, tool hint 2, state/lifecycle 49, bag 86). Post-merge verification is recorded separately. Current final-fix PlayMode/Quest/Windows Build: **Not Run**.
- Read-only asset scripts `asset_checks.py`, `phase_b_assets.py`, `integration_checks.py` passed; run `python -B Docs/UISessionRefinement/check_refinement.py` for unchanged Scene/Prefab/Rig/core/schema and glyph contracts.
- The existing TMP shutdown `m_AtlasTextures` exception remains a known issue, not suppressed. Two compiler warning categories remain (CS0252 and UAC0005); no C# compiler error. A batch log is not an Editor Console counter.
- Local raw logs and exact seven-file rollback hashes are under ignored `Logs/UISessionRefinement-20261018`. Before/after byte backups are outside Git at `D:\Documents\COMP5424\LocalBackups\UISession-PreUnity-20261018-204345-542227`.

No formal Scene, Prefab, XR Rig, Input Binding, package manifest/lock, ProjectSettings, saved JSON schema or user feedback was changed by this commit. The local OpenXR test file remains unstaged. No Windows Build, hardware automation, branch merge or final submission was performed.

## Final local refinements

Every new Play/Player starts English; a current-run Chinese selection refreshes immediately and survives menu reopening. Measurement shows the real current/last distance in the compact hint and menu. Current marker information is readable in the same hint; labels receive a small viewer-facing presentation offset without moving stored points or making all historical markers visible through walls. No save schema, Journey stages or controller mapping changes. See `PR12-Integration-Verification.md` for current merge and acceptance evidence.
