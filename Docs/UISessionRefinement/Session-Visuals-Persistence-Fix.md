# Session visuals, persistent data and error recovery

> Saved review data persists between sessions, but previous markers and measurements are hidden by default when a new review session starts.

## Actual cause and lifecycle

`ReviewTools.Start` restored the existing scene-keyed version-2 JSON, and the restore loop instantiated a visual for every restored record. These were deliberate new instances, not evidence of DontDestroyOnLoad leftovers. Parallel record/visual indices also made presentation and storage unnecessarily coupled.

The same stored records still load, save and export through the unchanged `ReviewStorage` and original path/schema. `Records` retains history. A runtime-only `ActiveRecords` collection and ID-to-visual dictionary represent only this run's creations or explicitly opened records. Startup loads history without instantiating old markers, lines or labels, without raising new-record/save completion events and without saving an empty replacement. New measurements/markers display normally; save keeps both history and new data. Ray selection and the menu use visible records only, so a hidden historical Draft cannot block a new marker. Explicit `ShowRecord`/`SelectMarker` can reopen an addressed record; no history-browser UI was added.

## Recovery audit and actual changes

| Operation | Previously available | Current formal-menu behavior | Persistent effect |
| --- | --- | --- | --- |
| Cancel unfinished measurement | `CancelPending`; menu cancel appeared only while pending | **Cancel start point / 取消起点** | Removes the unfinished preview; no completed record is created |
| Undo latest current measurement | Generic `UndoLast` deleted the last record | **Undo last display / 撤销最近测距显示** | Hides latest visible measurement created this run; retains record, IDs and saved JSON |
| Clear displayed measurement lines | Destructive `ClearAll` API existed | **Hide measurements / 隐藏测距显示** | Hides measurement visuals only; markers and disk records remain. Use Cancel start point separately for an unfinished preview |
| Remove a newly misplaced marker | Draft deletion existed without a current-run guard | **Cancel draft / 取消草稿** | Removes only this run's unsubmitted Draft, including its autosaved draft entry; never a submitted or historical review |
| Remove a saved marker from view | Generic delete removed its data and could clear links | **Hide marker / 隐藏标记** | Hides only; submitted feedback and measurement links remain |
| Delete history | Public `DeleteRecord`, `CancelMarker`, `ClearAll` APIs remain for compatibility | Not exposed by these new recovery buttons; no new history-delete feature | These legacy APIs are destructive; do not wire them to recovery actions |

Undo means undoing the **display**, not erasing a saved measurement. Stage 9, Stage 10 and repeatable Help use these exact bilingual button names. Recovery Help is a subview of the existing panel, not a new tutorial stage. Hiding a saved marker does not reset its completed Journey task.

## Stuck/movement recovery audit

No input or locomotion change was necessary. B opens the existing menu before world-ray collision rejection. **B > Settings > Teleport to Safe Position**, then the existing confirmation, uses `TrySafetyReset`: it deliberately does not require the current capsule to be clear or a walkable route from the stuck point. It still requires tracked input and a clear valid safe destination. `CommitRelocation` restores the controller and clears movement/teleport transient state. The formal Scene has a safeResetPoint reference.

The first menu teaching/safety copy and Help explain left-stick smooth movement, right-stick turning, and holding right A to aim/releasing at a green landing. B is unchanged. A blocked safe destination reports failure and preserves the scene. No unimplemented shortcut is advertised. This is a static audit; physically stuck-menu accessibility and movement after reset require Quest testing.

## Persistence verification and limits

Ten actual Unity EditMode tests use isolated system-temp storage: restore without visuals/events, hidden Draft nonblocking, cancel first point, hide-only measurement undo, hide-all visual separation, current-Draft guard, real save/reload preserving history, explicit show/idempotence, export preserving submitted history and rejection of non-temp test paths. Original user JSON was not read, cleared or migrated. JSON remains version 2, IDs and feedback text remain unchanged, and actual feedback-save acknowledgements still come from successful disk writes.

Current branch hardware/Player save-reopen is **Not Run**. See `Quest3-UI-UX-Regression.csv`; do not equate temp-file tests with final-apartment acceptance. Autosaved unsubmitted drafts remain data, not submitted feedback. Legacy destructive APIs are retained, but the formal Scene contains no legacy `ReviewPointerInput` development shortcut component.
