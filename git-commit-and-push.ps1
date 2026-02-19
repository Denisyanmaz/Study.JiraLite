# Git Commit and Push Script - Handles Windows lock file issues
# Run this script OUTSIDE of Cursor/VS Code in a regular PowerShell window
# Usage: .\git-commit-and-push.ps1

param(
    [string]$CommitMessage = "Enhance member management and activity logging

- Add activity logging for MemberAdded, MemberRemoved, and MemberLeft actions
- Display former members (grayed out) for project owners in Members tab
- Show actor emails instead of IDs in activity logs (Project and Task views)
- Add MemberAdded, MemberRemoved, MemberLeft to activity filter dropdown
- Prevent assigning tasks to former/removed members (UI and backend validation)
- Remove User IDs from member activity log messages for cleaner display
- Add helper method to clean User IDs from legacy activity log entries"
)

$ErrorActionPreference = "Continue"

# Get script directory and change to repo root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Git Commit and Push Script" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Step 1: Clean up lock files
Write-Host "[1/4] Cleaning up Git lock files..." -ForegroundColor Yellow

$lockPaths = @(
    ".git\index.lock",
    ".git\config.lock"
)

foreach ($lockPath in $lockPaths) {
    $fullPath = Join-Path $scriptDir $lockPath
    if (Test-Path $fullPath) {
        try {
            # Try to remove with retries
            $retries = 3
            $removed = $false
            for ($i = 1; $i -le $retries; $i++) {
                try {
                    Remove-Item $fullPath -Force -ErrorAction Stop
                    Write-Host "  ✓ Removed: $lockPath" -ForegroundColor Green
                    $removed = $true
                    break
                } catch {
                    if ($i -lt $retries) {
                        Start-Sleep -Milliseconds 200
                    }
                }
            }
            if (-not $removed) {
                Write-Host "  ⚠ Could not remove $lockPath after $retries attempts" -ForegroundColor Yellow
                Write-Host "     This usually means Cursor/VS Code is holding it." -ForegroundColor Yellow
                Write-Host "     Please close Cursor/VS Code and try again." -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  ⚠ Error removing $lockPath : $_" -ForegroundColor Yellow
        }
    }
}

# Clean up temp object files
try {
    Get-ChildItem -Path ".git\objects" -Recurse -Filter "tmp_obj_*" -ErrorAction SilentlyContinue | 
        Remove-Item -Force -ErrorAction SilentlyContinue
} catch {
    # Ignore
}

Start-Sleep -Milliseconds 300

# Step 2: Stage changes
Write-Host "`n[2/4] Staging changes..." -ForegroundColor Yellow
try {
    git add -A 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Changes staged" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Git add had issues (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
        Write-Host "     Trying to continue anyway..." -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ❌ Failed to stage: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Commit
Write-Host "`n[3/4] Committing..." -ForegroundColor Yellow
try {
    git commit -m $CommitMessage 2>&1 | Tee-Object -Variable commitOutput
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Commit successful!" -ForegroundColor Green
    } else {
        # Check if commit failed due to lock
        if ($commitOutput -match "index.lock|File exists") {
            Write-Host "`n  ❌ Commit failed due to lock file" -ForegroundColor Red
            Write-Host "`n  SOLUTION:" -ForegroundColor Yellow
            Write-Host "  1. Close Cursor/VS Code completely" -ForegroundColor White
            Write-Host "  2. Run this script again in a regular PowerShell window" -ForegroundColor White
            Write-Host "  3. Or manually run: Remove-Item .git\index.lock -Force" -ForegroundColor White
            exit 1
        } else {
            Write-Host "  ❌ Commit failed: $commitOutput" -ForegroundColor Red
            exit 1
        }
    }
} catch {
    Write-Host "  ❌ Commit error: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Push
Write-Host "`n[4/4] Pushing to remote..." -ForegroundColor Yellow
try {
    git push origin master 2>&1 | Tee-Object -Variable pushOutput
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Push successful!" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ Push had issues: $pushOutput" -ForegroundColor Yellow
        Write-Host "     You may need to push manually: git push origin master" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠ Push error: $_" -ForegroundColor Yellow
    Write-Host "     You can push manually later: git push origin master" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Done!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
