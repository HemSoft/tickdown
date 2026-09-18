Set-StrictMode -Version Latest

function Invoke-QualityCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [scriptblock]$InspectOutput
    )

    Write-Host "`nChecking $Name..."
    $exitCode = 0
    $status = 'Pass'
    $output = ''
    try {
        $global:LASTEXITCODE = 0
        $output = (& $Command 2>&1 | Out-String).Trim()
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $status = 'ToolError'
        }
        elseif ($InspectOutput -and (& $InspectOutput $output)) {
            $status = 'Findings'
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

Export-ModuleMember -Function Invoke-QualityCheck, Test-PackageFindings
