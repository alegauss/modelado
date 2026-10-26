@echo off
setlocal

rem MOD61: the UI cases on the real models (cases-live), run on the VMware guest by run-live-vm.cmd
rem and never by the gate. Builds the solution, downloads every model into the default models
rem folder (each file verified by SHA-256, and one already there kept), then runs the cases under
rem winwright.live.json's longer waits.
rem
rem Usage:  run-live.cmd [Configuration]        default Debug

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

pushd "%~dp0"

set "REPORTS=%CD%\TestResults\reports"
if exist "%REPORTS%" rd /s /q "%REPORTS%"
mkdir "%REPORTS%" 2>nul

dotnet build Modelado.slnx --configuration %CONFIG% --nologo -v quiet
if errorlevel 1 (echo BUILD FAILED - no case was run. & popd & exit /b 1)

echo.
echo === models ===
tools\Modelado.Cli\bin\%CONFIG%\net10.0\modelado-cli.exe download all --provider cpu
if errorlevel 1 (echo the models could not be downloaded - no case was run. & popd & exit /b 1)

echo.
echo === cases-live ===
set "MODELADO_LIVE=1"
tests\Modelado.Cases\bin\%CONFIG%\net10.0-windows\Modelado.Cases.exe --results-directory "%REPORTS%" --hangdump --hangdump-timeout 20m --hangdump-type Mini
set "CODE=%ERRORLEVEL%"
popd
exit /b %CODE%
