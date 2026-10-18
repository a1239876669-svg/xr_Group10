# Tutorial panel comfort fix — review candidate

## Identified cause

The Guided Journey task card explicitly placed its centre about **0.57 m left and 0.32 m below** the head-forward position, then repeatedly followed that offset. Its normal copied head-forward rather than facing the eye from the laterally offset position. The different left/right viewing distances explain the reported oblique appearance. This is a source-level explanation consistent with the user's experience, not a new headset measurement. The separate basic Tutorial also used its own repeated positioning path. Formal Scene/Rig hierarchy is unchanged.

## Minimal implementation

`Assets/Scripts/GuidedJourney/GuidancePanelPlacement.cs` moves only the tutorial Canvas. `ApartmentTutorialController` and `ApartmentGuidedJourney` reuse it. The World Space panel is initially straight ahead, 0.10 m below eye level, with its normal aimed at the viewer and zero inherited roll. Its distance is calculated from actual panel width at 0.001 world scale, targeting 34 degrees horizontally, then limited to 1.3–1.6 m (870-wide task card approximately 1.42 m; 900-wide tutorial approximately 1.47 m).

The panel stays still for small head movements. It recentres only after over 0.45 m translation, over 30 degrees rotation, unsafe distance or leaving the view, with a 0.6 s minimum interval. Pitch is bounded to 35 degrees and vertical gaze has a finite fallback. The Canvas alone is detached from inherited nonuniform scale/rotation; its owner, camera, tracking, height, controller and locomotion are not moved. Existing owner teardown disposes the Canvas.

Full task cards hide while a menu, modal tutorial, teleport aim, world-interactable hover or active review tool needs the view. Active measurement/marker tools retain a compact, lower-view hint with the current operation and real tool readout; stopping the tool returns the full task card. The compact hint also respects menu, modal, focus and teleport restrictions. No new popup or compulsory G stage was added. The existing task-card preference and Help remain available.

## Verification

Six actual Unity EditMode placement tests passed: initial centre/facing, equal side distances, stable small motion, turn/walk recenter, immunity to parent scale and finite vertical gaze. Journey core source and formal Scene/Prefab bytes remain unchanged from `07ba53a`.

Quest 3 binocular comfort, actual door/Grip occlusion, both-language fit and wall intersections are **Not Run** for this branch. There is no new environmental occlusion solver; validate in the formal apartment before acceptance. Recheck G00–G05, then complete pending G06–G11. User-reported G00–G05 Pass applies only to prior `07ba53a`.
