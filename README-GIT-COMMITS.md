# Git Commit Issues - Solution Guide

## Problem
Git operations fail with lock file errors like:
- `fatal: Unable to create '.git/index.lock': File exists`
- `Permission denied` errors

## Root Cause
Cursor/VS Code or other processes are holding Git lock files, preventing commits.

## Solutions

### Solution 1: Use the Helper Script (Recommended)
Run the script **OUTSIDE of Cursor** in a regular PowerShell window:

```powershell
cd C:\Denis_dotnet\StudyProjects\DenoLite
.\git-commit-and-push.ps1
```

This script automatically:
- Removes lock files
- Stages changes
- Commits with a proper message
- Pushes to remote

### Solution 2: Manual Fix
If the script doesn't work:

1. **Close Cursor/VS Code completely**
2. Open a **new PowerShell window** (not in Cursor)
3. Run:
```powershell
cd C:\Denis_dotnet\StudyProjects\DenoLite
Remove-Item .git\index.lock -Force -ErrorAction SilentlyContinue
Remove-Item .git\config.lock -Force -ErrorAction SilentlyContinue
git add -A
git commit -m "Your commit message"
git push origin master
```

### Solution 3: Use Cursor's Source Control Panel
1. Open Source Control panel in Cursor
2. Stage your changes
3. Enter commit message
4. Click "Commit"
5. Click "Sync" or "Push" button

## Prevention
- Avoid running multiple Git operations simultaneously
- Close Cursor before running Git commands in terminal
- Use Cursor's built-in Git UI when possible

## Files Created
- `git-commit-and-push.ps1` - Automated commit and push script
- `git-commit-safe.ps1` - Safe commit script (alternative)
- `git-push-safe.ps1` - Safe push script (alternative)
