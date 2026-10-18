# Wall-contact feedback plan

Date: 2026-10-18. Baseline `a27dd1c6f937ed694d69e885dd2c83cf512cd9f8`. Static source inspection plus the bounded scene wiring implemented in this feature branch; no Unity or Quest run. The two selected wall segments are now explicitly registered. Runtime contact, warning restoration and haptics remain Not Tested.

## Confirmed cause of missing warnings on inspected walls

`BagContactSensor.ScanNow` performs a scene-local overlap of the actual oriented bag body; its Prefab mask is all layers (`4294967295`). It only accepts solid colliders resolved by the explicit `BagContactTarget` registry. The baseline scene has 24 furniture targets and does not register the following structural walls. Therefore contacts with these walls are intentionally filtered out even though their solid collider exists. This is stronger evidence than assuming a missing wall collider or a failed MaterialPropertyBlock. It does not explain every untested wall segment automatically.

| Fixed structural segment | GO / Transform | Collider / renderer | Pose / mesh | Baseline registry |
|---|---|---|---|---|
| `DetailedEnvironment/House/Structure/WallKB` | `1261912974` / `1261912975` | MeshCollider `1261912979`, MeshRenderer `1261912978` | `(1.66,1.4,4.81)`; inline mesh `1693569302` | Not registered |
| `DetailedEnvironment/House/Structure/WallLC_4b` | `27991925` / `27991926` | MeshCollider `27991930`, MeshRenderer `27991929` | `(3.18,1.4,6.755)`; inline mesh `357620063` | Not registered |

These two inspected segments each have their own mesh filter, renderer and collider. The whole building is **not one combined renderer** for these cases. Some other wall/window/trim objects may have different topology and must be inspected individually before registration.

## Implemented bounded registration

The feature branch adds `BagContactTarget` component `2147000004` to `WallKB` and component `2147000005` to `WallLC_4b`. Each lists only its own solid collider and renderer from the table above, with the existing warning material GUID `77b608892d835a498d83230311d36980`. The explicit scene target count is now **26**, from the original 24 furniture targets. Every original collider, renderer, mesh and Transform is unchanged. There is no target attached to the whole `House`, no scene-wide scan, no new collision geometry, and no shared material edit.

`BagContactTarget` already builds a runtime duplicate mesh overlay using the red warning material. The source materials and existing cyan hover renderer are untouched; the overlay disappears when contacts clear. No MaterialPropertyBlock integration is required for this implementation. A multi-submesh segment would receive the overlay on every submesh; that is a whole *segment* warning, not a precise contact spot. If an inspected renderer spans an unacceptable part of the building, omit it and use a short nonblocking HUD contact message or a separate local indicator instead.

Label the feedback as **fixed structural wall contact**. Red indicates a clearance concern for design review, not a promise that the wall is editable. Do not demand a collision for guided-task completion. Retain separate furniture names/categories in feedback. Do not register floors, UI, teleport discs or helper objects.

## Runtime acceptance still required

- Hold the Small bag in each hand and touch only the registered wall segment: that segment gives clear feedback, adjacent unrelated walls/floor stay unchanged.
- Touch wall and furniture together: both warnings remain active until their respective contacts end; one exit must not clear the other.
- Walk away, release Grip, open menu, lose focus, disable the target and exit Play: overlays clear and no continuous haptic output remains.
- The same original wall still blocks player/measurement/marker rays correctly; no new solid geometry or movement system is created.
- If a hover-capable object is later registered, cyan hover returns correctly after contact without modifying its materials.
- Player build and Link Editor behavior must both be tested. A static reference check is not a true contact or haptic PASS.

## Change boundary and rollback

The implemented runtime change is confined to two added `BagContactTarget` components and their component-list references on the chosen scene GOs. The existing collider, renderer, mesh, pose, GUID and material data are preserved. The added components resolve statically; this does not prove that runtime contact or overlay restoration passes. Review the scene diff before commit. If rollback becomes necessary, remove only these two added components and their two GO component-list entries while Unity is closed, or revert the reviewed feature commit through normal review. Do not reset, clean or overwrite a teammate's later scene changes.
