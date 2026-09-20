#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../WindowsQuality.psm1" -Force
$required = @('quality', 'tests', 'coverage', 'mutation', 'architecture')
$passed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name expected '$Expected', got '$Actual'" }
    $script:passed++
}

function Assert-Rejected([string]$Json, [string]$Name) {
    try {
        $null = Assert-RequiredJobs -ResultsJson $Json -RequiredNames $required
        throw "$Name unexpectedly passed"
    }
    catch {
        if ($_.Exception.Message -like '*unexpectedly passed*') { throw }
        $script:passed++
    }
}

$clean = '{"quality":{"result":"success"},"tests":{"result":"success"},"coverage":{"result":"success"},"mutation":{"result":"success"},"architecture":{"result":"success"}}'
$rows = @(Assert-RequiredJobs -ResultsJson $clean -RequiredNames $required)
Assert-Equal 6 $rows.Count 'Temporary ruleset failure proof'
Assert-Equal 'architecture' $rows[4].Name 'Required order retained'
Assert-Rejected '{"quality":{"result":"failure"},"tests":{"result":"success"},"coverage":{"result":"success"},"mutation":{"result":"success"},"architecture":{"result":"success"}}' 'Failure'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"cancelled"},"coverage":{"result":"success"},"mutation":{"result":"success"},"architecture":{"result":"success"}}' 'Cancellation'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"success"},"coverage":{"result":"success"},"mutation":{"result":"success"},"architecture":{"result":"skipped"}}' 'Unexpected skip'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"success"},"coverage":{"result":"success"},"mutation":{"result":"failure"},"architecture":{"result":"success"}}' 'Mutation failure'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"success"},"coverage":{"result":"success"},"architecture":{"result":"success"}}' 'Missing mutation job'
Assert-Rejected 'not json' 'Malformed result document'
"Passed $passed Windows quality assertions."
