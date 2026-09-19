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
    <class name="TickDown.Services.SettingsService/&lt;&lt;Outer&gt;g__Local|0_0&gt;d" filename="D:/repo/src/Services/SettingsService.cs"><methods>
      <method name="MoveNext" signature="()" complexity="2"><lines>
        <line number="6" hits="1" branch="False" />
      </lines></method>
      <method name="&lt;&gt;m__Finally1" signature="()" complexity="2"><lines>
        <line number="7" hits="0" branch="True" condition-coverage="0% (0/2)" />
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
        '[Synthetic]TickDown.Services.SettingsService/<FlushAsync>d__5' = [pscustomobject]@{
            Class = 'TickDown.Services.SettingsService'; Method = 'FlushAsync'; Signature = '(System.Threading.CancellationToken)'
        }
        '[Synthetic]TickDown.Services.SettingsService/<ReadAsync>d__6`1' = [pscustomobject]@{
            Class = 'TickDown.Services.SettingsService'; Method = 'ReadAsync`1'; Signature = '(System.String)'
        }
        '[Synthetic]TickDown.Services.SettingsService/<<Outer>g__Local|0_0>d' = [pscustomobject]@{
            Class = 'TickDown.Services.SettingsService'; Method = '<Outer>g__Local|0_0'; Signature = '()'
        }
        '[Synthetic]TickDown.Core.Models.CountdownTimer/<History>d__9' = [pscustomobject]@{
            Class = 'TickDown.Core.Models.CountdownTimer'; Method = 'History'; Signature = '()'
        }
    }
    $genericArities = @{
        '[Synthetic]TickDown.Services.SettingsService::Generic(T)' = @(1)
        '[Synthetic]TickDown.Services.SettingsService::Mixed(System.Int32)' = @(0, 1)
    }
    $conversionReturns = @{
        '[Synthetic]TickDown.Services.SettingsService::op_Implicit(System.String)' = @('System.Int32', 'System.Double')
        '[Synthetic]TickDown.Services.SettingsService::op_CheckedExplicit(System.String)' = @('System.Int64')
    }
    $functions = @(Get-CoverageFunctions $coveragePath $asyncMap $genericArities $conversionReturns)
    Assert-Equal 14 $functions.Count 'Function count'
    $stop = $functions | Where-Object Method -eq 'Stop'
    $tick = $functions | Where-Object Method -eq 'Tick'
    $load = $functions | Where-Object Method -eq 'Load'
    Assert-Equal 42 $stop.Crap 'Stop uncovered CRAP'
    Assert-Equal 42 $tick.Crap 'Tick uncovered CRAP'
    Assert-Equal 'branch+line' $tick.CoverageBasis 'Tick coverage basis'
    Assert-Equal 2.5 $load.Crap 'Line fallback CRAP'
    Assert-Equal 'line' $load.CoverageBasis 'Load coverage basis'
    $flush = $functions | Where-Object Method -eq 'FlushAsync'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::FlushAsync(System.Threading.CancellationToken)' $flush.Id 'Async state-machine mapping'
    Assert-Equal 3 $flush.Crap 'Async state-machine CRAP'
    $read = $functions | Where-Object Method -eq 'ReadAsync`1'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::ReadAsync`1(System.String)' $read.Id 'Generic async state-machine mapping'
    $localStateMachine = $functions | Where-Object Method -eq '<Outer>g__Local|0_0'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::<Outer>g__Local|0_0()' $localStateMachine.Id 'Local-function state-machine mapping'
    Assert-Equal 12 $localStateMachine.Crap 'Local-function state-machine aggregation'
    $callback = $functions | Where-Object Method -eq '<Save>b__1_0'
    Assert-Equal '[Synthetic]TickDown.ViewModels.TimerViewModel/<>c::<Save>b__1_0(TickDown.ViewModels.TimerViewModel)' $callback.Id 'Generated callback retention'
    $iterator = $functions | Where-Object Method -eq 'History'
    Assert-Equal '[Synthetic]TickDown.Core.Models.CountdownTimer::History()' $iterator.Id 'Iterator state-machine mapping'
    Assert-Equal 12 $iterator.Crap 'Iterator finally-helper aggregation'
    $generic = $functions | Where-Object Method -eq 'Generic`1'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::Generic`1(T)' $generic.Id 'Ordinary generic method arity'
    $mixed = @($functions | Where-Object Method -Like 'Mixed*')
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::Mixed(System.Int32)' $mixed[0].Id 'Nongeneric shared-signature identity'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::Mixed`1(System.Int32)' $mixed[1].Id 'Generic shared-signature identity'
    $module = Get-Module CoverageQuality
    $rankedArrayName = & $module { Format-CoverageTypeName ([int[,]]) }
    Assert-Equal 'System.Int32[,]' $rankedArrayName 'Multidimensional array rank formatting'
    $conversions = @($functions | Where-Object Method -eq 'op_Implicit')
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::op_Implicit(System.String)->System.Int32' $conversions[0].Id 'First conversion return identity'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::op_Implicit(System.String)->System.Double' $conversions[1].Id 'Second conversion return identity'
    $checkedConversion = $functions | Where-Object Method -eq 'op_CheckedExplicit'
    Assert-Equal '[Synthetic]TickDown.Services.SettingsService::op_CheckedExplicit(System.String)->System.Int64' $checkedConversion.Id 'Checked conversion return identity'
    Assert-Equal 'Synthetic' $stop.Assembly 'Function assembly identity'

    $multiAssemblyPath = Join-Path $temp 'multi-assembly.xml'
    @'
<coverage><packages>
  <package name="Alpha"><classes><class name="Shared.Type" filename="D:/repo/shared/Alpha.cs"><methods>
    <method name="Run" signature="()" complexity="1"><lines><line number="1" hits="1" branch="False" /></lines></method>
  </methods></class></classes></package>
  <package name="Beta"><classes><class name="Shared.Type" filename="D:/repo/src/Beta.cs"><methods>
    <method name="Run" signature="()" complexity="1"><lines><line number="1" hits="1" branch="False" /></lines></method>
  </methods></class></classes></package>
</packages></coverage>
'@ | Set-Content $multiAssemblyPath -Encoding utf8
    $multiAssemblyFunctions = @(Get-CoverageFunctions $multiAssemblyPath @{} @{} @{} 'D:/repo')
    Assert-Equal 2 $multiAssemblyFunctions.Count 'Cross-assembly identity count'
    Assert-Equal 'shared/Alpha.cs' $multiAssemblyFunctions[0].Source 'Repository-relative source outside src'
    Assert-Equal '[Alpha]Shared.Type::Run()' $multiAssemblyFunctions[0].Id 'First assembly-qualified identity'
    Assert-Equal '[Beta]Shared.Type::Run()' $multiAssemblyFunctions[1].Id 'Second assembly-qualified identity'

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
    Assert-Equal 3 ((Get-Content $baselinePath -Raw | ConvertFrom-Json).version) 'Assembly-qualified baseline version'
    $regressions = @(Test-CoverageBaseline $functions $baselinePath)
    Assert-Equal 4 $regressions.Count 'Uncovered branch regressions'
    $missingFunction = @($healthy | Where-Object Method -ne 'FlushAsync')
    Assert-Equal 1 (@(Test-CoverageBaseline $missingFunction $baselinePath)).Count 'Missing baseline function rejection'

    $newHigh = [pscustomobject]@{
        Id = '[Synthetic]TickDown.Core.Models.NewRisk::Run()'; Assembly = 'Synthetic'; Class = 'NewRisk'; Method = 'Run'; Signature = '()'
        Source = 'src/TickDown.Core/Models/NewRisk.cs'; Complexity = 6; CoverageBasis = 'branch'; Coverage = 0; Crap = 42
    }
    $newHighFailures = @(Test-CoverageBaseline ($healthy + $newHigh) $baselinePath)
    Assert-Equal 1 $newHighFailures.Count 'New high-risk rejection'
    Assert-Equal 1 (@(Test-CoverageBaseline ($healthy + $newHigh) $baselinePath -AllowNewFunctions)).Count 'High-risk baseline update rejection'

    $newLow = $newHigh.PSObject.Copy()
    $newLow.Id = '[Synthetic]TickDown.Core.Models.NewRisk::Safe()'
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
    [xml]$runsettingsXml = $runsettings
    $excludeFilter = [string]$runsettingsXml.RunSettings.DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude
    Assert-Equal $false $runsettings.Contains('ExcludeByAttribute') 'No attribute-based coverage escape hatch'
    Assert-Equal $false $excludeFilter.Contains('[TickDown]TickDown.ViewModels.*') 'No namespace-wide ViewModel exclusion'
    Assert-Equal $false $excludeFilter.Contains('MainViewModel*') 'MainViewModel exclusion anchored'
    Assert-Equal $false $excludeFilter.Contains('TimerViewModel*') 'TimerViewModel exclusion anchored'
    Assert-Equal $false $excludeFilter.Contains('SettingsService*') 'SettingsService exclusion anchored'
    Assert-Equal $false $excludeFilter.Contains('[TickDown]Microsoft.*') 'No namespace-wide Microsoft exclusion'
    Assert-Equal $false $runsettings.Contains('[TickDown.Tests]TickDown.ViewModels.*') 'No namespace-wide test ViewModel inclusion'
    Assert-Equal $true $runsettings.Contains('[TickDown.Tests]TickDown.ViewModels.MainViewModel*') 'MainViewModel collector prefix'
    Assert-Equal $true $runsettings.Contains('[TickDown.Tests]TickDown.Services.SettingsService*') 'SettingsService collector prefix'
    Assert-Equal $true ($null -ne (Get-Command Get-CoverageExclusionViolations)) 'Compiled exclusion guard exported'
    Assert-Equal $true ($null -ne (Get-Command Get-UnexpectedSourceLinkedTypes)) 'Collector boundary guard exported'
    $coverageRunner = Get-Content (Join-Path $root 'scripts/check-coverage.ps1') -Raw
    Assert-Equal $true $coverageRunner.Contains('-getItem:ProjectReference') 'MSBuild project-reference inventory query'
    Assert-Equal $true $coverageRunner.Contains('$productionAssemblyNames') 'Dynamic production assembly filter input'
    Assert-Equal $false $coverageRunner.Contains('$expectedAssemblyNames = @([regex]::Matches') 'Expected assemblies not derived from static filters'
    $sourceRoot = Join-Path $temp 'src'
    New-Item $sourceRoot -ItemType Directory | Out-Null
    '[ExcludeFromCodeCoverage] class Hidden {}' | Set-Content (Join-Path $sourceRoot 'Hidden.cs')
    Assert-Equal 1 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Source exclusion rejection'
    'using Hidden = System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute; [Hidden] class Aliased {}' | Set-Content (Join-Path $sourceRoot 'Aliased.cs')
    Assert-Equal 2 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Direct and aliased source exclusion rejection'
    'global using GlobalHidden = System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute;' | Set-Content (Join-Path $sourceRoot 'GlobalAlias.cs')
    'class GloballyAliased { [GlobalHidden] void Hidden() {} }' | Set-Content (Join-Path $sourceRoot 'GlobalAliasUse.cs')
    'class Verbatim { [@ExcludeFromCodeCoverageAttribute] void Hidden() {} }' | Set-Content (Join-Path $sourceRoot 'Verbatim.cs')
    Assert-Equal 4 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Global alias and verbatim attribute rejection'
    Remove-Item (Join-Path $sourceRoot '*.cs')
    '// ExcludeFromCodeCoverage is forbidden.' | Set-Content (Join-Path $sourceRoot 'Comment.cs')
    'class Documented { const string Policy = "ExcludeFromCoverage"; }' | Set-Content (Join-Path $sourceRoot 'String.cs')
    Assert-Equal 0 @(Get-CoverageSourceExclusionViolations $sourceRoot).Count 'Comments and strings are not attributes'
    $partialSourceRoot = Join-Path $temp 'partial-src'
    $viewModelSources = Join-Path $partialSourceRoot 'ViewModels'
    $nestedSources = Join-Path $partialSourceRoot 'Other'
    New-Item $viewModelSources, $nestedSources -ItemType Directory | Out-Null
    'namespace TickDown.ViewModels; public partial class MainViewModel {}' | Set-Content (Join-Path $viewModelSources 'MainViewModel.cs')
    'namespace TickDown.ViewModels; public unsafe partial class TimerViewModel {}' | Set-Content (Join-Path $nestedSources 'TimerViewModel.Extra.cs')
    'namespace TickDown.Services; public partial class SettingsService {}' | Set-Content (Join-Path $nestedSources 'SettingsService.Extra.cs')
    ('namespace TickDown.ViewModels;' + "`n" + '[Description("//")] public partial' + "`n" + 'class MainViewModel {}') | Set-Content (Join-Path $nestedSources 'MainViewModel.Multiline.cs')
    'namespace TickDown.ViewModels; public partial /* split declaration */ class TimerViewModel {}' | Set-Content (Join-Path $nestedSources 'TimerViewModel.Commented.cs')
    ("#if NET10_0_WINDOWS`n" + 'namespace TickDown.ViewModels; public partial class MainViewModel {}' + "`n#endif") | Set-Content (Join-Path $nestedSources 'MainViewModel.Conditional.cs')
    'namespace Other; public partial class SettingsService {}' | Set-Content (Join-Path $nestedSources 'UnrelatedSettingsService.cs')
    'namespace TickDown { namespace ViewModels { public partial class MainViewModel {} } }' | Set-Content (Join-Path $nestedSources 'MainViewModel.NestedNamespace.cs')
    'namespace TickDown.@ViewModels; public partial class MainViewModel {}' | Set-Content (Join-Path $nestedSources 'MainViewModel.VerbatimNamespace.cs')
    'namespace TickDown.ViewModels; public partial class MainViewModel<T> {}' | Set-Content (Join-Path $nestedSources 'UnrelatedGenericMainViewModel.cs')
    $linkedPatterns = @{
        'TickDown.ViewModels.MainViewModel' = 'ViewModels/MainViewModel.cs'
        'TickDown.ViewModels.TimerViewModel' = 'ViewModels/TimerViewModel.cs'
        'TickDown.Services.SettingsService' = 'Services/SettingsService.cs'
    }
    Assert-Equal 7 @(Get-UnlinkedPartialTypeViolations $partialSourceRoot $linkedPatterns @('NET10_0_WINDOWS')).Count 'Qualified, arity-aware, conditional, and trivia-rich partial rejection'
    Remove-Item $nestedSources -Recurse
    Assert-Equal 0 @(Get-UnlinkedPartialTypeViolations $partialSourceRoot $linkedPatterns @('NET10_0_WINDOWS')).Count 'Linked partial acceptance'
    $testProject = Get-Content (Join-Path $root 'tests/TickDown.Tests/TickDown.Tests.csproj') -Raw
    Assert-Equal $false $testProject.Contains('src\ViewModels\*.cs') 'No broad ViewModel source-link glob'
    Assert-Equal $true $testProject.Contains('src\ViewModels\MainViewModel.cs') 'MainViewModel exact source link'
    Assert-Equal $true $testProject.Contains('src\ViewModels\TimerViewModel.cs') 'TimerViewModel exact source link'
    $collectorFixture = Join-Path $temp 'collector-fixture.dll'
    Add-Type -TypeDefinition 'namespace TickDown.ViewModels { public class MainViewModel { public class Nested {} } public class MainViewModelFake {} }' -OutputAssembly $collectorFixture
    $collectorViolations = @(Get-UnexpectedSourceLinkedTypes $collectorFixture @('TickDown.ViewModels.MainViewModel'))
    Assert-Equal 1 $collectorViolations.Count 'Collector sibling-prefix rejection'
    Assert-Equal $true $collectorViolations[0].Contains('MainViewModelFake') 'Collector violation identity'
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
