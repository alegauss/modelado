@echo off
rem MOD29: sets the version, gates it, builds the installer and tags the release, after PhotoBoost's
rem update-release.cmd. Pushing the tag is what publishes: .github\workflows\release.yml builds it
rem again on a clean runner and creates the GitHub release with the installer and SHA256SUMS.
rem
rem   build\update-release 0.2.0          everything up to a local commit and tag
rem   build\update-release 0.2.0 push     and pushes the branch and the tag
rem
rem 1. The version, in Directory.Build.props and app.manifest (update-release.ps1).
rem 2. The whole suite on the VMware guest (run-tests-vm.cmd, MOD14): no release from a red tree.
rem 3. The installer (build-installer.cmd), and release-files.ps1, which refuses a version the
rem    build or the installer's name disagrees with and writes artifacts\release.
rem 4. The version commit, through run-commit.cmd, and the tag v<x.y.z>. run-commit.cmd stages the
rem    whole tree, so start from a clean one.
setlocal

cd /d "%~dp0.."

if "%~1"=="" (
    echo Usage: build\update-release 0.2.0 [push]
    exit /b 1
)
set "VERSION=%~1"

git rev-parse -q --verify "refs/tags/v%VERSION%" >nul
if not errorlevel 1 (
    echo *** The tag v%VERSION% already exists. ***
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update-release.ps1" -Version "%VERSION%"
if errorlevel 1 exit /b 1

call "%~dp0..\run-tests-vm.cmd"
if errorlevel 1 (
    echo *** The suite failed on the guest; no installer was built. ***
    exit /b 1
)

call "%~dp0build-installer.cmd"
if errorlevel 1 exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0release-files.ps1" -Version "%VERSION%"
if errorlevel 1 exit /b 1

rem Nothing to commit when the version already was this one, the first release for example.
git diff --quiet
if errorlevel 1 (
    call run-commit.cmd -m "chore: release v%VERSION%"
    if errorlevel 1 exit /b 1
)

git tag "v%VERSION%"
if errorlevel 1 exit /b 1

if /i not "%~2"=="push" (
    echo.
    echo Release v%VERSION% is built and tagged locally; artifacts\release holds what it will publish.
    echo To publish:  git push ^&^& git push origin v%VERSION%
    exit /b 0
)

git push
if errorlevel 1 exit /b 1
git push origin "v%VERSION%"
if errorlevel 1 exit /b 1
echo Pushed v%VERSION%: release.yml publishes it.
endlocal
