# Actual verification evidence

Prepared 26 October 2026. These files describe **pre-evaluation developer stabilization**, not Quest acceptance or participant-study findings.

- `EditMode-Results.xml`: actual Unity 6000.6.4f1 DX11 final suite, 186/186, zero failures/skips. `EditMode-Summary.json` includes counts, log hash, warnings and the retained TMP shutdown exception. XML raw UTC dates differ from the local China calendar date.
- `Spatial-Checks.json`: seven actual Unity imported-mesh/Physics groups.
- `Capsule-Route-Checks.json`: actual overlap-capsule grid/edge/floor queries reaching three exact goals; not a recorded human trajectory or CharacterController.Move test.
- `Unity-Before-Top-Down.png`, `Unity-Before-Oblique.png`: actual Unity camera renders of the unmodified source scene in the isolated worktree.
- `Unity-Top-Down.png`, `Unity-Oblique.png`: actual Unity camera renders after final local geometry correction. They are rendered evidence, not headset screenshots.
- `../Spatial-Plan-Measurements.svg` / `.png`: source-derived annotated plan with actual Unity query paths. It is a diagram, not a screenshot or legal certificate.

Unity-tested scene SHA-256 `412911e221b92acd6720bd12a41eb92d4e4462c4da7529c5bf6039b05d5f936f`; final committed scene `23a9813c3479d699846a03a277d9368b3482bd5eb5f70383c6f81dc0bc710da5` differs only by eleven removed trailing spaces on empty `m_Name` values, verified object-wise. No geometry change followed the final 186-test run.

Full raw Unity logs, tool attempts, before-byte backups, detailed imported-mesh JSON, offline source calculations and private historical-data protection manifests remain locally under `Logs/Phase3Stabilization-20261026/` and are excluded from commits. Historical participant/development feedback is not republished here. No PlayMode, Quest or Phase 3 Windows Player pass is implied.
