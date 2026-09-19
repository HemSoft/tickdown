#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
Import-Module "$PSScriptRoot/../WindowsQuality.psm1" -Force
$required = @('quality', 'tests', 'architecture')
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

$clean = '{"quality":{"result":"success"},"tests":{"result":"success"},"architecture":{"result":"success"}}'
$rows = @(Assert-RequiredJobs -ResultsJson $clean -RequiredNames $required)
Assert-Equal 3 $rows.Count 'All required jobs returned'
Assert-Equal 'architecture' $rows[2].Name 'Required order retained'
Assert-Rejected '{"quality":{"result":"failure"},"tests":{"result":"success"},"architecture":{"result":"success"}}' 'Failure'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"cancelled"},"architecture":{"result":"success"}}' 'Cancellation'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"success"},"architecture":{"result":"skipped"}}' 'Unexpected skip'
Assert-Rejected '{"quality":{"result":"success"},"tests":{"result":"success"}}' 'Missing matrix job'
Assert-Rejected 'not json' 'Malformed result document'
"Passed $passed Windows quality assertions."
