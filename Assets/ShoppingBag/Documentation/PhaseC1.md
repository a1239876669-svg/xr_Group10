# ShoppingBag C1 Grip safety gate

C1 changes only the reusable ShoppingBag module. It does not connect the apartment menu,
tutorial or review controller. Those connections remain C2. No rig, scene, package or shared
configuration is changed. Quest 3 acceptance remains pending.

## Processing order and verified SDK APIs

Meta Interaction SDK 205 in this project drives Interactor/InteractorGroup from Update.
GrabInteractor.ComputeCandidate enumerates GrabInteractable.Registry.List(this).
InteractableRegistry.Include calls CanBeSelectedBy, which runs the prefab's BagGripFilter,
before the normal ShouldSelect/Select path. SDK Postprocess clears queued selector events.
SDK ShouldUnselect/Unselect is a separate path and is not gated here.

ApartmentMenuController on the integration branch runs LateUpdate at order 8500. A cached
menu-open boolean would therefore be too late for B and Grip in one frame. C1 reads current
OVR B (Button.Two, RTouch) and A (Button.One, RTouch) in the filter path as conservative
menu/teleport intent blockers. It also checks the registered right SDK controller's matching
SecondaryButton/PrimaryButton state. Neither button is rebound or acted upon by C1.

Selection overrides such as ForceSelect can bypass normal SDK candidate discovery. They
are outside this module's contract and must not be used to acquire the bag in C2.

For every new SDK candidate query:

1. Reject non-controller/non-near-grab candidates as before. An existing held owner passes
   the filter; another interactor cannot take over. No forced release is issued.
2. Read current application, OVR input/VR focus, head and individual controller tracking,
   Grip, and A/B intent. Unknown data or input exceptions fail closed.
3. Read current IBagInteractionContext.Blockers synchronously and combine all blockers.
   A missing, disabled, destroyed or throwing previously bound context fails closed.
4. Merge registered SDK controller validity and Grip with raw OVR input. Either source
   reporting pressed prevents a neutral sample. A newly seen controller cannot inherit
   arming from raw input alone. Continue sampling known controllers away from the bag.
5. Update BagGripRearmState separately for left and right. Any global blocker disarms
   both. Invalid controller tracking disarms only that hand; invalid head tracking blocks both.
6. Require a tracked neutral sample in an unblocked frame after the last blocked frame,
   and a later frame before acquisition. Repeated queries or clear/release/press in the
   blocked frame cannot arm. Do not cache permission for the remainder of a frame.

Armed means eligible for SDK candidate discovery, not a synthetic select. ControllerSelector
still owns Grip selection/release. No candidate or selection override is installed on the rig.
The SDK must deliver its release event; C1 does not fabricate input when hardware data is absent.

## Integration contract for C2

Implement IBagInteractionContext in a default-assembly apartment adapter, outside the bag
asmdef. Bind it once with gate.BindContext(adapter), or assign the contextProvider field.
Use the existing gate on ShoppingBag.prefab. Do not add another rig or input owner.

The provider's getter must evaluate current state, including:

| Source | Blocker |
|---|---|
| Menu BlocksWorldInput, including neutral recovery and modal panels | Menu |
| Active tutorial with !AllowInteraction, not just ModalVisible | Tutorial |
| Locomotion IsTeleportAiming and any relevant transition restriction | Teleport |
| Review controller text editing / future world keyboard activity | TextEditing |
| Adapter not initialized or dependencies unavailable | ContextUnavailable |
| Additional global tracking/input readiness restriction | Tracking / InputFocus |

Runtime native sampling already handles application/OVR focus and tracking. Do not return
None for an adapter that is not ready. Do not rely solely on apartment LateUpdate to copy
the state. Existing SetMenuOpen, SetTutorialOpen and SetTeleportAiming entry points remain;
SetTextEditing, SetTrackingValid and SetInputFocused are also available. Multiple blockers
combine rather than overwriting each other. Prefer one complete context provider in C2.

Blocking acquisition must not disable GrabInteractor, ControllerSelector or GrabInteractable.
Release must remain available while a bag is already held. To reacquire after a blocker,
release Grip after the blocker has cleared, wait a clean frame, then press again.

A/B's Editor mouse simulation remains available without a headset. ShoppingBag.AcquisitionAllowed
continues to expose semantic blockers for that simulator. Real SDK acquisition additionally
requires Gate.CanAcquire and per-hand rearming; the mouse simulator is not evidence of this path.

## Automated verification

BagGripSafetyTests covers every blocker flag, both hands at startup, independent rearming,
invalid tracking vs neutral, overlapping blockers, same-frame edges, stale samples, missing
or failing context/input, gate disable/removal, SDK/raw disagreement, and non-Grip rejection.
It also exercises the real SDK CanBeSelectedBy eligibility API and queued Unselect path.
These are EditMode tests with explicitly driven SDK lifecycle in isolated preview scenes.
The existing Phase A/B tests run unchanged in the same suite.

BagGripGatePlayTests uses a test-only Unity IController and the actual prefab, physics,
ControllerSelector and GrabInteractor Update path, with no ForceSelect/candidate override.
Both left and right test: B/Grip intent, menu close while held Grip, invalid-tracking release,
valid release and re-grab, owner identity, continued hold under stacked blockers, and release
while blocked. The test controller is excluded from non-Editor builds. It is not a rig.

Local result logs are under Logs/ShoppingBagC1-20261016, outside committed assets. Report
EditMode and PlayMode counts separately. Neither suite establishes hardware vibration,
headset tracking fidelity, stereo rendering, usability or complete apartment integration.

## Quest 3 acceptance priorities

1. Near the handle, press B and Grip together on either hand. No new acquisition.
2. Keep Grip held through closing a menu or recovering tracking/focus. No automatic pickup.
3. After recovery, release only left Grip and try both hands; right must remain disarmed
   until it separately releases. Repeat with hands reversed.
4. While holding, open a blocking view and release Grip. The bag must drop normally.
5. Cover/uncover a controller, remove/replace the headset or use the system overlay;
   confirm reacquisition requires a valid new neutral Grip sample.
6. Verify ordinary near-grab/drop/re-grab, single-hand ownership, sizes, contact red/restore
   and owner-hand vibration from A/B. Disable Editor dry-run explicitly for real haptics.

Menu/tutorial/typing state tests in the formal apartment require the C2 provider. Raw B/A
guards alone do not tell C1 that an already-open menu remains open after B/A is released.
Record those cases as pending wiring, not as passed. Stop after C1; do not silently add C2.
