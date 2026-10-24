@echo off
setlocal enabledelayedexpansion

rem Builds Modelado.slnx with warnings as errors, then runs every test application in tests\
rem and the Python tests in scripts\tests. Green only if everything ran. The tests that use the
rem real models run when .cache\models exists (prepare-models.cmd) and say they skipped otherwise.
rem
rem Usage:  run-tests.cmd [Configuration]        default Debug

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"
pushd "%~dp0"

dotnet build Modelado.slnx --configuration %CONFIG% --nologo -v quiet
if errorlevel 1 (echo BUILD FAILED - no tests were run. & popd & exit /b 1)

set /a RAN=0
set /a FAILED=0
for /d %%P in (tests\*.Tests) do (
    set "APP=%%P\bin\%CONFIG%\net10.0\%%~nxP.exe"
    echo.
    echo === %%~nxP ===
    if exist "!APP!" (
        "!APP!"
        if errorlevel 1 set /a FAILED+=1
        set /a RAN+=1
    ) else (
        echo   missing: !APP!
        set /a FAILED+=1
    )
)

echo.
echo === scripts\tests (Python) ===
set "PY=python"
if exist ".venv\Scripts\python.exe" set "PY=.venv\Scripts\python.exe"
"%PY%" -W ignore::DeprecationWarning -m unittest discover -s scripts\tests
if errorlevel 1 set /a FAILED+=1
set /a RAN+=1

echo.
if %FAILED%==0 (echo All %RAN% suites passed. & popd & exit /b 0)
echo %FAILED% of %RAN% suites failed.
popd
exit /b 1
