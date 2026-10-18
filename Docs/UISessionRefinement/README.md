# UI/session refinement handoff

Dependent base: `feature/phase2-guided-journey@07ba53a`. PR #10 and OpenXR PR #11 remain separate and unmerged. This branch must be reviewed and Quest-tested before accepting its presentation changes. After the owner accepts/merges PR #10, retarget the dependent PR to `merge-apartmentguidence` and retain the latest PR #9 README plus the small session-display note. Do not automatically merge or rewrite the Guided branch.

## Open the correct copy

1. Finish other tests and close other project Editors. In Unity Hub add **D:\Documents\GitHub\xr_Group10-ui-session-refinement**; use **6000.6.4f1**.
2. Open **Assets/Scenes/Apartment_Detailed_VR.unity** alone. It already includes the integrated bag, explicit furniture targets and one player system; do not add ShoppingBag_Test or another Rig.
3. This copy has the separately approved local OpenXR test fix, SHA256 `1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff`. It is intentionally uncommitted and not a UI-PR change. A fresh clone needs the separately reviewed PR #11 configuration, not Fix All.
4. Preserve existing data before manual save tests: this project's company/product resolves on this Windows account to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Quest Guidence\ReviewSessions`. Different clones may share it. Never clear it to obtain a clean display.
5. Connect Quest Link, enter Play manually, and use `Quest3-UI-UX-Regression.csv`. New-branch hardware rows are Not Run. First check language, panel readability and G00–G05; then complete G06–G11 and real disk save/reopen.
6. Recovery: **B > Measurement > Cancel start point / Undo last display / Hide measurements**. A draft marker offers **Cancel draft**; saved feedback offers **Hide marker**. **B > Settings > Teleport to Safe Position** uses the existing confirmation. Release controls before resuming. Do not test physical entrapment by stepping outside the Quest safety boundary.
7. Stop, wait 10 seconds, second Play. Verify old visuals stay hidden while data remains; create and save new records. Windows Player acceptance is still pending after the stable source/configuration is frozen.

## Evidence

- `UI-Localization-Implementation.md`, `Tutorial-Panel-Comfort-Fix.md`, `Session-Visuals-Persistence-Fix.md` explain causes, code and limits.
- `Verification/Actual-EditMode-Results.json`: actual Unity DX11 **160/160 Passed**, source hashes and raw-XML hash. This is not offline-only compilation or a hardware result.
- New tests: session/recovery 10, language 9, panel 6. Existing 135 EditMode cases also passed. Current branch PlayMode/Quest/Windows Build: **Not Run**.
- Read-only asset scripts `asset_checks.py`, `phase_b_assets.py`, `integration_checks.py` passed; run `python -B Docs/UISessionRefinement/check_refinement.py` for unchanged Scene/Prefab/Rig/core/schema and glyph contracts.
- The existing TMP shutdown `m_AtlasTextures` exception remains a known issue, not suppressed. Two compiler warning categories remain (CS0252 and UAC0005); no C# compiler error. A batch log is not an Editor Console counter.
- Local raw logs and exact seven-file rollback hashes are under ignored `Logs/UISessionRefinement-20261018`. Before/after byte backups are outside Git at `D:\Documents\COMP5424\LocalBackups\UISession-PreUnity-20261018-204345-542227`.

No formal Scene, Prefab, XR Rig, Input Binding, package manifest/lock, ProjectSettings, saved JSON schema or user feedback was changed by this commit. The local OpenXR test file remains unstaged. No Windows Build, hardware automation, branch merge or final submission was performed.
