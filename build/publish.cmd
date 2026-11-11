@echo off
rem MOD73: the publish folder both packages take, the Inno installer (build-installer.cmd, MOD28)
rem and the Store's MSIX (build-msix.cmd), so the two can never ship different files.
rem
rem   build\publish.cmd
rem
rem 1. dotnet publish: modelado-cli, then the app, Release, win-x64, self-contained, into one
rem    folder. Both carry the same runtime, so they share it rather than each bundling a copy.
rem 2. The packages' own license and notice files into artifacts\publish\licenses.
rem No models: each operation downloads its own on first use, so the packages stay small.
setlocal

cd /d "%~dp0.."

set "PUBLISH=%CD%\artifacts\publish"

rem Nothing may be running from the folder this is about to empty and rewrite: the guard names the
rem pid and the path, and stops nothing on its own.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0guard-publish.ps1" -PublishDir "%PUBLISH%"
if errorlevel 1 exit /b 1

if exist "%PUBLISH%" rd /s /q "%PUBLISH%"

echo === Publishing modelado-cli and Modelado (Release, win-x64, self-contained) ===
dotnet publish tools\Modelado.Cli\Modelado.Cli.csproj --configuration Release --runtime win-x64 ^
    --self-contained true -p:DebugType=none --output "%PUBLISH%" --nologo
if errorlevel 1 (
    echo *** Publishing modelado-cli failed. ***
    exit /b 1
)
dotnet publish src\Modelado.App\Modelado.App.csproj --configuration Release --runtime win-x64 ^
    --self-contained true -p:DebugType=none --output "%PUBLISH%" --nologo
if errorlevel 1 (
    echo *** Publishing Modelado failed. ***
    exit /b 1
)

echo.
echo === Collecting third-party notices ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0collect-notices.ps1" -Out "%PUBLISH%\licenses"
if errorlevel 1 (
    echo *** Collecting the notices failed. ***
    exit /b 1
)
endlocal
