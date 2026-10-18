# Phase 2 Verification Summary

## Source and evidence boundaries

Integrated runtime source: **`d0f512eb453a19d47352dccf2dc4979bd094a90d`** ([exact source](https://github.com/a1239876669-svg/xr_Group10/tree/d0f512eb453a19d47352dccf2dc4979bd094a90d)). PR #12 merged normally at 22:31:22 China time on 18 October 2026, with parents `e0ddc2c` and accepted head `f3b2507`. PR #10 (`ab149710`) and #11 (`e0ddc2c`) are ancestors. [Integration identity](Verification/Integration-Identity.json) preserves full IDs. Documentation commits add no runtime source or settings changes.

| Evidence | Actual status | Scope and remaining boundary |
|---|---|---|
| Clean integrated Unity import/compile | **PASS** | New final worktree, Unity 6000.6.4f1, DX11; 0 C# errors. Warnings/exceptions below remain. |
| Actual EditMode | **172/172 PASS**, 0 failed/skipped | Clean `d0f512e`, 22:36:58-22:36:59 China time. [Raw XML](Verification/Final-EditMode-Results.xml), [summary/hashes](Verification/Final-EditMode-Summary.json). |
| Current PR #12 Quest 3 / Quest Link Editor regression | **User-reported Pass** | `f3b2507`: language/new-Play English, UI/tutorial, G00-G11, bag, measurement/Marker, Session display, save/reopen, safe teleport, second Play. [Owner report](Verification/Owner-Reported-Quest-Acceptance.md). No fabricated test clock, screenshot or generated headset log. |
| OpenXR/static scene integrity | **PASS** | Approved asset SHA and reference counts below. Availability is not proof of runtime stability. |
| Windows x86-64 generation | **Build Generated** (pipeline Succeeded) | Actual `Build Finished, Result: Success.` and Editor exit 0. Built from `d0f512e`; C# source/settings retained. |
| ZIP completeness | **PASS** | 249 runtime/package files, CRC check and full extraction with every file SHA compared. Unity's non-shipping backup is retained locally outside the ZIP. |
| Actual Windows Player launch / Quest Link runtime / save-restart | **NOT RUN / NOT VERIFIED** | The owner expressly confirmed this EXE has not yet been run. Every Player manual functional case remains Not Run; Editor and archive passes do not fill this row. |
| Course PCs PQ604A/B/C | **NOT VERIFIED** | Requires course-platform run. |
| Representative novice study / comfort and comprehension outcomes | **NOT PERFORMED** | Phase 3 evaluation, not developer regression. |

EditMode groups: Journey 47; adapter lifecycle 2; language 13; panel placement 6; billboard 3; Session/readouts 13; compact tool hint 2; bag feedback 19; Grip safety 52; bag core 7; UI bridge 8. Automated storage cases use isolated temporary storage and do not replace a real Player round trip. Current final-source PlayMode was not separately rerun; earlier simulated PlayMode evidence remains historical.

## Windows package identity

- Build output folder: `Group10_Phase2_d0f512e`; executable: `Group10_Phase2.exe`.
- ZIP: **`Group10_Phase2_WindowsBuild.zip`**, **129,428,303 bytes**; runtime package **367,236,105 bytes** before compression.
- ZIP SHA-256: `870f3f25564fda1b096cc6f65e013066ba86972b893d62a30841e8d58418d2aa`.
- EXE SHA-256: `91a36fa14dd3ccf390b99720805eda0553da1ca1108381b49c98c938f69dc256`.
- [Build manifest](Build_Manifest.json) records source, Unity, target and archive identity. Complete runtime output includes `_Data`, MonoBleedingEdge, UnityPlayer.dll, native XR plugins and runtime binding JSON; no EXE-only package.
- First enabled scene: `Assets/Scenes/Apartment_Detailed_VR.unity`. Existing `Assets/Scenes/test/TwoRooms.unity` and `Assets/Scenes/Apartment.unity` remain additional enabled scenes; their order/settings were not changed.
- Target: Windows x86-64, Mono runtime, DirectX 11, standalone OpenXR with Meta Quest Link. Meta XR Core/Interaction/OVR 205.0.0, OpenXR 1.18.0, XR Management 4.7.0, Input System 1.20.0.
- Download/Canvas upload: **Pending owner publication**. The ZIP is local, deliberately outside Git history.

Local output root is `D:/Documents/COMP5424/Phase2Deliverables/`; raw build/test logs, pre/post bytes and render QA are in `BuildEvidence-20261018-d0f512e`. Build log SHA-256: `bda91f1637c14b3216ab9aec5bad1455f6e3316ab638c46f38885e90c9cba7ce`. This local evidence location is not a published download link.

## OpenXR and source protection

`Assets/XR/Settings/OpenXR Package Settings.asset` SHA-256 remains `1779ef7d5b765aab71b533295b765f3654c36c7e4665d8af0a9a916aa50b78ff`; GUID `924c5ef8bcbe9bb4197cc0204c95a785`. Standalone 34, Android 35, WebGL 1 valid references; no null Feature entries. All 41 script GUIDs resolve. Approved Android Foveation/Subsampled Layout remain off; MetaXRSpaceWarp `-4735985726658496985` remains off. Another pre-existing Android SpaceWarpFeature `2660867977806732929` remains on, exactly as PR #11. Meta XR and ordinary Touch Plus profiles keep their existing enabled states.

All 76 LFS worktree objects were checked against their declared SHA-256. Before Unity, 857 tracked source-file hashes were captured. Tests produced the seven familiar font/performance/tool-settings changes; before/after bytes were retained and originals restored. Build produced the same seven changes plus automatic removal of two XR `preloadedAssets` entries in ProjectSettings.asset. The official XR Management build processors add runtime settings for build and clean them afterwards. The owner explicitly approved restoration of that additional file; before SHA `906488e6b0e41096de357b0f45e0862d514555e6018158c9166003ccdd521890` was restored byte-for-byte from post-build `77be28d445f7051eb70ff04b877d5edf8cf11da574244921e8240a4cf04afba1`. No SDK/configuration fix or rebuild was used. README changed concurrently as authorized documentation and was preserved. All Assets/Packages/ProjectSettings now match frozen source bytes. Original/tested workspaces were not overwritten.

## Warnings and unresolved technical risks

- Clean compile emits existing CS0252 (Editor migration reference comparison) and UAC0005 (test-helper assembly enumeration). Neither is a C# error; they have not been silently suppressed.
- **TMP `m_AtlasTextures` UnassignedReferenceException occurs in TMP_PreBuildProcessor.OnPreprocessBuild and again during Editor shutdown.** The pipeline still reports Success. The relation to Player font rendering is **not verified**; check English/Chinese glyphs and task/readout visibility in the EXE. No font/SDK source was changed to bypass this issue.
- Historical UnityOpenXR native crashes / XR_ERROR_LIMIT_REACHED have not been reproduced by the latest user-reported Editor checks, but the root cause remains unproven. Short startup warnings are distinct from sustained failure. Preserve Player.log and stop on grey view/error floods.
- Model/collider dimensions are assumptions; contact warnings do not certify clearance or enforce a universal movement barrier. R5 narrative specificity remains partial; R11/R12 are deferred Could features. Resource permissions and account mappings need the named owners' confirmation.

## Immediate Player acceptance record

Run the extracted complete EXE with Quest Link. Record actual headset/PC/runtime conditions and separate outcomes for immersive view and tracking; controllers/menu/movement; G00-G11; S/M/L/Grip/Rearm/contact/haptics; measurement/Marker/save; Exit and restart with retained records/hidden historical visuals; fresh English/live Chinese; safe recovery. Enter **Not Run** where not executed. Preserve logs and real screenshots if supplied. Do not infer these from a live process, build success, or Editor tests.

Owner clarification received 18 October 2026: the completed headset regression was Unity Editor Play, not the independent EXE. Keep the generated EXE/full directory/ZIP unchanged; no rebuild is planned while waiting for the actual Player report.
