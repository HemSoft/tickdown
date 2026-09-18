#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Import-Module "$PSScriptRoot/QualityChecks.psm1" -Force

Push-Location (Split-Path $PSScriptRoot)
try {
    $results = @(
        Invoke-QualityCheck 'code style' { dotnet format style --verify-no-changes }
        Invoke-QualityCheck 'whitespace' { dotnet format whitespace --verify-no-changes }
        Invoke-QualityCheck 'analyzers' { dotnet format analyzers --verify-no-changes }
        Invoke-QualityCheck 'vulnerable packages' {
            dotnet list package --vulnerable --include-transitive --format json
        } { param($json) Test-PackageFindings $json }
        Invoke-QualityCheck 'outdated packages' {
            dotnet list package --outdated --format json
        } { param($json) Test-PackageFindings $json }
        Invoke-QualityCheck 'Markdown' { npm run lint:md }
        Invoke-QualityCheck 'npm vulnerabilities' {
            npm audit --audit-level=low --json
        } { param($json) Test-NpmAuditFindings $json } -FindingExitCodes 1
    )
    $results | Select-Object Name, Status, ExitCode | Format-Table -AutoSize | Out-Host
    if (@($results | Where-Object Status -ne 'Pass').Count -gt 0) { exit 1 }
    exit 0
}
finally {
    Pop-Location
}
