param(
    [string]$UnityEditorDirectory = 'D:\Software\Unity\6000.6.4f1\Editor'
)
$ErrorActionPreference = 'Stop'
$taskRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$taskMono = Join-Path $UnityEditorDirectory 'Data\MonoBleedingEdge\bin\mono.exe'
$taskMcs = Join-Path $UnityEditorDirectory 'Data\MonoBleedingEdge\lib\mono\4.5\mcs.exe'
$taskNUnit = Join-Path $UnityEditorDirectory 'Data\Resources\PackageManager\BuiltInPackages\com.unity.ext.nunit\net472\unity-custom\nunit.framework.dll'
$taskCore = Join-Path $taskRepo 'Assets\GuidedJourney\Core\JourneyStateMachine.cs'
$taskTests = Join-Path $taskRepo 'Assets\GuidedJourney\Tests\JourneyStateMachineTests.cs'
foreach ($taskRequired in @($taskMono,$taskMcs,$taskNUnit,$taskCore,$taskTests)) {
    if (-not (Test-Path -LiteralPath $taskRequired -PathType Leaf)) { throw "Missing local prerequisite: $taskRequired" }
}
# New output only. Never deletes caches, starts Unity, loads a scene or executes XR/native calls.
$taskOut = Join-Path $taskRepo ('Logs\GuidedJourneyCoreTests-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
if (Test-Path -LiteralPath $taskOut) { throw "Output already exists: $taskOut" }
New-Item -ItemType Directory -Path $taskOut | Out-Null
Copy-Item -LiteralPath $taskNUnit -Destination (Join-Path $taskOut 'nunit.framework.dll')
& $taskMono $taskMcs '-target:library' '-langversion:latest' ("-out:" + (Join-Path $taskOut 'Core.dll')) $taskCore
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed' }
& $taskMono $taskMcs '-target:library' '-langversion:latest' ("-out:" + (Join-Path $taskOut 'Tests.dll')) ("-r:" + (Join-Path $taskOut 'Core.dll')) ("-r:" + (Join-Path $taskOut 'nunit.framework.dll')) $taskTests
if ($LASTEXITCODE -ne 0) { throw 'Pure test compilation failed' }
& $taskMono $taskMcs '-langversion:latest' ("-out:" + (Join-Path $taskOut 'Runner.exe')) ("-r:" + (Join-Path $taskOut 'Core.dll')) ("-r:" + (Join-Path $taskOut 'Tests.dll')) ("-r:" + (Join-Path $taskOut 'nunit.framework.dll')) (Join-Path $PSScriptRoot 'CoreTestRunner.cs')
if ($LASTEXITCODE -ne 0) { throw 'Runner compilation failed' }
$taskRunOutput = & $taskMono (Join-Path $taskOut 'Runner.exe') 2>&1
$taskExit = $LASTEXITCODE
$taskRunOutput | Set-Content -LiteralPath (Join-Path $taskOut 'test-results.log') -Encoding UTF8
$taskRunOutput | Write-Output
$taskEvidence = [ordered]@{
    timestamp = [DateTime]::UtcNow.ToString('o')
    framework = 'Unity-shipped Mono with built-in NUnit assertions, reflection runner'
    unityEditorStarted = $false
    coreSourceSHA256 = (Get-FileHash -LiteralPath $taskCore -Algorithm SHA256).Hash
    testsSourceSHA256 = (Get-FileHash -LiteralPath $taskTests -Algorithm SHA256).Hash
    exitCode = $taskExit
    expectedTests = 47
    evidence = 'test-results.log'
    limitation = 'Offline core unit tests; not Unity Test Runner, scene lifecycle, physics, SDK or Quest validation'
}
$taskEvidence | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOut 'execution.json') -Encoding UTF8
Write-Output "Evidence: $taskOut"
if ($taskExit -ne 0) { throw 'Pure core tests failed or the expected 47 test count changed' }
