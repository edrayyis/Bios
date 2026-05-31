@echo off
rem ============================================================================
rem  promote-to-remote.bat
rem
rem  Stages all local changes, commits them, and pushes to the remote so that
rem  Streamlit Cloud picks up the new revision and redeploys the
rem  BIOS Recovery Manager app.
rem
rem  Usage:
rem    promote-to-remote.bat                 Prompt for a commit message
rem    promote-to-remote.bat "Your message"  Use the supplied commit message
rem
rem  Exit codes:
rem    0  Success (changes pushed, or nothing to commit)
rem    1  Not a git repository / git not found
rem    2  Commit failed
rem    3  Push failed
rem ============================================================================

setlocal EnableDelayedExpansion

rem -- Run from the repository root, regardless of where the script is invoked --
pushd "%~dp0\.."

rem -- Make sure git is available -----------------------------------------------
where git >nul 2>&1
if errorlevel 1 (
    echo [ERROR] git was not found on your PATH.
    echo         Install Git for Windows: https://git-scm.com/download/win
    popd
    exit /b 1
)

rem -- Make sure we are inside a git work tree ----------------------------------
git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo [ERROR] This folder is not a git repository.
    popd
    exit /b 1
)

rem -- Figure out the current branch --------------------------------------------
for /f "delims=" %%B in ('git rev-parse --abbrev-ref HEAD') do set "BRANCH=%%B"
echo.
echo === Promote to remote =======================================================
echo  Repository : %CD%
echo  Branch     : %BRANCH%
echo =============================================================================
echo.

rem -- Bail out early if there is nothing to do ---------------------------------
git status --porcelain >"%TEMP%\_ptr_status.txt"
for %%A in ("%TEMP%\_ptr_status.txt") do set "CHANGESIZE=%%~zA"
del "%TEMP%\_ptr_status.txt" >nul 2>&1
if "!CHANGESIZE!"=="0" (
    echo [INFO] No local changes to commit. Pushing branch in case the remote
    echo        is behind...
    goto :push
)

rem -- Show what will be committed ----------------------------------------------
echo The following changes will be committed:
echo -----------------------------------------------------------------------------
git status --short
echo -----------------------------------------------------------------------------
echo.

rem -- Determine the commit message ---------------------------------------------
set "MSG=%~1"
if "!MSG!"=="" (
    set /p "MSG=Commit message: "
)
if "!MSG!"=="" (
    rem Fall back to a timestamped message if the user entered nothing
    set "MSG=Update !DATE! !TIME!"
)

rem -- Stage and commit ---------------------------------------------------------
git add -A
if errorlevel 1 (
    echo [ERROR] git add failed.
    popd
    exit /b 2
)

git commit -m "!MSG!"
if errorlevel 1 (
    echo [ERROR] git commit failed.
    popd
    exit /b 2
)

:push
echo.
echo Pushing to origin/%BRANCH% ...
git push -u origin "%BRANCH%"
if errorlevel 1 (
    echo [ERROR] git push failed. Resolve the issue above and try again.
    popd
    exit /b 3
)

echo.
echo [OK] Done. origin/%BRANCH% is up to date.
echo      Streamlit Cloud will redeploy automatically.
popd
endlocal
exit /b 0
