# Safe Git Commit Script - Handles lock files automatically
# Usage: .\git-commit-safe.ps1 "Your commit message"

param(
    [Parameter(Mandatory=$true)]
    [string]$CommitMessage
)

$ErrorActionPreference = "Stop"

# Change to repo root
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

Write-Host "🔧 Cleaning up Git lock files..." -ForegroundColor Yellow

# Remove all Git lock files
$lockFiles = @(
    ".git\index.lock",
    ".git\config.lock",
    ".git\refs\heads\*.lock",
    ".git\objects\*\tmp_obj_*"
)

foreach ($pattern in $lockFiles) {
    $files = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue -Recurse
    foreach ($file in $files) {
        try {
            Remove-Item -Path $file.FullName -Force -ErrorAction SilentlyContinue
            Write-Host "  ✓ Removed: $($file.Name)" -ForegroundColor Green
        } catch {
            # Ignore errors - file might be in use
        }
    }
}

# Also try to remove index.lock directly
try {
    if (Test-Path ".git\index.lock") {
        Remove-Item ".git\index.lock" -Force -ErrorAction SilentlyContinue
    }
} catch {
    Write-Host "  ⚠ Could not remove index.lock (may be in use)" -ForegroundColor Yellow
}

Write-Host "`n📦 Staging changes..." -ForegroundColor Cyan
git add -A

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to stage changes" -ForegroundColor Red
    exit 1
}

Write-Host "`n💾 Committing..." -ForegroundColor Cyan
git commit -m $CommitMessage

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Commit failed" -ForegroundColor Red
    exit 1
}

Write-Host "`n✅ Commit successful!" -ForegroundColor Green
Write-Host "`nTo push: git push origin master" -ForegroundColor Cyan
