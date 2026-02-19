# Safe Git Push Script - Handles lock files automatically
# Usage: .\git-push-safe.ps1

$ErrorActionPreference = "Stop"

# Change to repo root
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "🔧 Cleaning up Git lock files..." -ForegroundColor Yellow

# Remove all Git lock files
$lockFiles = @(
    ".git\index.lock",
    ".git\config.lock"
)

foreach ($lockFile in $lockFiles) {
    try {
        if (Test-Path $lockFile) {
            Remove-Item $lockFile -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Removed: $lockFile" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ⚠ Could not remove $lockFile (may be in use)" -ForegroundColor Yellow
    }
}

Write-Host "`n🚀 Pushing to remote..." -ForegroundColor Cyan
git push origin master

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n❌ Push failed" -ForegroundColor Red
    exit 1
}

Write-Host "`n✅ Push successful!" -ForegroundColor Green
