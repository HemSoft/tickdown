#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
Import-Module (Join-Path $root 'scripts/DesktopQualification.psm1') -Force
$temp = Join-Path ([IO.Path]::GetTempPath()) "tickdown-desktop-$([Guid]::NewGuid())"
New-Item $temp -ItemType Directory | Out-Null
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

try {
    Assert-Equal 30 (Get-QualificationPercentile @(10, 20, 30, 40) 0.75) 'Nearest-rank percentile'
    Assert-Equal 0 (Get-QualificationPercentile @() 0.95) 'Empty percentile'
    $owned = Resolve-DesktopQualificationOutputPath $temp 'artifacts/desktop-qualification/run'
    Assert-Equal $true $owned.EndsWith('artifacts\desktop-qualification\run') 'Owned output acceptance'
    $sharedRejected = $false
    try { $null = Resolve-DesktopQualificationOutputPath $temp 'artifacts' } catch { $sharedRejected = $true }
    Assert-Equal $true $sharedRejected 'Shared output rejection'
    $traversalRejected = $false
    try { $null = Resolve-DesktopQualificationOutputPath $temp '../outside' } catch { $traversalRejected = $true }
    Assert-Equal $true $traversalRejected 'Traversal rejection'

    $runtimeZero = [pscustomobject]@{
        TickLatencyP50Milliseconds = 0; TickLatencyP95Milliseconds = 0; TickLatencyP99Milliseconds = 0
        TickLatencyMaximumMilliseconds = 0; DisplayedTicks = 0; PendingUiCallbacks = 0
        MaximumPendingUiCallbacks = 0; TimerSubscriptions = 0; ActiveAlarmRepeatTimers = 0; ActiveMediaPlayers = 0
    }
    $before = [pscustomobject]@{
        TotalAvailableMemoryBytes = 17179869184; ManagedHeapBytes = 10000000; PrivateMemoryBytes = 100000000
        WorkingSetBytes = 80000000; HandleCount = 100; ThreadCount = 20; TimerCount = 0; Runtime = $runtimeZero
    }
    $after = [pscustomobject]@{
        TotalAvailableMemoryBytes = 17179869184; ManagedHeapBytes = 11000000; PrivateMemoryBytes = 104000000
        WorkingSetBytes = 83000000; HandleCount = 102; ThreadCount = 21; TimerCount = 0; Runtime = $runtimeZero
    }
    $loadRuntime = [pscustomobject]@{
        TickLatencyP50Milliseconds = 2; TickLatencyP95Milliseconds = 5; TickLatencyP99Milliseconds = 8
        TickLatencyMaximumMilliseconds = 10; DisplayedTicks = 110; PendingUiCallbacks = 0
        MaximumPendingUiCallbacks = 2; TimerSubscriptions = 4; ActiveAlarmRepeatTimers = 0; ActiveMediaPlayers = 0
    }
    $load = [pscustomobject]@{ Runtime = $loadRuntime }
    $policy = (Get-Content (Join-Path $root 'scripts/desktop-qualification-policy.json') -Raw | ConvertFrom-Json).budgets
    $evaluation = Get-QualificationEvaluation $before $after $load @(4, 8, 12) $policy 4 3
    Assert-Equal $true $evaluation.Passed 'Healthy qualification acceptance'
    Assert-Equal 12 $evaluation.Metrics.inputLatencyP95Milliseconds 'Input p95 calculation'
    Assert-Equal 84 $evaluation.Budgets.minimumDisplayedTicks 'Throughput budget'
    Assert-Equal 1000000 $evaluation.Metrics.managedHeapGrowthBytes 'Managed heap delta'
    Assert-Equal 2 $evaluation.Metrics.handleGrowth 'Handle delta'

    $badAfter = $after.PSObject.Copy()
    $badAfter.Runtime = [pscustomobject]@{
        PendingUiCallbacks = 1; TimerSubscriptions = 1; ActiveAlarmRepeatTimers = 1; ActiveMediaPlayers = 1
    }
    $badAfter.TimerCount = 1
    $badAfter.ManagedHeapBytes = 100000000
    $bad = Get-QualificationEvaluation $before $badAfter $load @(400) $policy 4 3
    Assert-Equal $false $bad.Passed 'Regression rejection'
    Assert-Equal $true ($bad.Failures.Count -ge 6) 'Distinct resource and latency failures'

    $scriptText = Get-Content (Join-Path $root 'scripts/desktop-qualification.ps1') -Raw
    Assert-Equal $true $scriptText.StartsWith('#Requires -Version 7.4') 'Start-Process environment version requirement'
    Assert-Equal $false $scriptText.Contains('RedirectStandardError = $true') 'Nonblocking ffmpeg diagnostics'
    Assert-Equal $true ($scriptText.IndexOf('if (!$Recorder.HasExited)', [StringComparison]::Ordinal) -lt $scriptText.IndexOf("StandardInput.WriteLine('q')", [StringComparison]::Ordinal)) 'Exited recorder guard'
    Assert-Equal $true ($scriptText -match 'finally\s*\{\s*if \(\$null -ne \$app') 'Recorder-independent app cleanup'
    Assert-Equal $true $scriptText.Contains('TICKDOWN_SETTINGS_DIRECTORY') 'Isolated settings launch'
    Assert-Equal $true $scriptText.Contains('TICKDOWN_QUALIFICATION_CANDIDATE') 'Candidate-bound launch'
    Assert-Equal $true $scriptText.Contains('$inputLatencies.Clear()') 'Measured input-latency boundary'
    Assert-Equal $true $scriptText.Contains('AlarmReplayRequests') 'Observed alarm replay'
    Assert-Equal $true $scriptText.Contains("AutomationId -ne 'RemoveTimerButton'") 'Expected keyboard tab target'
    Assert-Equal 3 ([regex]::Matches($scriptText, 'Assert-NamedActionableControls \$window').Count) 'Dynamic accessible-name states'
    Assert-Equal $true $scriptText.Contains("AppliedTheme -ne 'Dark'") 'Applied theme verification'
    Assert-Equal 2 ([regex]::Matches($scriptText, 'git -C \$root rev-parse HEAD').Count) 'Candidate revision revalidation'
    Assert-Equal 2 ([regex]::Matches($scriptText, 'git -C \$root status --porcelain').Count) 'Working-tree revalidation'
    Assert-Equal $true $scriptText.Contains('SetWindowPos') 'Deterministic display placement'
    Assert-Equal $true $scriptText.Contains('SystemInformation]::HighContrast') 'High-contrast state capture'
    Assert-Equal $true $scriptText.Contains('interaction.mp4') 'Bounded screen recording'
    Assert-Equal $true ((Get-Content (Join-Path $root 'src/Views/MainPage.xaml') -Raw).Contains('x:Key="HighContrast"')) 'High-contrast resource dictionary'
    $policyDocument = Get-Content (Join-Path $root 'scripts/desktop-qualification-policy.json') -Raw | ConvertFrom-Json
    Assert-Equal $true ($policyDocument.full.measuredCycles -gt $policyDocument.fast.measuredCycles) 'Full duration tier'
    Assert-Equal 8 $policyDocument.full.loadTimerCount 'Documented full timer count'
    Assert-Equal 4 $policyDocument.fast.loadTimerCount 'Documented fast timer count'
    $missingContrast = Get-QualificationEvaluation $before $after $load @(4, 8, 12) $policy 4 3 -HighContrastResource $false
    Assert-Equal $false $missingContrast.Passed 'Missing high-contrast resource rejection'
    Assert-Equal $true ($missingContrast.Failures -contains 'The required high-contrast resource dictionary is missing.') 'High-contrast failure detail'

    $reportResult = [pscustomobject]@{
        Candidate = 'abc'; Mode = 'Fast'; RunLabel = 'fixture'; ElapsedSeconds = 1
        Machine = [pscustomobject]@{ ProcessorCount = 4; TotalAvailableMemoryBytes = 100 }
        Evaluation = $evaluation
    }
    $reportPath = Join-Path $temp 'report'
    Write-DesktopQualificationReport $reportResult $reportPath
    Assert-Equal $true (Test-Path (Join-Path $reportPath 'qualification-result.json')) 'JSON report'
    $reportMarkdown = Get-Content (Join-Path $reportPath 'qualification-result.md') -Raw
    Assert-Equal $true $reportMarkdown.Contains('| minimumDisplayedTicks | 110 | 84 |') 'Throughput report value'
    Assert-Equal $true ((Get-Content (Join-Path $root 'src/Diagnostics/QualificationSnapshotWriter.cs') -Raw).Contains('GC.GetTotalMemory(forceFullCollection: false)')) 'Current managed-memory measurement'
    Assert-Equal $true ((Get-Content (Join-Path $root 'src/Services/AudioService.cs') -Raw).Contains('lock (this.playbackSync)')) 'Serialized playback ownership'
    "Passed $passed desktop-qualification assertions."
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
