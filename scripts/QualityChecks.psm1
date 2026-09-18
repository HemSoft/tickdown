Set-StrictMode -Version Latest

function Invoke-QualityCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [scriptblock]$InspectOutput,
        [int[]]$FindingExitCodes = @()
    )

    Write-Host "`nChecking $Name..."
    $exitCode = 0
    $status = 'Pass'
    $output = ''
    try {
        $global:LASTEXITCODE = 0
        $output = (& $Command 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
        if ($InspectOutput -and ($exitCode -eq 0 -or $FindingExitCodes -contains $exitCode)) {
            if (& $InspectOutput $output) { $status = 'Findings' }
            elseif ($exitCode -ne 0) { $status = 'ToolError' }
        }
        elseif ($exitCode -ne 0) {
            $status = 'ToolError'
        }
    }
    catch {
        $status = 'ToolError'
        $output = "$output`n$($_.Exception.Message)".Trim()
        if ($exitCode -eq 0) { $exitCode = 1 }
    }

    if ($output) { Write-Host $output }
    [pscustomobject]@{
        Name = $Name
        Status = $status
        ExitCode = $exitCode
        Output = $output
    }
}

function Test-PackageFindings {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Json)

    $report = ConvertFrom-Json -InputObject $Json -AsHashtable -ErrorAction Stop
    if ($report.version -ne 1 -or !$report.ContainsKey('projects')) {
        throw 'Package command returned an unsupported or incomplete JSON report.'
    }
    if ($report.ContainsKey('problems') -and @($report.problems).Count -gt 0) {
        throw 'Package inspection reported problems; the scan is incomplete.'
    }
    if (@($report.projects).Count -eq 0) {
        throw 'Package inspection returned no projects.'
    }

    foreach ($project in $report.projects) {
        if (!$project.ContainsKey('frameworks')) { continue }
        foreach ($framework in $project.frameworks) {
            if (($framework.ContainsKey('topLevelPackages') -and @($framework.topLevelPackages).Count -gt 0) -or
                ($framework.ContainsKey('transitivePackages') -and @($framework.transitivePackages).Count -gt 0)) {
                return $true
            }
        }
    }
    return $false
}

function Test-NpmAuditFindings {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Json)

    $report = ConvertFrom-Json -InputObject $Json -AsHashtable -ErrorAction Stop
    if ($report.ContainsKey('error') -or $report.auditReportVersion -ne 2 -or
        !$report.ContainsKey('metadata') -or !$report.metadata.ContainsKey('vulnerabilities') -or
        !$report.metadata.vulnerabilities.ContainsKey('total')) {
        throw 'npm audit returned an error or incomplete report.'
    }
    return $report.metadata.vulnerabilities.total -gt 0
}

Export-ModuleMember -Function Invoke-QualityCheck, Test-PackageFindings, Test-NpmAuditFindings
