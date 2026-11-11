@echo off
setlocal

rem MOD43: the Blender add-on's test, headless, against the modelado-cli this tree built.
rem Blender is MODELADO_BLENDER when set, else a portable one under %LOCALAPPDATA%\Programs (the
rem VMware guest's), else the installed one. Exit 0 and a "skipped" line where there is none.
rem
rem Usage:  blender\tests\run-blender-tests.cmd [Configuration]        default Debug

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"
pushd "%~dp0..\.."

set "BLENDER=%MODELADO_BLENDER%"
if not defined BLENDER for /d %%B in ("%LOCALAPPDATA%\Programs\blender-*") do if exist "%%B\blender.exe" set "BLENDER=%%B\blender.exe"
if not defined BLENDER for /d %%B in ("%ProgramFiles%\Blender Foundation\Blender*") do if exist "%%B\blender.exe" set "BLENDER=%%B\blender.exe"
if not defined BLENDER (
    echo   skipped: no Blender found; set MODELADO_BLENDER to run it
    popd & exit /b 0
)

rem MOD70: the manifest against extensions.blender.org's own checks, before anything is submitted.
echo   using %BLENDER%
"%BLENDER%" --factory-startup --command extension validate blender\modelado
if errorlevel 1 (echo   the add-on's manifest fails Blender's extension validate & popd & exit /b 1)

set "CLI=%CD%\tools\Modelado.Cli\bin\%CONFIG%\net10.0\modelado-cli.exe"
if not exist "%CLI%" (echo   missing: %CLI% & popd & exit /b 1)

"%BLENDER%" --background --factory-startup --python-exit-code 1 --python blender\tests\test_addon.py -- "%CLI%"
set "CODE=%ERRORLEVEL%"
popd
exit /b %CODE%
