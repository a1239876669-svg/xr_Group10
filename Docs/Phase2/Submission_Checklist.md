# Phase 2 Submission Checklist

COMP5424 Group 10 — prepared 18 October 2026
Course deadline supplied by the team: **18 October 2026, 23:59 (Asia/Shanghai)**. Confirm the final Canvas instructions and submission location. This checklist records preparation, not a claim that Canvas submission is complete.

## Source and test identity

- Integrated runtime source: [`d0f512eb453a19d47352dccf2dc4979bd094a90d`](https://github.com/a1239876669-svg/xr_Group10/commit/d0f512eb453a19d47352dccf2dc4979bd094a90d), branch `merge-apartmentguidence`.
- PR #10/#11/#12 are actually merged through ordinary Merge Commits; their earlier implementation and author history is retained.
- Documentation is prepared separately on `docs/phase2-final-deliverables`; the team must review its PR. A docs commit is not automatically the runtime/build source identity.
- Final clean-copy **Unity EditMode: 172/172 Passed**, as reported by the actual new-workspace run. Record the exact XML/log/hash and source in [Verification Summary](Verification_Summary.md). Do not substitute the older 160-case result.
- Latest integrated-module Quest check applies to PR #12 head **`f3b25074535750a1198b5685fe9081723fc8a710`**: **User-reported Pass** for language/new Play English, UI/tutorial, G00–G11, bag, measurement, Marker, session display, save/reopen, safe teleport and second Play. The user supplied no exact execution time/screenshots/raw hardware log. The merge does not make this a Windows Player pass.
- Actual Windows x86-64 generation **SUCCEEDED** from d0f512e. The ZIP is 129,428,303 bytes and passed CRC/full-extraction/file-hash checks. [Build manifest](Build_Manifest.json) records identity. Player/Quest Link/PQ604A/B/C acceptance remains NOT VERIFIED.

## Group hand-in artefacts

| Check | Required material / actual intended file | Current preparation and final action | Owner |
|---|---|---|---|
| [ ] | [Root README](../../README.md) | Review member IDs/accounts, accurate task/controls, final source, build location and all validation limits. Replace pending build values only with observed results. | Integration/documentation owner; all members confirm identity |
| [ ] | `Phase2_Prototype_Design_and_Technical_Note.md`, `.docx`, `.pdf` in this folder | English MD/DOCX/PDF generated. Actual exported PDF has **4 A4 pages**, nominal 2.5 cm margins and Times New Roman >=12 pt; all pages visually inspected. Team content review remains pending. | Report owner + team |
| [ ] | [Requirements Traceability](Requirements_Traceability.md) | Use actual R1–R12/priorities; distinguish implemented, verified, partial and deferred. Confirm the final report agrees with it; do not mark optional Could features complete. | Report/module owners |
| [ ] | [Weekly Activity Record](Weekly_Activity_Record.md) | Repository event record created; genuine meetings, effort/allocation decisions and member confirmation remain to be supplied where missing. Do not invent hours/attendance. | Every member |
| [ ] | [Contribution Evidence](Contribution_Evidence.md) | Review source/import authorship and each member's scope; confirm YuzYue/YuzYue7 and YANG Jiale/YJL-196 mapping. Preserve IDs and external/AI assistance distinctions. | Every member |
| [ ] | [Verification Summary](Verification_Summary.md) and genuine raw evidence | Preserve clean-copy EditMode output, exact runtime source, user-reported Quest scope, OpenXR checks and actual build result. Retain remaining warnings/exceptions. | Test/integration owner |
| [ ] | [Participant Quick Start](Participant_Quick_Start.md) | Confirm real Quest controls, route, save/exit, cancellation/recovery and model limits. Teacher/novice instructions must match the built version. | UI/test owner |
| [ ] | [External Resources and Licences](External_Resources_and_Licences.md) | Confirm model/material/font/floor-plan provenance and permission gaps; preserve notices. A working build does not resolve missing licence evidence. | Importing members + integration owner |
| [ ] | Complete Windows runtime folder and ZIP, kept outside Git | Include EXE, `_Data`, `UnityPlayer.dll`, required plugins/dependencies; record source SHA, Unity version, build log, ZIP size and SHA256. Test archive integrity and extraction. **Build Generated and archive-verified; every Player manual functional test NOT RUN / NOT VERIFIED; upload pending.** | Build owner |
| [ ] | GitHub source access and final URL | Supply an explicit branch/commit link and ensure teaching staff can reach it. Check Git LFS content, not pointer files alone. | Integration owner |
| [ ] | Canvas group submission | Upload/link actual required files, check downloads and receipt before the deadline. No automated Canvas submission is claimed. | Designated team submitter |

## Windows acceptance before calling the delivery verified

1. Record the build command/log, actual exit/result and the source SHA used. Preserve build warnings; do not change approved source/XR settings without authorization to make it compile.
2. Extract the complete ZIP into a writable folder and confirm the documented executable and companion files exist. Keep large outputs, Library, Temp and raw Logs out of Git history.
3. **User must run the actual EXE through Quest Link**: immersive view/tracking/controllers, menu, movement and Guided Journey; bag Grip/feedback; a real measurement and Marker; disk save, exit and restart/reopen; safe recovery and the relevant restart behavior. Record Actual/Pass/Fail without borrowing Editor results.
4. Confirm target Windows x86-64 and the intended Meta OpenXR runtime on the test PC. Record headset/PC/software conditions; avoid calling every Windows machine supported from one run.
5. **PQ604A/B/C compatibility is NOT VERIFIED.** Record an actual course-machine result or state the remaining limitation; do not replace it with development-PC evidence.
6. If the EXE or saved-data round trip fails, retain artefacts/logs and report the blocker. Do not mark the ZIP ready merely because compression succeeded.

## Repository visibility and publication decision

A direct GitHub check during preparation found default branch **`main`**, at **`fc0444ccc6b782527907ed2762a493d036694494`**, which is older than the final integrated source. Opening the repository home page therefore does not guarantee teachers see the final prototype or updated documentation.

- [ ] Team explicitly chooses the hand-in publication route: a pinned exact `merge-apartmentguidence` commit/build link, an approved release, or a separately authorized ordinary integration into main/default-branch change.
- [ ] Provide the teaching team the exact final source/docs/build locations; verify access/downloads.
- [ ] Review/approve the documentation PR before merging it. Do not silently merge runtime code into main, change the default branch, rewrite history or enable auto-merge.
- [ ] Confirm actual build/source identity again if the integration branch moves after this snapshot.

## Individual submission — each member is responsible

- [ ] Each of the five members reviews their own Phase 2 Individual Essay and repository evidence; team technical notes are not a replacement.
- [ ] English, **no more than 750 words**, with the applicable individual rubric: repository-evidenced contribution (3 marks) and technical/design reflection (1 mark). Kai's target draft range is 650–700; do not assume that draft is another member's essay.
- [ ] Verify personal claims, teammates/SDK/external assets, assistance declaration and test scope. No invented experiences, sole-authorship claims, user studies or Player passes.
- [ ] Paste the final approved essay into the member's own **Canvas Text Entry**, verify the content/word limit and retain the individual submission receipt. This repository task does not submit on anyone's behalf.

## Unresolved confirmations to hand over

Player acceptance and build upload; classroom-PC compatibility; genuine weekly information; member/account aliases; external-resource permission gaps; final documentation PR review/publication route; each individual essay and the actual Canvas receipts remain human-confirmed hand-over items. Use [Verification Summary](Verification_Summary.md) for evolving technical results rather than editing historical evidence into a false pass.
