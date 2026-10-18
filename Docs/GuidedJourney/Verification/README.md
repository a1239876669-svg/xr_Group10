# Executed verification evidence

These text records describe actual offline checks on 2026-10-18. They do not claim Unity Test Runner, Play, Quest or Player acceptance.

- `core-test-results.txt` and `core-execution.json`: 47/47 pure-state test methods using NUnit assertions and Unity-shipped Mono, with source hashes. Only `JourneyStateMachineTests.cs` is executed; the separate Unity adapter lifecycle tests are not.
- `offline-compilation-results.json`: seven successful Roslyn compilations against existing local Unity/SDK references, with exact source hashes. This is not a fresh package import or build. Compiler response files and binaries remain in ignored local Logs.
- `static-checks.json`: four Python checks and captured real output, including protected baseline scene/source comparison.
- `yaml-parse.json`: YamlDotNet parsed the ten changed/new Unity YAML documents; runtime scene deserialization remains pending.

## Reproduce pure core tests without starting Unity

From a PowerShell session, run `& .\Docs\GuidedJourney\Verification\Run-Core-Tests.ps1` in the repository. Optionally pass `-UnityEditorDirectory` pointing at an existing compatible Unity Editor directory. This uses its Mono, C# compiler and bundled NUnit; it installs nothing. It creates a new ignored Logs directory with compiled test artifacts and text results, and never deletes or overwrites an earlier test run. `CoreTestRunner.cs` runs only the 47 pure test methods and checks their count. It does not implement NUnit fixture/setup/parameterized-test discovery; extend the runner if the suite adopts those features.

## Reproduce read-only asset checks

Run Python with `-B` on each of: `Assets/ShoppingBag/Tests/asset_checks.py`, `phase_b_assets.py`, `integration_checks.py`, and `Assets/GuidedJourney/Tests/check_scene.py`. The scene contract uses the frozen audited base SHA, so it works after commit as well as before it.

For actual Unity acceptance, manually open the new clone after the active Quest session ends, resolve its separately approved local XR setup, run all applicable Editor/PlayMode suites in Test Runner, and execute the 33-row regression CSV. Keep all unexecuted cases Not Run. Shared review JSON may live outside the project: preserve existing sessions before any storage/restore/failure test.
