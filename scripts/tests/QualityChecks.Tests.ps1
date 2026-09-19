#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../QualityChecks.psm1" -Force
$passed = 0
function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

$clean = '{"version":1,"projects":[{"path":"sample.csproj"}]}'
$findings = '{"version":1,"projects":[{"path":"sample.csproj","frameworks":[{"framework":"net10.0","topLevelPackages":[{"id":"Example","resolvedVersion":"1.0","latestVersion":"2.0"}]}]}]}'
$transitive = '{"version":1,"projects":[{"path":"sample.csproj","frameworks":[{"transitivePackages":[{"id":"Example","vulnerabilities":[{"severity":"High"}]}]}]}]}'
$inspect = { param($json) Test-PackageFindings $json }

$r = Invoke-QualityCheck 'clean package scan' { $clean } $inspect
Assert-Equal 'Pass' $r.Status 'Clean report'
$r = Invoke-QualityCheck 'outdated packages' { $findings } $inspect
Assert-Equal 'Findings' $r.Status 'Outdated report'
$r = Invoke-QualityCheck 'transitive vulnerabilities' { $transitive } $inspect
Assert-Equal 'Findings' $r.Status 'Transitive report'
$r = Invoke-QualityCheck 'source failure' { $global:LASTEXITCODE = 1; 'error NU1301: source unavailable' } $inspect
Assert-Equal 'ToolError' $r.Status 'Nonzero native exit'
Assert-Equal 1 $r.ExitCode 'Native code retained'
$r = Invoke-QualityCheck 'localized error' { $global:LASTEXITCODE = 7; 'Quelle nicht erreichbar' } $inspect
Assert-Equal 'ToolError' $r.Status 'Localized failure'
$r = Invoke-QualityCheck 'malformed response' { 'New output without JSON' } $inspect
Assert-Equal 'ToolError' $r.Status 'Malformed JSON'
$r = Invoke-QualityCheck 'empty projects' { '{"version":1,"projects":[]}' } $inspect
Assert-Equal 'ToolError' $r.Status 'Empty project inventory'
$r = Invoke-QualityCheck 'incomplete response' { '{"version":1,"projects":[{"path":"a"}],"problems":["feed unavailable"]}' } $inspect
Assert-Equal 'ToolError' $r.Status 'Incomplete scan'
$r = Invoke-QualityCheck 'missing executable' { throw 'Command not found' }
Assert-Equal 'ToolError' $r.Status 'Command exception'
$r = Invoke-QualityCheck 'independent check after failure' { 'success' }
Assert-Equal 'Pass' $r.Status 'Subsequent check executes'
Assert-Equal 0 $r.ExitCode 'Stale exit code reset'
$npmInspect = { param($json) Test-NpmAuditFindings $json }
$npmClean = '{"auditReportVersion":2,"metadata":{"vulnerabilities":{"info":0,"low":0,"moderate":0,"high":0,"critical":0,"total":0}}}'
$npmFindings = '{"auditReportVersion":2,"metadata":{"vulnerabilities":{"info":0,"low":1,"moderate":0,"high":0,"critical":0,"total":1}}}'
$r = Invoke-QualityCheck 'clean npm audit' { $npmClean } $npmInspect -FindingExitCodes 1
Assert-Equal 'Pass' $r.Status 'Clean npm report'
$r = Invoke-QualityCheck 'npm vulnerability' { $global:LASTEXITCODE = 1; $npmFindings } $npmInspect -FindingExitCodes 1
Assert-Equal 'Findings' $r.Status 'npm findings with exit 1'
$r = Invoke-QualityCheck 'npm registry failure' { $global:LASTEXITCODE = 1; '{"error":{"code":"ENOTFOUND"}}' } $npmInspect -FindingExitCodes 1
Assert-Equal 'ToolError' $r.Status 'npm registry failure'
$r = Invoke-QualityCheck 'inconsistent npm exit' { $global:LASTEXITCODE = 1; $npmClean } $npmInspect -FindingExitCodes 1
Assert-Equal 'ToolError' $r.Status 'Nonzero npm exit without findings'
$r = Invoke-QualityCheck 'unexpected npm exit' { $global:LASTEXITCODE = 2; $npmFindings } $npmInspect -FindingExitCodes 1
Assert-Equal 'ToolError' $r.Status 'Unexpected exit remains failure'
$r = Invoke-QualityCheck 'informational npm advisory' { $npmClean.Replace('"info":0', '"info":1').Replace('"total":0', '"total":1') } $npmInspect
Assert-Equal 'Pass' $r.Status 'Below-low severity does not fail'
$r = Invoke-QualityCheck 'missing npm severity counts' { '{"auditReportVersion":2,"metadata":{"vulnerabilities":{"total":0}}}' } $npmInspect
Assert-Equal 'ToolError' $r.Status 'Incomplete severity report fails'
$PSNativeCommandUseErrorActionPreference = $true
$r = Invoke-QualityCheck 'native stderr diagnostic' { pwsh -NoProfile -File "$PSScriptRoot/fixtures/native-report.ps1" } $inspect
Assert-Equal 'Pass' $r.Status 'Benign stderr does not corrupt JSON'
Assert-Equal $true ($r.ErrorOutput -like '*benign diagnostic*') 'stderr retained separately'
$r = Invoke-QualityCheck 'native failure with valid stdout' { pwsh -NoProfile -File "$PSScriptRoot/fixtures/native-report.ps1" -ExitCode 7 } $inspect
Assert-Equal 'ToolError' $r.Status 'Native failure still fails with valid JSON'
Assert-Equal 7 $r.ExitCode 'Native failure exit retained'
"Passed $passed quality-runner assertions."
