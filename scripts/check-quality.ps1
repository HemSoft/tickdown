$ErrorActionPreference = "Stop"

function Invoke-Check {
    param(
        [string]$Name,
        [scriptblock]$Command,
        [string]$FailPattern = $null
    )
    
    Write-Host "`nChecking for $Name..." -ForegroundColor Cyan
    $output = & $Command
    
    $failed = if ($FailPattern) { $output -match $FailPattern } else { $LASTEXITCODE -ne 0 }
    
    if ($failed) {
        if ($output) { Write-Host ($output | Out-String) }
        Write-Host "$Name issues found." -ForegroundColor Yellow
        exit 1
    }
    Write-Host "No $Name issues found." -ForegroundColor Green
}

Invoke-Check "code style"          { dotnet format style --verify-no-changes }
Invoke-Check "whitespace"          { dotnet format whitespace --verify-no-changes }
Invoke-Check "analyzer"            { dotnet format analyzers --verify-no-changes }
Invoke-Check "vulnerable packages" { dotnet list package --vulnerable } "has the following vulnerable packages"
Invoke-Check "outdated packages"   { dotnet list package --outdated }   "has the following updates"
