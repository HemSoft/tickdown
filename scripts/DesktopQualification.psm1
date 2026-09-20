Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-DesktopQualificationOutputPath([string]$RepoRoot, [string]$OutputDirectory) {
    $ownedRoot = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts/desktop-qualification'))
    $candidate = [IO.Path]::GetFullPath((Join-Path $RepoRoot $OutputDirectory))
    $ownedPrefix = $ownedRoot.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($candidate -ne $ownedRoot -and !$candidate.StartsWith($ownedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Desktop qualification output must stay within $ownedRoot; received $candidate."
    }
    return $candidate
}

function Get-QualificationPercentile([double[]]$Values, [double]$Percentile) {
    if ($Values.Count -eq 0) { return 0.0 }
    if ($Percentile -lt 0 -or $Percentile -gt 1) { throw "Percentile must be between zero and one: $Percentile" }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Ceiling($Percentile * $sorted.Count) - 1
    return [double]$sorted[[Math]::Clamp($index, 0, $sorted.Count - 1)]
}

function Get-QualificationEvaluation(
    [object]$ResourceBefore,
    [object]$ResourceAfter,
    [object]$LoadSnapshot,
    [double[]]$InputLatenciesMilliseconds,
    [object]$Policy,
    [int]$LoadTimerCount,
    [double]$LoadSeconds
) {
    $memoryScale = [Math]::Max(1.0, [double]$ResourceAfter.TotalAvailableMemoryBytes / [double]$Policy.machineReferenceBytes)
    $budgets = [ordered]@{
        tickLatencyP95Milliseconds = [double]$Policy.tickLatencyP95Milliseconds
        tickLatencyP99Milliseconds = [double]$Policy.tickLatencyP99Milliseconds
        inputLatencyP95Milliseconds = [double]$Policy.inputLatencyP95Milliseconds
        minimumDisplayedTicks = [Math]::Floor($LoadTimerCount * 10 * $LoadSeconds * [double]$Policy.minimumThroughputRatio)
        managedHeapGrowthBytes = [Math]::Round([double]$Policy.managedHeapGrowthBytes * [Math]::Sqrt($memoryScale))
        privateMemoryGrowthBytes = [Math]::Round([double]$Policy.privateMemoryGrowthBytes * [Math]::Sqrt($memoryScale))
        workingSetGrowthBytes = [Math]::Round([double]$Policy.workingSetGrowthBytes * [Math]::Sqrt($memoryScale))
        handleGrowth = [int]$Policy.handleGrowth
        threadGrowth = [int]$Policy.threadGrowth
        maximumPendingUiCallbacks = [Math]::Max(1, [Math]::Ceiling($LoadTimerCount * [double]$Policy.maximumPendingCallbacksPerTimer))
    }
    $metrics = [ordered]@{
        inputLatencyP50Milliseconds = Get-QualificationPercentile $InputLatenciesMilliseconds 0.50
        inputLatencyP95Milliseconds = Get-QualificationPercentile $InputLatenciesMilliseconds 0.95
        inputLatencyMaximumMilliseconds = if ($InputLatenciesMilliseconds.Count -eq 0) { 0.0 } else { [double]($InputLatenciesMilliseconds | Measure-Object -Maximum).Maximum }
        tickLatencyP50Milliseconds = [double]$LoadSnapshot.Runtime.TickLatencyP50Milliseconds
        tickLatencyP95Milliseconds = [double]$LoadSnapshot.Runtime.TickLatencyP95Milliseconds
        tickLatencyP99Milliseconds = [double]$LoadSnapshot.Runtime.TickLatencyP99Milliseconds
        tickLatencyMaximumMilliseconds = [double]$LoadSnapshot.Runtime.TickLatencyMaximumMilliseconds
        displayedTicks = [long]$LoadSnapshot.Runtime.DisplayedTicks
        displayedTicksPerSecond = [Math]::Round([double]$LoadSnapshot.Runtime.DisplayedTicks / $LoadSeconds, 3)
        managedHeapGrowthBytes = [long]$ResourceAfter.ManagedHeapBytes - [long]$ResourceBefore.ManagedHeapBytes
        privateMemoryGrowthBytes = [long]$ResourceAfter.PrivateMemoryBytes - [long]$ResourceBefore.PrivateMemoryBytes
        workingSetGrowthBytes = [long]$ResourceAfter.WorkingSetBytes - [long]$ResourceBefore.WorkingSetBytes
        handleGrowth = [int]$ResourceAfter.HandleCount - [int]$ResourceBefore.HandleCount
        threadGrowth = [int]$ResourceAfter.ThreadCount - [int]$ResourceBefore.ThreadCount
        maximumPendingUiCallbacks = [int]$LoadSnapshot.Runtime.MaximumPendingUiCallbacks
        finalPendingUiCallbacks = [int]$ResourceAfter.Runtime.PendingUiCallbacks
        finalTimerSubscriptions = [int]$ResourceAfter.Runtime.TimerSubscriptions
        finalActiveAlarmRepeatTimers = [int]$ResourceAfter.Runtime.ActiveAlarmRepeatTimers
        finalActiveMediaPlayers = [int]$ResourceAfter.Runtime.ActiveMediaPlayers
        finalTimerCount = [int]$ResourceAfter.TimerCount
    }
    $failures = [Collections.Generic.List[string]]::new()
    if ($metrics.tickLatencyP95Milliseconds -gt $budgets.tickLatencyP95Milliseconds) { $failures.Add("Tick p95 $($metrics.tickLatencyP95Milliseconds) ms exceeds $($budgets.tickLatencyP95Milliseconds) ms.") }
    if ($metrics.tickLatencyP99Milliseconds -gt $budgets.tickLatencyP99Milliseconds) { $failures.Add("Tick p99 $($metrics.tickLatencyP99Milliseconds) ms exceeds $($budgets.tickLatencyP99Milliseconds) ms.") }
    if ($metrics.inputLatencyP95Milliseconds -gt $budgets.inputLatencyP95Milliseconds) { $failures.Add("Input p95 $($metrics.inputLatencyP95Milliseconds) ms exceeds $($budgets.inputLatencyP95Milliseconds) ms.") }
    if ($metrics.displayedTicks -lt $budgets.minimumDisplayedTicks) { $failures.Add("Displayed ticks $($metrics.displayedTicks) are below $($budgets.minimumDisplayedTicks).") }
    foreach ($metricName in 'managedHeapGrowthBytes', 'privateMemoryGrowthBytes', 'workingSetGrowthBytes', 'handleGrowth', 'threadGrowth', 'maximumPendingUiCallbacks') {
        if ($metrics[$metricName] -gt $budgets[$metricName]) { $failures.Add("$metricName $($metrics[$metricName]) exceeds $($budgets[$metricName]).") }
    }
    foreach ($counter in 'finalPendingUiCallbacks', 'finalTimerSubscriptions', 'finalActiveAlarmRepeatTimers', 'finalActiveMediaPlayers', 'finalTimerCount') {
        if ($metrics[$counter] -ne 0) { $failures.Add("$counter must settle to zero; observed $($metrics[$counter]).") }
    }
    [pscustomobject]@{ Passed = $failures.Count -eq 0; Failures = $failures.ToArray(); Metrics = $metrics; Budgets = $budgets }
}

function Write-DesktopQualificationReport([object]$Result, [string]$OutputDirectory) {
    New-Item $OutputDirectory -ItemType Directory -Force | Out-Null
    $Result | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $OutputDirectory 'qualification-result.json') -Encoding utf8
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('# Desktop qualification result')
    $lines.Add('')
    $lines.Add("- Candidate: ``$($Result.Candidate)``")
    $lines.Add("- Mode: $($Result.Mode)")
    $lines.Add("- Run: $($Result.RunLabel)")
    $lines.Add("- Result: $(if ($Result.Evaluation.Passed) { 'PASS' } else { 'FAIL' })")
    $lines.Add("- Elapsed: $($Result.ElapsedSeconds) seconds")
    $lines.Add("- Machine: $($Result.Machine.ProcessorCount) logical processors, $($Result.Machine.TotalAvailableMemoryBytes) runtime-available bytes")
    $lines.Add('')
    $lines.Add('## Measurements')
    $lines.Add('')
    $lines.Add('| Metric | Actual | Budget |')
    $lines.Add('|---|---:|---:|')
    foreach ($name in $Result.Evaluation.Budgets.Keys) {
        $actual = if ($Result.Evaluation.Metrics.Contains($name)) { $Result.Evaluation.Metrics[$name] } else { '-' }
        $lines.Add("| $name | $actual | $($Result.Evaluation.Budgets[$name]) |")
    }
    $lines.Add('')
    $lines.Add('## Failures')
    $lines.Add('')
    if ($Result.Evaluation.Failures.Count -eq 0) { $lines.Add('None.') }
    else { foreach ($failure in $Result.Evaluation.Failures) { $lines.Add("- $failure") } }
    $lines | Set-Content (Join-Path $OutputDirectory 'qualification-result.md') -Encoding utf8
}

Export-ModuleMember -Function Resolve-DesktopQualificationOutputPath, Get-QualificationPercentile, Get-QualificationEvaluation, Write-DesktopQualificationReport
