# Tutorial state transitions

All conditions below are implemented observations; scene/runtime acceptance is pending. `Tick` advances at most once. Every explicit skip records Skipped, not Completed. Help/skip are on B → Controls / 按键说明 → Review guide. New stages cannot be completed by a generic Next button.

| Stage | Real success condition | Recovery / exclusions |
|---|---|---|
| 0 Welcome/movement | Existing basics reach Step 3 after neutral controls | Welcome can skip all; Help can skip basic practice. Both movement methods available by default. No need to force a mode choice. |
| 1 Entrance | Entrance door reaches Open, or visitor already genuinely inside Living/Kitchen/Balcony | Already-open/inside recognized; early active tools must be stopped before Trigger controls the door. |
| 2 Living exploration | Living Room visited, then 2 m valid post-move distance **or** natural approach within 1.65 m of balcony door | No head sway, turning, blocked-controller displacement, safety reset or teleport mileage. Skip allowed; no fixed-distance treadmill requirement. |
| 3 Balcony | The explicitly wired sliding door reaches Open | Nearness alone does not pass. Paused/blocked instruction explains step clear/resume. Already-open state accepted. |
| 4 Cabinet practice | Actual Kitchen observation and designated cabinet Closed → Open → Closed | Closing/Opening/Blocked/Paused are not Closed. Close first if already open. No bag required; loss of availability invalidates half-cycle. |
| 5 Return Entrance | Actual tracked position resolves to Entrance | Any safe route including A teleport. Safe reset counts only when actual resulting position is in Entrance. |
| 6 Select/pick up | New successful My Items size-click after entry, then real SDK-held bag | Same-size click counts; no stale previous choice. Existing held-size lock, menu gate, one-hand ownership and separate-hand Grip rearm retained. |
| 7 Carry Kitchen | Held bag observed in Living, then held arrival in Kitchen with ≥0.5 m valid carrying movement; alternatively a genuine held A teleport ≥0.5 m into Kitchen | Release prompts re-grab without resetting all progress. Safety reset is excluded. No collision required. |
| 8 Kitchen floor drop | Held proof for this task, then actual release; whole body horizontally in designated area, bottom near floor, settled 0.35 s; visitor in Kitchen | Explicit simplified alternative, not cabinet storage. Bag cannot be held; Stage 4 cannot satisfy this. Focus loss requires re-grab/release proof. |
| 9 Measurement | New valid finite two-point measurement record after entering stage | Existing/invalid/one-point preview and marker records do not count. Cancel/retry/skip available. |
| 10 Feedback | Fresh marker created in this stage, valid feedback Submitted, actual disk save succeeds | Earlier draft instructions; failure retains input and does not advance; restored/old saved markers and measurements excluded. |
| 11 Free exploration | All preceding outcomes recorded as Completed or Skipped | Completion wording distinguishes skipped tasks. Existing movement, bags, doors, measuring, marking, review and exit remain available. |

## Scene wiring

Observer component `2147000001` is on existing Tutorial GO `1260247394`. References: tutorial `1260247395`, menu `184434899`, locomotion `542895441`, interactor `742092206`, room map `854460277`, review tools `854460278`, bag bridge `1082077889`, entrance door `171350835`, balcony door `527763517`, cabinet `154016231`. The former bag hint `1588222421` is dismissed only when the configured guide starts.

Drop marker GO `2147000002`, Transform `2147000003`: `(2.1,0.012,5.5)`, area `(1.30,1.00,0.90)` m; no collider. Horizontal containment is X `[1.45,2.75]`, Z `[5.05,5.95]`; body bottom must be within floor Y −0.04/+0.12 m and body top within area. This is static geometry selection, not a verified physical settling test.

## Session and interruption rules

The guide resets per scene/session and does not persist into review JSON. Loss of focus/tracking/availability suspends observation. Explicit pause/disable callbacks invalidate partial cabinet/drop evidence even if no frame is delivered while unavailable. Revision baselines prevent replaying actions performed while unavailable. Returning from the menu preserves completed guide stages and the existing C1 release-before-regrab requirement.

Fresh feedback saved, full tutorial completed and exit requested are independent. Skipping a task cannot manufacture a door event, measurement, saved record or cabinet-storage result. A user may explore out of order and return to the pending target; already-open entrance/balcony and arrival regions avoid unnecessary reversal. Current cabinet practice intentionally requires a new full cycle.
