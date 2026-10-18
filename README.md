# COMP5424 Group 10 — Virtual Apartment Design Review

**Scenario C: Virtual Apartment Design Review.** This Phase 2 prototype helps resident advisers understand apartment scale, circulation and everyday use, then give location-linked feedback to the design team. The core task is carrying shopping along **Entrance → Living Room → Kitchen**. It is an architectural design-review prototype, not a property-sales presentation.

Dimensions are documented assumptions in a simplified model, not construction information or proof of compliance. Structure is fixed; alternative layouts and direct furniture editing are deferred.

## Team

| Name | Student ID | GitHub Account | Responsibility |
| --- | --- | --- | --- |
| Guo Jikang | 26125103g | YuzYue | Distance measurement and feedback |
| Fan Zujian | 26124914g | fanfanzujian-oss | User interface and onboarding tutorial |
| Yang Jiale | 26042226g | YJL-196 | Movement comfort |
| Zhuang Kaiyu | 26109544g | KaiZ755 | 3D modeling and shopping bag |
| Xu Boyan | 26052354g | a1239876669-svg | Integration, version control, and project setup |

SDKs, external assets and subsequent integration are distinct from individual original contributions.

## Version and release status

- Integration branch: [`merge-apartmentguidence`](https://github.com/a1239876669-svg/xr_Group10/tree/merge-apartmentguidence).
- Source basis checked for this README: [`a27dd1c`](https://github.com/a1239876669-svg/xr_Group10/commit/a27dd1c6f937ed694d69e885dd2c83cf512cd9f8), 18 October 2026.
- **Final frozen release SHA, Windows EXE/ZIP and download link: Pending — to be updated after final validation.** No final Player or course-machine pass is claimed.
- Main scene: `Assets/Scenes/Apartment_Detailed_VR.unity`.

| Component | Declared version / target |
| --- | --- |
| Unity | 6000.6.4f1 |
| Meta XR Core, Interaction and Interaction OVR SDKs | 205.0.0 |
| Unity OpenXR / XR Plug-in Management | 1.18.0 / 4.7.0 |
| Unity Input System | 1.20.0 |
| Intended environment | Windows PCVR, DX11, Meta Quest 3 through Meta Quest Link |

Versions come from project files; package resolution and runtime acceptance are separate checks. Android APK delivery is outside the current Phase 2 target.

## Open in Unity

With Git and Git LFS available, clone into a new directory:

```powershell
git clone --branch merge-apartmentguidence --single-branch https://github.com/a1239876669-svg/xr_Group10.git xr_Group10
git -C xr_Group10 lfs pull
git -C xr_Group10 rev-parse HEAD
```

1. Unity Hub → **Add → Add project from disk**: select the repository root and use **6000.6.4f1**. Do not upgrade or open two Editors on one folder.
2. Wait for package import/compilation; inspect **Window → General → Console**. Preserve errors; do not upgrade packages, delete caches or use **Fix All** as a workaround.
3. Connect Quest 3 in Meta Quest Link, enter Link in the headset and confirm tracking and the intended Meta OpenXR runtime. A desktop mirror alone is not immersive VR.
4. Open **only** `Assets/Scenes/Apartment_Detailed_VR.unity`. Reuse its rig; do not add additive test scenes. Check the OpenXR limitation below before manually entering Play.

`ShoppingBag_Test` and `ShoppingBag_UI_Test` are development fixtures, not Player entry scenes. Record the actual SHA and approved local overrides with tests.

## Run the Windows executable

**Build filename/download link, final frozen SHA and platform acceptance: Pending.** Once published, extract the **entire ZIP** to a writable folder, keep the EXE, `_Data`, `UnityPlayer.dll` and plugins together, connect Quest Link and launch the documented EXE from Explorer. Follow the task below, including exit/reopen.

The owner must supply the real EXE/ZIP names, download URL, source SHA, SHA256 and actual PQ604A/B/C compatibility results. No successful build or Player test is asserted here.

## Included modules

- World-space menu/onboarding, smooth movement and teleport, safe reset, door/cabinet interaction and three-size bag contact feedback.
- Comfort vignette, smooth acceleration/deceleration and **Comfort / Standard / Sensitive** presets.
- Measurement, automatic room detection, location-linked markers and structured review records, with autosave/restore. Export support exists in code; an export-menu workflow is not claimed as verified.

## Controls

| Input | Action |
| --- | --- |
| Left stick | Smooth movement, when enabled |
| Right stick | Smooth or snap turning; in the comfort menu, up/down adjusts the pointed setting |
| Right **A** | Hold to aim teleport; release at a valid green destination, when enabled |
| Right **B** | Open/close the menu; follow modal confirm/cancel prompts |
| Right ray + index **Trigger** | Menu selection, nearby door interaction, measurement points or marker placement/selection, according to active mode |
| Left or right **Grip** | Near-grab the bag; release Grip to put it down. Only one hand holds it at a time |
| **My Items / 我的物品** | Select Small, Medium or Large while the bag is released |

Menu/tutorial restrictions, teleport aiming, tracking loss and focus loss block new bag grabs. After an interruption, return controls to neutral and release Grip before pressing again. Existing holders can release; holding Grip through recovery must not automatically pick up the bag.

## Complete the design-review task

1. Complete or skip onboarding after headset safety/setup. Use **B** and right ray/Trigger for the menu.
2. Find the entrance bag. While released, choose **My Items / 我的物品** size. Dimensions in metres (width × height × depth): **Small 0.30 × 0.30 × 0.15; Medium 0.45 × 0.40 × 0.22; Large 0.60 × 0.48 × 0.30**. Held bags cannot resize.
3. Close the menu, return controls to neutral and Grip near the handle. Compare sizes on the **same Entrance → Living Room → Kitchen route**; outcomes depend on geometry and pose.
4. Registered furniture contact gives **red feedback and a short pulse in the holding hand**. Move clear to restore appearance. Warnings do not universally block movement or certify real-world clearance.
5. Choose **测距 → 开始测距**; point/Trigger at two surface endpoints for distance in metres.
6. Choose **标记注释 → 放置标记**, place/select an issue marker, choose feedback aspect/rating, importance (**低/中/高**, Low/Medium/High) and optional measurement link, then **保存反馈**. Feedback uses structured presets; free-text/voice entry is not verified.
7. Check storage errors/records, confirm menu exit, then reopen the **same build/scene** and verify marker location, feedback and links. This final round trip remains pending.

## Saved records

`ReviewTools.SavePath` resolves the scene-keyed JSON under `Application.persistentDataPath/ReviewSessions`; for this scene the file is `2129619AB63EE0D1_current.json`. Windows normally uses:

```text
%USERPROFILE%\AppData\LocalLow\DefaultCompany\Quest Guidence\ReviewSessions
```

Autosave, temporary-file writes, `.bak` and startup restore are implemented; a saved label alone does not prove disk persistence. Preserve errors/records and back up this folder before testing another clone, which may share it. Comfort preferences use PlayerPrefs separately. No unverified export-menu or tutorial/door-progress persistence is promised.

## Verification and known limitations

- **Historical automation:** ShoppingBag A/B/C1 recorded 78/78 EditMode and 2/2 simulated PlayMode passes, including the earlier 26 A/B cases. These are not fresh final-apartment results.
- **Isolated hardware:** User-reported Quest 3 Pass: five grab/release cycles per hand, four C1 checks and eight Phase B checks. This is not final-apartment or user-study acceptance; no screenshots/times are invented.
- **Final validation Pending:** integrated compilation/Test Runner, complete Quest journey, Windows Player, save/reopen and course-machine compatibility. Source wiring has been inspected; this documentation update runs no tests.
- Shared OpenXR settings still contain known Standalone duplicates and Android null references. A separate verification copy's reviewed local repair is **not in this README-only change or shared source**. Preserve `FeatureHelpers` errors and obtain the owner's precise fix; do not regenerate all settings.
- Historical `UnityOpenXR.dll` crashes / `XR_ERROR_LIMIT_REACHED` have an unproven root cause. Stop on persistent grey view, session failure, error floods or discomfort; preserve logs.
- Editor haptic dry-run may suppress physical pulses. Dynamic fonts, route-specific contact coverage, save-failure recovery and complete task/model-limit guidance still need validation. Presets constrain feedback; no representative-user comfort study or full seated mode is claimed.

Use the Quest boundary and a clear play area; software collision is not a physical barrier.

## Evidence and submission documents

Existing repository evidence, retaining each record's historical scope/status:

- [Geometry assumptions](Docs/ApartmentGeometryAssumptions.md), [apartment setup](Docs/ApartmentDetailedVR.md), [comfort settings](Docs/ApartmentComfortSettings.md).
- ShoppingBag [A](Assets/ShoppingBag/Documentation/PhaseA.md), [B](Assets/ShoppingBag/Documentation/PhaseB.md), [C1](Assets/ShoppingBag/Documentation/PhaseC1.md), [test sources](Assets/ShoppingBag/Tests), [UI handoff](Docs/ShoppingBag-UI-Integration-Handoff.md) and [joint checklist](Docs/ShoppingBag-UI-Acceptance.csv). Pending rows are not Pass results.
- Bag commits [470468d](https://github.com/a1239876669-svg/xr_Group10/commit/470468d), [5769964](https://github.com/a1239876669-svg/xr_Group10/commit/5769964), [40193be](https://github.com/a1239876669-svg/xr_Group10/commit/40193be) / [PR #2](https://github.com/a1239876669-svg/xr_Group10/pull/2); UI [b8cbf42](https://github.com/a1239876669-svg/xr_Group10/commit/b8cbf42), cleanup [9e0987e](https://github.com/a1239876669-svg/xr_Group10/commit/9e0987e) / [PR #3](https://github.com/a1239876669-svg/xr_Group10/pull/3).

**Final technical-note PDF, genuine weekly record and final test-result publication locations: Pending team confirmation.** They are separate deliverables; no repository path or completed submission is invented.

Third-party provenance/licences, including Furniture Mega Pack, Real Materials and SDKs, require the owners' final register. Existing [Noto](Assets/Apartment/Fonts/NotoSansSC-LICENSE.txt) and [Liberation Sans](Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt) notices do not license unrelated assets. Preserve authorship and declare assistance under course policy.
