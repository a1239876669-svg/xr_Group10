# Guided apartment review journey — implementation and handoff

2026-10-18. Development clone: `D:\Documents\GitHub\xr_Group10-guided-journey`. Branch: `feature/phase2-guided-journey`; base `a27dd1c6f937ed694d69e885dd2c83cf512cd9f8` (`merge-apartmentguidence`). The base was checked again before publication. This is reviewable implementation, not a hardware acceptance claim.

## Implemented route

Welcome/movement → Entrance door → Living-room exploration → Balcony sliding door → Kitchen cabinet open/close → Return to Entrance → Choose S/M/L and Grip pickup → Carry through Living Room to Kitchen → **Kitchen floor drop** → Real measurement → New marker and successfully saved feedback → Free exploration/menu exit.

The floor-drop task is the declared Stage 8 alternative, **not cabinet storage**. A single inspected cabinet leaf is approximately 0.468 m wide while Large is 0.60 m wide. No bag was shrunk, no collision disabled, and no cabinet geometry changed. The cyan floor outline is visual only. The entire actual body bounds must fit the configured horizontal area, be released near the actual floor, and remain settled for 0.35 s. The user must also be in Kitchen. Clearance and physics still require Play/Quest validation.

## Reused systems and dependency direction

- `ApartmentTutorialController` keeps the existing welcome/modal/basic-practice input route. Smooth movement and A teleport are both allowed by default; deliberate existing movement-mode settings remain respected. B still opens/closes the menu; right ray + Trigger selects; Grip remains bag-only. Basic practice is skippable through the existing Controls page.
- `Group10.GuidedJourney.Core` is pure C#, with no Unity or apartment dependency. Its one-way snapshots drive distinct Completed/Skipped outcomes and at most one transition per tick.
- Default-assembly `ApartmentGuidedJourney` observes explicitly serialized room/door/bag/review references. It depends on the core and existing apartment/bag APIs. The reusable ShoppingBag assembly never references Assembly-CSharp.
- `ApartmentMenuController` adds Help/Skip to its existing Controls page, not a new menu. Old contextual door popups and premature tour-completion popup are suppressed while this guide owns the sequence. Selected tools still own Trigger; guidance tells the user to stop an early measurement/marker tool before door practice.
- The scene retains its original XR Rig, input mapping, locomotion, character capsule, colliders, furniture poses, bag size/physics and review storage schema. Original A/B/C1 runtime scripts remain byte-identical to the base.

## Observation order and lifecycle

Existing menu runs at 8500, tutorial at 9000, locomotion at 10000 and furniture/review interaction at 11000. The observer runs at 12000. It subscribes to actual post-CharacterController `Walked`, typed relocation, successful size-selection callbacks, real `RecordCreated`, and newly acknowledged `FeedbackSaved` events. No whole-scene polling or new input listener is added.

Rooms use the existing `ReviewRoomMap` and the tracked head's horizontal world position. Distance uses actual locomotion-reported displacement; head sway, rotation compensation, blocked motion, discontinuities and safety resets are excluded. Ordinary A teleport can provide a real held-bag arrival from Living Room to Kitchen without being counted as walk mileage. Losing focus/tracking or disabling the observer invalidates partial cabinet/drop proofs. Door intermediate/blocked states cannot count as Closed.

Task cards and the cabinet label have no collider and hide during menus, modal onboarding, unavailable input and teleport aiming. The card sits below/left of central view. Comfort/readability and dynamic-font rendering are runtime checks, not proven by static layout inspection.

## Real feedback-save acknowledgement

`ReviewTools.SubmitFeedback` now returns the actual disk-save result, including when autosave is disabled. It preserves entered data and selection on failure. `FeedbackSaved` is emitted only after `ReviewStorage.Save` succeeds. The observer accepts only a fresh marker created during Stage 10, subsequently Submitted and saved without error. Restored records, old drafts, measurements and ordinary autosaves cannot fake this completion. Earlier drafts receive explicit recovery instructions. The version-2 JSON format, tmp/bak mechanism, filename and storage location are unchanged.

Guide progress is session-only and is intentionally not mixed into review JSON. Restarting a session restarts the guide; restored review data remains review data. Tutorial completion, confirmed feedback save and application exit are separate events. Existing exit behavior is retained; a global save-on-exit failure guard was not added and needs explicit manual acceptance.

## Bounded feedback changes

1. Bag prefab `dryRunInEditor` is now false so Quest Link in Editor can issue the existing guarded owner-hand pulse. This does not prove a physical pulse or fix native OpenXR stability.
2. Only structural wall segments `WallKB` and `WallLC_4b` gain explicit contact targets; their own renderers/colliders are reused. Other walls remain unregistered. No shared material or whole-building highlight is changed.
3. Furniture geometry/colliders remain unchanged. See the separate diagnosis before proposing any physical edit.

## Actual verification and safe handoff

- 47/47 pure-state NUnit assertion tests executed with Unity-shipped Mono outside the Editor; **not Unity Test Runner**.
- Seven assemblies successfully compiled offline against locally available Unity/SDK references, including the new two-source guide test assembly. This checks C# API compatibility; **not a fresh Unity import or build**.
- Four Python asset/integration/scene checks passed. YamlDotNet parsed every changed/new scene document (10). Original scene transforms/colliders/Rig and protected configuration were checked against Git base.
- Two new adapter lifecycle tests are compiled but **not run in Unity**. Existing A/B/C1 EditMode/PlayMode suites were compiled, not re-executed here.
- Unity import, Play, Quest, performance and Windows Player: **Not Run** in this branch. Prior user-reported T01–T06 are baseline observations, not new results.

Text evidence and a reproducible core runner are under `Verification/`. `Quest3-Guided-Journey-Regression.csv` is the unexecuted manual acceptance record.

When the active Quest session is finished, close its Editor normally. Open this clone with Unity 6000.6.4f1, then open only `Assets/Scenes/Apartment_Detailed_VR.unity` (no additive test scene). This fresh clone does not contain the other workspace's local OpenXR repair. If the known OpenXR Feature null error recurs, stop and arrange its separately approved local repair; do not click Fix All or mix configuration into this feature PR. Check Console, run the Editor tests, then follow the CSV in Link and later in a Windows Player.

## Rollback

Review or decline the feature PR without merging it. For a local scene-only rollback, close this clone's Editor and restore the original scene bytes from the recorded external backup after verifying its SHA; preserve any subsequent user edits first. For a merged feature, use a separately reviewed inverse/revert commit rather than reset/clean. Do not restore an old scene over teammates' newer scene work. No active testing workspace was edited.
