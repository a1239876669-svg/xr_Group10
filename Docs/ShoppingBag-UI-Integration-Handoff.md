# ShoppingBag → UI integration handoff

## Scope and provenance

- PR base: `fzj/interaction-ui@a976cbd16a5067c5a33da9fbff83aa340b7b5dde`.
- Integration branch: `feature/shopping-bag-ui-integration`; original worktree remains on `feature/shopping-bag-interaction@40193be1b737b6abd94d2e82ea047d701b7a88f9`.
- Verified module source: A `470468d` → `20be7f0`; B `5769964` → `295e199`; C1 `40193be` → `e5f5ab1`. These three commits contain ShoppingBag assets only; SDK205 versions agree. Core implementation, GUIDs, prefab and asmdefs are preserved.
- Package sources differ: UI uses registry205; the original bag branch uses machine-local tarball paths. The UI manifest/lock and all ProjectSettings/OpenXR assets are unchanged. Do not transfer the original machine's local settings as part of this PR.
- Only the UI owner may decide to merge this PR. No auto-merge. Existing Draft PR #2 stays open and unchanged.

## What is connected

`Assets/ShoppingBag/Integration/ApartmentShoppingBagBridge.cs` belongs to default `Assembly-CSharp`, outside `Scripts`' asmdef. The bag assembly remains auto-referenced and has no dependency on apartment scripts. NUnit module tests use reflection to exercise the adapter without creating reverse references.

`ApartmentMenuController.shoppingBag` supplies the existing **我的物品** page (index3). Three existing-style panel buttons call `TrySelectSize(BagSize)` on the adapter, which calls the real `ShoppingBag.TrySetSize`; it never scales the root. Current size and selected state are read directly. Held bags show noninteractive labels plus `Held - release first`; callbacks recheck holding, page, visibility and configuration. Release and size events mark the page dirty; one LateUpdate refresh is coalesced. `RefreshMyItems()` updates only the open items page and resets existing pointer input. Other pages, B and right Trigger retain their owners. Reopening always reads current state.

The Bridge synchronously provides `menu.BlocksWorldInput`, `!tutorial.AllowInteraction`, `locomotion.IsTeleportAiming` to C1. Existing C1 still owns OVR/application focus, tracking, A/B intent, release and per-hand rearm. Optional `additionalContext` accepts another `IBagInteractionContext` for later Review text editing; no missing Review classes are referenced on this UI baseline. A supplied but invalid/destroyed context fails closed. An unbound/disabled Bridge blocks new acquisition, never forcibly releases a held bag.

Menu-open blocks Grip but intentionally does not block resizing an already released bag. Do not gate size buttons on global `CanAcquire`, since an open menu is itself a blocker.

## Ready-wired Editor test entry

Open **only** `Assets/ShoppingBag/Integration/ShoppingBag_UI_Test.unity`, then Play. It contains one existing bag prefab, registered test targets, stand/floor and a Bridge/Bootstrap, with no Camera, Rig, EventSystem, desktop mouse-holder or OnGUI size panel.

`ShoppingBagUITestBootstrap` loads the unchanged `Assets/Scenes/Apartment_Detailed_VR.unity` additively in Editor, unless already loaded. It resolves exactly `VRPlayer/ApartmentMenu` in that scene, binds the bag/Bridge, reuses the menu's tutorial/locomotion, and moves test geometry relative to the player's initial pose. It sets the UI scene active for lighting. No full-scene furniture search or second rig is used. The serialized prefab Gate context points at the Bridge before startup, so acquisition is blocked while loading. An invalid path/duplicate owner yields a clear error and remains blocked.

This is an **Editor test fixture**, not the final Windows Player scene. Its Editor-only scene loader does not require Build Settings changes. In a Player the UI scene must already be loaded or the bootstrap reports the pending setup instead of spawning another rig. Do not add the fixture to the final Build.

Inspector wiring is already saved in the new fixture: Bridge.bag/gate, Bootstrap.bridge/fixtureRoot/path, and Gate.contextProvider. UI references are bound at startup because Unity cannot persist cross-scene references. No manual Inspector work is required merely to try this fixture. If the UI owner renames the exact menu path, update that one serialized path and the static checker.

Finish/skip the existing welcome/tutorial using its original controls, press B, choose 我的物品, and use the original right-hand pointer/Trigger. On the fixture bag temporarily uncheck `BagHaptics.dryRunInEditor` in Play to test hardware vibration; do not save this to the source prefab. Test objects may overlap parts of the apartment; relocate the **fixture root** in a scratch test if necessary and record it. This placement is not the final Entrance SpawnPoint.

## Verification actually completed

- Offline Roslyn compilation using Unity6000.6.4f1's compiler and existing SDK205 references: ShoppingBag runtime (12 files), default runtime (16), default Editor (15), EditMode test assembly (4), PlayMode test assembly (2): all exit0, no compiler errors.
- Read-only `asset_checks.py`, `phase_b_assets.py`, `integration_checks.py`: pass. Checks include preserved module metadata, test geometry, local fileIDs, context override, exact UI path, no extra Camera, no desktop simulator and all new UI glyphs in the existing static font.
- `git diff --check`: pass. Existing apartment scenes, Packages, ProjectSettings and XR settings are unchanged relative to UI base.
- Unity Editor import/compilation: **Not Run**. Several existing Unity processes were detected; no extra Editor or package-import job was launched. Offline compilation does not prove Unity asset import.
- EditMode execution: **Not Run**. Eight new parameterized NUnit cases cover real profile changes, held/stale callbacks, unavailable contexts, page refresh and serialized fixture. Historical module78/78 results are not new integration results.
- PlayMode execution: **Not Run**. Existing two simulated cases compile; historical2/2 does not prove this scene.
- UI human acceptance and Quest3 joint acceptance: **Not Run**. The original isolated module had user-reported hardware passes (five grips/releases per hand, rearm, feedback, both-hand vibration and two-Play tests); that is not UI joint acceptance.
- Historical UnityOpenXR native crashes have not recurred in the reported isolated retest, but their root cause is not proven fixed. Short startup warnings can occur. Original bag-worktree local OpenXR fixes are intentionally not transplanted; the UI branch's own OpenXR settings still require its owner's validation if it reports problems. Do not Fix All or change packages to make this PR appear verified.

## Immediate joint acceptance (all pending)

1. Open/close the original menu and enter 我的物品; Small/Medium/Large visible.
2. Select each: same bag instance, body/visual/sensor dimensions and current selection agree.
3. Grip with each hand; held size buttons inactive; stale callbacks cannot resize.
4. Release while menu is open; refresh restores sizing without moving/resetting the bag.
5. Menu open blocks acquisition; close while Grip held does not auto-grab; release/press each hand restores it.
6. B+Grip and right Trigger UI clicking never select the bag accidentally.
7. Tutorial and teleport aiming block new acquisition; release still works; unchanged movement modes and other pages remain accessible.
8. Registered test contacts turn red/restore independently; both hands get short real vibration; no residual pulse on release/focus loss.
9. Same Editor: Play, stop10s, second Play; record Session/display behavior and warning growth. Stop on gray screen or LIMIT_REACHED; preserve logs.

## Final apartment integration, for the team owner

After review/merge into UI, combine that reviewed UI lineage with measurement/marker/Review and comfort work on the **team's chosen final branch**, not by merging this PR to main automatically.

1. Add one existing `ShoppingBag.prefab` to the final scene; root scale1; place bottom origin on a stable reachable entrance surface, clear even for Large. Do not use the Editor fixture floor/walls as official apartment furniture.
2. Add one Bridge; assign bag, existing Gate, menu, tutorial and locomotion; assign `menu.shoppingBag` and serialized `Gate.contextProvider`. Never Apply scene references back to the shared bag prefab. No automatic respawn or second BagManager is introduced.
3. Manually register chosen actual furniture `Collider[]` and `Renderer[]` on `BagContactTarget` with `ContactWarning.mat`, once per collider and within target subtree. Exclude Rig, UI, floor, rugs and teleport helpers. Red must override cyan hover and restore the current hover/default state. Actual furniture registration remains pending.
4. Supply Review `IsEditingText` through a small default-assembly `IBagInteractionContext` adapter. Do not reference nonexistent Review classes on this baseline, and do not block the entire measuring/marking mode as if it were text editing.
5. Preserve the newest team's menu/Review API; manually reconcile the items-page and RefreshMyItems changes rather than replacing the menu file wholesale. Comfort/Review branches have different panel/button APIs.
6. If ShoppingBag already exists, compare its version/tree to40193be and keep one implementation with the original GUIDs; do not duplicate-import or blindly cherry-pick A/B/C1 again. Cherry-picked hashes differ, so final Git-history reconciliation belongs to the integration owner; PR #2 is not silently closed or treated as already merged.
7. Validate onboarding→apartment→bag→movement/compare→measure/marker→feedback→save→exit→reopen in one Windows Player. Persistence, text entry, comfort route, final furniture and Windows Build are out of this PR's acceptance scope.