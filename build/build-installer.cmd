@echo off
rem MOD28: publishes Modelado self-contained and builds its per-user installer with Inno Setup,
rem after PhotoBoost's build\build-installer.cmd.
rem
rem   build\build-installer.cmd
rem
rem 1. build\publish.cmd: the app and modelado-cli, self-contained, with the packages' notices, into
rem    artifacts\publish (MOD73 shares it with the Store's MSIX).
rem 2. ISCC compiles build\installer.iss into artifacts\installer\Modelado-Setup-<version>.exe.
setlocal

cd /d "%~dp0.."

if exist "%CD%\artifacts\installer" rd /s /q "%CD%\artifacts\installer"

call "%~dp0publish.cmd"
if errorlevel 1 (
    echo *** No installer was built. ***
    exit /b 1
)

rem Inno Setup 6, machine-wide or per user, or on PATH.
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%I"
if not defined ISCC (
    echo *** ISCC.exe not found. Install Inno Setup 6: https://jrsoftware.org/isdl.php ***
    exit /b 1
)

echo.
echo === Building the installer ===
"%ISCC%" /Q "%~dp0installer.iss"
if errorlevel 1 (
    echo *** The installer did not compile. ***
    exit /b 1
)

for %%F in ("%CD%\artifacts\installer\Modelado-Setup-*.exe") do echo Installer: %%F
endlocal
