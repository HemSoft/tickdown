Set-StrictMode -Version Latest

function Assert-RequiredJobs {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ResultsJson,
        [Parameter(Mandatory)][string[]]$RequiredNames
    )

    $results = ConvertFrom-Json -InputObject $ResultsJson -AsHashtable -ErrorAction Stop
    $missing = @($RequiredNames | Where-Object { !$results.ContainsKey($_) })
    if ($missing.Count -gt 0) {
        throw "Required child jobs were absent: $($missing -join ', ')"
    }

    $rows = @($RequiredNames | ForEach-Object {
        $result = $results[$_].result
        [pscustomobject]@{ Name = $_; Result = $result }
    })
    $failed = @($rows | Where-Object Result -ne 'success')
    if ($failed.Count -gt 0) {
        throw "Required child jobs did not succeed: $(($failed.Name) -join ', ')"
    }

    return $rows
}

Export-ModuleMember -Function Assert-RequiredJobs
