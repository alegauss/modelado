@echo off
rem MOD73: builds Modelado's Microsoft Store package, after claude-tray's build\build-msix.cmd (T409).
rem
rem   build\build-msix.cmd              writes artifacts\msix\Modelado.msix (unsigned: the Store signs it)
rem   build\build-msix.cmd -Register    also installs the layout on this machine (Developer Mode)
rem
rem build\publish.cmd makes the same folder the Inno installer takes, and build-msix.ps1 packs it with
rem build\msix\AppxManifest.xml, the logos tools\Modelado.Icon draws and the Windows SDK's makeappx.
setlocal

cd /d "%~dp0.."

call "%~dp0publish.cmd"
if errorlevel 1 (
    echo *** No package was built. ***
    exit /b 1
)

echo.
echo === Packing the MSIX ===
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-msix.ps1" %*
if errorlevel 1 (
    echo *** Packing the MSIX failed. ***
    exit /b 1
)
endlocal
