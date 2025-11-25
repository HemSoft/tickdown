$ErrorActionPreference = "Stop"

Write-Host "Checking for code style issues..." -ForegroundColor Cyan
dotnet format style --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "Code style issues found." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "No code style issues found." -ForegroundColor Green
}

Write-Host "`nChecking for whitespace issues..." -ForegroundColor Cyan
dotnet format whitespace --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "Whitespace issues found." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "No whitespace issues found." -ForegroundColor Green
}

Write-Host "`nChecking for analyzer issues..." -ForegroundColor Cyan
dotnet format analyzers --verify-no-changes
if ($LASTEXITCODE -ne 0) {
    Write-Host "Analyzer issues found." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "No analyzer issues found." -ForegroundColor Green
}

Write-Host "`nChecking for package vulnerabilities..." -ForegroundColor Cyan
$vulnerabilityOutput = dotnet list package --vulnerable
Write-Host ($vulnerabilityOutput | Out-String)
if ($vulnerabilityOutput -match "has the following vulnerable packages") {
    Write-Host "Package vulnerabilities found." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "No package vulnerabilities found." -ForegroundColor Green
}

Write-Host "`nChecking for outdated packages..." -ForegroundColor Cyan
$outdatedOutput = dotnet list package --outdated
Write-Host ($outdatedOutput | Out-String)
if ($outdatedOutput -match "has the following updates") {
    Write-Host "Outdated packages found." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "All packages are up to date." -ForegroundColor Green
}
