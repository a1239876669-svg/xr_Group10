# Bag haptics: diagnosis and limited configuration fix

Date: 2026-10-18 (Asia/Shanghai). Workspace: `xr_Group10-guided-journey` only.

## Evidence and interpretation

The user reported red furniture-contact feedback but no obvious physical vibration in the final apartment. That is a hardware observation; it does not establish a general Meta SDK or OpenXR fault.

A definite Editor-side suppression was found in `Assets/ShoppingBag/Prefabs/ShoppingBag.prefab`, BagHaptics component fileID `1009`: `dryRunInEditor: 1`. The formal apartment has no override for this property. `Assets/ShoppingBag/Scripts/BagHaptics.cs` computes `dry = Application.isEditor && dryRunInEditor` and issues no `OVRInput.SetControllerVibration` command while `dry` is true. It can still publish a simulated haptic request. A red highlight therefore does not prove that a physical command was sent.

This flag suppresses Quest Link vibration when running inside Unity Editor. It does **not** suppress a Windows Player: `Application.isEditor` is false there. It cannot by itself explain an absent vibration in a standalone executable. The final Player has not been tested here.

## Applied change

Only the serialized prefab property changed: `dryRunInEditor: 1` to `dryRunInEditor: 0`.

Unchanged: amplitude `0.35`, frequency `0.5`, duration `0.08` seconds, cooldown `0.25` seconds, `logRequests: 0`, contact sensor reference, original prefab/MonoScript GUIDs, SDK, controller mapping, physics, and all haptics source code. No scene override or new haptics system was added. Desktop simulation still does not send commands because it is not an SDK-held bag.

Rollback: set this one prefab property back to `1`, or apply the inverse of its reviewed one-line Git diff. Do not reset the worktree or overwrite unrelated changes.

## Existing command and cleanup conditions

A new contact must have a registered target, an active contact sensor, and a known held hand. The existing SDK-command guard additionally requires all of the following:

- Actual Meta SDK ownership (`IsSdkHeld`), rather than desktop simulation.
- Application focus and the held controller present and connected.
- `OVRManager.loadedXRDevice == OVRManager.XRDevice.Oculus`.
- Both `OVRManager.hasInputFocus` and `OVRManager.hasVrFocus`.

The pulse targets only the held side (LTouch or RTouch). The existing cooldown limits new pulses. Timeout, release/contact clearing, component disable/destruction, application pause, or application focus loss stop a running pulse. This change does not increase pulse strength or duration and does not change the Grip safety gate.

If vibration is still absent after the flag fix, inspect the existing `BagHaptics.Requested` event's `HardwareCommandIssued` value in a controlled diagnostic session, actual held hand, controller connection, and the above OVR focus/runtime guards. A request with `HardwareCommandIssued == false` is not physical-haptics success. Do not bypass these safety guards, suppress OpenXR warnings, or alter PackageCache to claim a fix.

## Manual Quest 3 acceptance (Not Tested in this change)

Use the new development clone only after approved local OpenXR setup. Keep the old active test workspace untouched.

1. On Quest Link, hold the bag with the left Grip and touch a registered furniture target: only the left controller should give a short pulse.
2. Leave the target and repeat with right Grip: only the right controller should pulse.
3. Maintain one continuous contact: it must not become a continuous vibration. Leave/re-enter to check the existing cooldown.
4. Release the bag during/after contact: pulse must stop and the next grab must work.
5. Open/close the menu, then remove/recover headset focus: no stuck pulse; Grip rearm remains required after blocking.
6. In a Windows Player, repeat the same checks. The Editor-only flag does not validate Player behavior.

Static evidence: the property was the sole prefab change, and the formal scene contains no `dryRunInEditor` override. No Unity Editor, Play, Windows Build, or Quest session was started by this task. Hardware resolution remains **Not Tested**, even though the configuration error is corrected.

## Related feedback limits

This fix does not register walls or change whole-wall materials. Red feedback remains determined by the explicit contact-target registry. Wall feedback and cabinet-storage feasibility are handled separately; haptics is not a substitute for those implementations.
