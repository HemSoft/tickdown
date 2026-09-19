#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
Import-Module (Join-Path $root 'scripts/CoverageQuality.psm1') -Force
$temp = Join-Path ([IO.Path]::GetTempPath()) "tickdown-coverage-$([Guid]::NewGuid())"
New-Item $temp -ItemType Directory | Out-Null
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

try {
    $coveragePath = Join-Path $temp 'coverage.cobertura.xml'
    @'
<coverage>
  <packages><package name="Synthetic"><classes>
    <class name="TickDown.Core.Models.CountdownTimer" filename="D:/repo/src/TickDown.Core/Models/CountdownTimer.cs"><methods>
      <method name="Stop" signature="()" complexity="6"><lines>
        <line number="1" hits="0" branch="True" condition-coverage="0% (0/2)" />
      </lines></method>
    </methods></class>
    <class name="TickDown.ViewModels.TimerViewModel" filename="D:/repo/src/ViewModels/TimerViewModel.cs"><methods>
      <method name="Tick" signature="()" complexity="6"><lines>
        <line number="2" hits="0" branch="True" condition-coverage="0% (0/2)" />
      </lines></method>
    </methods></class>
    <class name="TickDown.Services.SettingsService" filename="D:/repo/src/Services/SettingsService.cs"><methods>
      <method name="Load" signature="()" complexity="2"><lines>
        <line number="3" hits="1" branch="False" />
        <line number="4" hits="0" branch="False" />
      </lines></method>
      <method name="Generic" signature="(T)" complexity="1"><lines>
        <line number="4" hits="1" branch="False" />
      </lines></method>
      <method name="Mixed" signature="(System.Int32)" complexity="1"><lines>
        <line number="4" hits="1" branch="False" />
      </lines></method>
      <method name="Mixed" signature="(System.Int32)" complexity="1"><lines>
        <line number="4" hits="1" branch="False" />
      </lines></method>
      <method name="op_Implicit" signature="(System.String)" complexity="1"><lines>
        <line number="4" hits="1" branch="False" />
      </lines></method>
      <method name="op_CheckedExplicit" signature="(System.String)" complexity="1"><lines>
        <line number="4" hits="1" branch="False" />
      </lines></method>
    </methods></class>
    <class name="TickDown.Services.SettingsService/&lt;FlushAsync&gt;d__5" filename="D:/repo/src/Services/SettingsService.cs"><methods>
      <method name="MoveNext" signature="()" complexity="3"><lines>
        <line number="5" hits="1" branch="True" condition-coverage="100% (2/2)" />
      </lines></method>
    </methods></class>
    <class name="TickDown.Services.SettingsService/&lt;ReadAsync&gt;d__6`1" filename="D:/repo/src/Services/SettingsService.cs"><methods>
      <method name="MoveNext" signature="()" complexity="2"><lines>
        <line number="6" hits="1" branch="False" />
      </lines></method>
    </methods></class>
    <class name="TickDown.ViewModels.TimerViewModel/&lt;&gt;c" filename="D:/repo/src/ViewModels/TimerViewModel.cs"><methods>
      <method name="&lt;Save&gt;b__1_0" signature="(TickDown.ViewModels.TimerViewModel)" complexity="1"><lines>
        <line number="7" hits="1" branch="False" />
      </lines></method>
    </methods></class>
    <class name="TickDown.Core.Models.CountdownTimer/&lt;History&gt;d__9" filename="D:/repo/src/TickDown.Core/Models/CountdownTimer.cs"><methods>
      <method name="MoveNext" signature="()" complexity="2"><lines>
        <line number="8" hits="1" branch="False" />
      </lines></method>
      <method name="&lt;&gt;m__Finally1" signature="()" complexity="2"><lines>
        <line number="9" hits="0" branch="True" condition-coverage="0% (0/2)" />
      </lines></method>
    </methods></class>
  </classes></package></packages>
</coverage>
'@ | Set-Content $coveragePath -Encoding utf8

    $asyncMap = @{
        'TickDown.Services.SettingsService/<FlushAsync>d__5' = [pscustomobject]@{
            Class = 'TickDown.Services.SettingsService'; Method = 'FlushAsync'; Signature = '(System.Threading.CancellationToken)'
        }
        'TickDown.Services.SettingsService/<ReadAsync>d__6`1' = [pscustomobject]@{
            Class = 'TickDown.Services.SettingsService'; Method = 'ReadAsync`1'; Signature = '(System.String)'
        }
        'TickDown.Core.Models.CountdownTimer/<History>d__9' = [pscustomobject]@{
            Class = 'TickDown.Core.Models.CountdownTimer'; Method = 'History'; Signature = '()'
        }
    }
    $genericArities = @{
        'TickDown.Services.SettingsService::Generic(T)' = @(1)
        'TickDown.Services.SettingsService::Mixed(System.Int32)' = @(0, 1)
    }
    $conversionReturns = @{
        'TickDown.Services.SettingsService::op_Implicit(System.String)' = @('System.Int32')
        'TickDown.Services.SettingsService::op_CheckedExplicit(System.String)' = @('System.Int64')
    }
    $functions = @(Get-CoverageFunctions $coveragePath $asyncMap $genericArities $conversionReturns)
    Assert-Equal 12 $functions.Count 'Function count'
    $stop = $functions | Where-Object Method -eq 'Stop'
    $tick = $functions | Where-Object Method -eq 'Tick'
    $load = $functions | Where-Object Method -eq 'Load'
    Assert-Equal 42 $stop.Crap 'Stop uncovered CRAP'
    Assert-Equal 42 $tick.Crap 'Tick uncovered CRAP'
    Assert-Equal 'branch+line' $tick.CoverageBasis 'Tick coverage basis'
    Assert-Equal 2.5 $load.Crap 'Line fallback CRAP'
    Assert-Equal 'line' $load.CoverageBasis 'Load coverage basis'
    $flush = $functions | Where-Object Method -eq 'FlushAsync'
    Assert-Equal 'TickDown.Services.SettingsService::FlushAsync(System.Threading.CancellationToken)' $flush.Id 'Async state-machine mapping'
    Assert-Equal 3 $flush.Crap 'Async state-machine CRAP'
    $read = $functions | Where-Object Method -eq 'ReadAsync`1'
    Assert-Equal 'TickDown.Services.SettingsService::ReadAsync`1(System.String)' $read.Id 'Generic async state-machine mapping'
    $callback = $functions | Where-Object Method -eq '<Save>b__1_0'
    Assert-Equal 'TickDown.ViewModels.TimerViewModel/<>c::<Save>b__1_0(TickDown.ViewModels.TimerViewModel)' $callback.Id 'Generated callback retention'
    $iterator = $functions | Where-Object Method -eq 'History'
    Assert-Equal 'TickDown.Core.Models.CountdownTimer::History()' $iterator.Id 'Iterator state-machine mapping'
    Assert-Equal 12 $iterator.Crap 'Iterator finally-helper aggregation'
    $generic = $functions | Where-Object Method -eq 'Generic`1'
    Assert-Equal 'TickDown.Services.SettingsService::Generic`1(T)' $generic.Id 'Ordinary generic method arity'
    $mixed = @($functions | Where-Object Method -Like 'Mixed*')
    Assert-Equal 'TickDown.Services.SettingsService::Mixed(System.Int32)' $mixed[0].Id 'Nongeneric shared-signature identity'
    Assert-Equal 'TickDown.Services.SettingsService::Mixed`1(System.Int32)' $mixed[1].Id 'Generic shared-signature identity'
    $module = Get-Module CoverageQuality
    $rankedArrayName = & $module { Format-CoverageTypeName ([int[,]]) }
    Assert-Equal 'System.Int32[,]' $rankedArrayName 'Multidimensional array rank formatting'
    $conversion = $functions | Where-Object Method -eq 'op_Implicit'
    Assert-Equal 'TickDown.Services.SettingsService::op_Implicit(System.String)->System.Int32' $conversion.Id 'Conversion return identity'
    $checkedConversion = $functions | Where-Object Method -eq 'op_CheckedExplicit'
    Assert-Equal 'TickDown.Services.SettingsService::op_CheckedExplicit(System.String)->System.Int64' $checkedConversion.Id 'Checked conversion return identity'

    $healthy = @($functions | ForEach-Object {
        [pscustomobject]@{
            Id = $_.Id; Class = $_.Class; Method = $_.Method; Signature = $_.Signature
            Source = $_.Source; Complexity = $_.Complexity; CoverageBasis = $_.CoverageBasis
            Coverage = if ($_.CoverageBasis -eq 'branch+line') { 1 } else { $_.Coverage }
            Crap = if ($_.CoverageBasis -eq 'branch+line') { $_.Complexity } else { $_.Crap }
        }
    })
    $baselinePath = Join-Path $temp 'baseline.json'
    Write-CoverageBaseline $healthy $baselinePath
    $regressions = @(Test-CoverageBaseline $functions $baselinePath)
    Assert-Equal 3 $regressions.Count 'Uncovered branch regressions'
    $missingFunction = @($healthy | Where-Object Method -ne 'FlushAsync')
    Assert-Equal 1 (@(Test-CoverageBaseline $missingFunction $baselinePath)).Count 'Missing baseline function rejection'

    $newHigh = [pscustomobject]@{
        Id = 'TickDown.Core.Models.NewRisk::Run()'; Class = 'NewRisk'; Method = 'Run'; Signature = '()'
        Source = 'src/TickDown.Core/Models/NewRisk.cs'; Complexity = 6; CoverageBasis = 'branch'; Coverage = 0; Crap = 42
    }
    $newHighFailures = @(Test-CoverageBaseline ($healthy + $newHigh) $baselinePath)
    Assert-Equal 1 $newHighFailures.Count 'New high-risk rejection'
    Assert-Equal 1 (@(Test-CoverageBaseline ($healthy + $newHigh) $baselinePath -AllowNewFunctions)).Count 'High-risk baseline update rejection'

    $newLow = $newHigh.PSObject.Copy()
    $newLow.Id = 'TickDown.Core.Models.NewRisk::Safe()'
    $newLow.Crap = 30
    Assert-Equal 1 (@(Test-CoverageBaseline ($healthy + $newLow) $baselinePath)).Count 'New function inventory requirement'
    Assert-Equal 0 (@(Test-CoverageBaseline ($healthy + $newLow) $baselinePath -AllowNewFunctions)).Count 'Low-risk baseline update acceptance'
    $expandedBaselinePath = Join-Path $temp 'expanded-baseline.json'
    Write-CoverageBaseline ($healthy + $newLow) $expandedBaselinePath
    Assert-Equal 0 (@(Test-CoverageBaseline ($healthy + $newLow) $expandedBaselinePath)).Count 'Reviewed new function acceptance'
    Assert-Equal 2 (@(Test-CoverageBaseline $healthy $expandedBaselinePath)).Count 'Reviewed new source and function removal rejection'

    $reportPath = Join-Path $temp 'reports'
    Write-CoverageReports $functions $reportPath
    Assert-Equal $true (Test-Path (Join-Path $reportPath 'function-risk.json')) 'JSON report'
    Assert-Equal $true (Test-Path (Join-Path $reportPath 'function-risk.md')) 'Markdown report'
    $runsettings = Get-Content (Join-Path $root 'coverage.runsettings') -Raw
    Assert-Equal $false $runsettings.Contains('ExcludeByAttribute') 'No attribute-based coverage escape hatch'
    Assert-Equal $true ($null -ne (Get-Command Get-CoverageExclusionViolations)) 'Compiled exclusion guard exported'
    $sourceRoot = Join-Path $temp 'src'
    New-Item $sourceRoot -ItemType Directory | Out-Null
    '[ExcludeFromCodeCoverage] class Hidden {}' | Set-Content (Join-Path $sourceRoot 'Hidden.cs')
    Assert-Equal 1 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Source exclusion rejection'
    Remove-Item (Join-Path $sourceRoot 'Hidden.cs')
    Assert-Equal 0 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Clean source acceptance'
    $ownedResults = Resolve-CoverageResultsPath $temp 'artifacts/coverage/run'
    Assert-Equal $true $ownedResults.EndsWith('artifacts\coverage\run') 'Owned result path acceptance'
    $dangerousPathRejected = $false
    try { $null = Resolve-CoverageResultsPath $temp 'artifacts' } catch { $dangerousPathRejected = $true }
    Assert-Equal $true $dangerousPathRejected 'Shared result path rejection'
    $currentOutput = Join-Path $temp 'bin/current'
    $staleOutput = Join-Path $temp 'bin/stale'
    New-Item $currentOutput, $staleOutput -ItemType Directory | Out-Null
    New-Item (Join-Path $currentOutput 'TickDown.Tests.dll'), (Join-Path $currentOutput 'TickDown.Core.dll'), (Join-Path $staleOutput 'TickDown.Core.dll') -ItemType File | Out-Null
    $resolvedAssemblies = @(Resolve-CoveredAssemblyPaths @('TickDown.Tests', 'TickDown.Core') (Join-Path $currentOutput 'TickDown.Tests.dll'))
    Assert-Equal 2 $resolvedAssemblies.Count 'Current output assembly count'
    Assert-Equal (Join-Path $currentOutput 'TickDown.Core.dll') $resolvedAssemblies[1] 'Stale assembly ignored'
    "Passed $passed coverage-quality assertions."
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}
