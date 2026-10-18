# Reviewed change inventory

Base: `a27dd1c6f937ed694d69e885dd2c83cf512cd9f8`. All changes are confined to the dedicated Guided Journey clone/branch. See Git diff for exact lines and the PR for final commit.

| File / scope | Purpose |
|---|---|
| `Assets/GuidedJourney/Core/JourneyStateMachine.cs` + asmdef/metas | New pure state machine, guarded movement, real action revisions, explicit skipped outcomes. |
| `Assets/GuidedJourney/Tests/JourneyStateMachineTests.cs` + test asmdef/metas | 47 actual offline core tests, also discoverable as Editor tests. |
| `Assets/GuidedJourney/Tests/AdapterLifecycleTests.cs` + meta | Two Unity EditMode callback tests; compiled, not executed in this task. Reflection avoids reverse default-assembly dependency. |
| `Assets/GuidedJourney/Tests/check_scene.py` + meta | Read-only scene graph, GUID, protected-source and narrowly bounded diff checks against base. |
| `Assets/Scripts/GuidedJourney/ApartmentGuidedJourney.cs` + folder/meta | New default-assembly observer, nonmodal card, help, actual save acceptance, designated cabinet/drop visuals. |
| `Assets/Scripts/ApartmentTutorialController.cs` | English onboarding, optional practice skip, both movement methods in practice, delegation of contextual lessons. |
| `Assets/Scripts/ApartmentMenuController.cs` | Existing Controls Help/Skip and delayed old tour popup; no new key or menu. |
| `Assets/Scripts/Review/ReviewTools.cs` | Successful disk-save acknowledgement and truthful SubmitFeedback result. No JSON schema/storage implementation change. |
| `Assets/ShoppingBag/Integration/ApartmentShoppingBagBridge.cs` | Nonbreaking SizeSelected callback on a real successful menu selection, including current size. |
| `Assets/ShoppingBag/Prefabs/ShoppingBag.prefab` | Only `dryRunInEditor: 1 → 0`; original dimensions/physics/haptic strength unchanged. |
| `Assets/ShoppingBag/Tests/phase_b_assets.py` | Align haptics assertion/generator template with hardware-capable Editor prefab; generator not run. |
| `Assets/ShoppingBag/Tests/integration_checks.py` | Validate actual dynamic font source cmap glyphs; retain pre-baked check for static fonts. No font asset changed. |
| `Assets/Scenes/Apartment_Detailed_VR.unity` | Five existing YAML documents touched and five new documents, listed below. |
| `Docs/GuidedJourney/` | Eight requested handoff reports/table plus text verification artifacts/runner. |

New folder and source `.meta` files preserve reproducible Unity references. No DLL, generated cache, compiler response file or local log is part of the source commit.

## Exact bounded scene changes

Existing documents: Tutorial GameObject `1260247394` gains observer; Tutorial MonoBehaviour `1260247395` references it; wall GOs `1261912974` and `27991925` gain their own target components; SceneRoots `9223372036854775807` registers drop Transform.

New documents: observer `2147000001`; drop GO/Transform `2147000002`/`2147000003`; wall contact targets `2147000004`/`2147000005`. Original tutorial references, object transforms, meshes, colliders, renderers, bag prefab GUID, materials and XR Rig remain unchanged. Contact targets increase from 24 to 26, solely through two independently rendered wall segments.

Before scene write, the original bytes were saved outside Git at `D:\Documents\COMP5424\LocalBackups\GuidedJourney-20261018-185120\Apartment_Detailed_VR.unity.original.bytes`; companion `scene-backup.json` contains the original SHA-256. Keep this backup outside all PRs.

## Protected and unchanged

No Packages, ProjectSettings, OpenXR asset, SDK/package cache, `.gitattributes`, original ShoppingBag A/B/C1 runtime script, input mapping, Rig, locomotion, furniture transform, collider or physical bag size changed. No active testing/original/UI worktree was written. No reset/clean/stash/force push, branch merge or automatic PR merge is permitted.
