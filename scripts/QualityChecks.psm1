Set-StrictMode -Version Latest

function Invoke-QualityCheck {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Command,
        [scriptblock]$InspectOutput,
        [int[]]$FindingExitCodes = @()
    )

    # Native exits are classified below, including npm's documented findings exit.
    $commandVariables = [System.Management.Automation.PSVariable[]]@(
        [System.Management.Automation.PSVariable]::new('PSNativeCommandUseErrorActionPreference', $false)
    )
    Write-Host "`nChecking $Name..."
    $exitCode = 0
    $status = 'Pass'
    $output = ''
    $errorOutput = ''
    $stderrPath = $null
    try {
        $stderrPath = [IO.Path]::GetTempFileName()
        $global:LASTEXITCODE = 0
        $output = ($Command.InvokeWithContext($null, $commandVariables, @()) 2> $stderrPath | Out-String).Trim()
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

    finally {
        if ($stderrPath) {
            $errorOutput = (Get-Content $stderrPath -Raw -ErrorAction SilentlyContinue) ?? ''
            Remove-Item $stderrPath -ErrorAction SilentlyContinue
        }
    }

    if ($output) { Write-Host $output }
    if ($errorOutput) { Write-Host $errorOutput }
    [pscustomobject]@{
        Name = $Name
        Status = $status
        ExitCode = $exitCode
        Output = $output
        ErrorOutput = $errorOutput
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
    $counts = $report.metadata.vulnerabilities
    foreach ($severity in @('info', 'low', 'moderate', 'high', 'critical', 'total')) {
        if (!$counts.ContainsKey($severity) -or
            ($counts[$severity] -isnot [int] -and $counts[$severity] -isnot [long]) -or
            $counts[$severity] -lt 0) {
            throw "npm audit returned an invalid $severity count."
        }
    }
    return ($counts.low + $counts.moderate + $counts.high + $counts.critical) -gt 0
}

Export-ModuleMember -Function Invoke-QualityCheck, Test-PackageFindings, Test-NpmAuditFindings
