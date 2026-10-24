@echo off
setlocal enabledelayedexpansion

rem Builds Modelado.slnx with warnings as errors, then runs every test application in tests\
rem and the Python tests in scripts\tests. Green only if everything ran. The tests that use the
rem real models run when .cache\models exists (prepare-models.cmd) and say they skipped otherwise.
rem
rem It is run on the VMware guest, not on the desk you work at: run-tests-vm.cmd carries this tree
rem there (MOD14), since the UI cases take real windows and the foreground.
rem
rem Usage:  run-tests.cmd [Configuration]        default Debug

set "CONFIG=%~1"
if "%CONFIG%"=="" set "CONFIG=Debug"

rem How long one test may run before it is taken as hung, ended and named.
if "%MODELADO_HANG%"=="" set "MODELADO_HANG=10m"

pushd "%~dp0"

set "REPORTS=%CD%\TestResults\reports"
if exist "%REPORTS%" rd /s /q "%REPORTS%"
mkdir "%REPORTS%" 2>nul

dotnet build Modelado.slnx --configuration %CONFIG% --nologo -v quiet
if errorlevel 1 (echo BUILD FAILED - no tests were run. & popd & exit /b 1)

set /a RAN=0
set /a FAILED=0
rem The *.Tests folders and the winwright UI cases (MOD8), which skip on a desk that cannot be
rem observed. The cases are net10.0-windows, the rest net10.0.
for /d %%P in (tests\*.Tests tests\*.Cases) do (
    set "APP=%%P\bin\%CONFIG%\net10.0\%%~nxP.exe"
    if not exist "!APP!" set "APP=%%P\bin\%CONFIG%\net10.0-windows\%%~nxP.exe"
    echo.
    echo === %%~nxP ===
    if exist "!APP!" (
        "!APP!" --results-directory "%REPORTS%" --hangdump --hangdump-timeout %MODELADO_HANG% --hangdump-type Mini
        if errorlevel 1 set /a FAILED+=1
        set /a RAN+=1
    ) else (
        echo   missing: !APP!
        set /a FAILED+=1
    )
)

echo.
echo === scripts\tests (Python) ===
rem The interpreter: .venv's when prepare-models.cmd made one, else a python on PATH that runs, else
rem one Python's installer registered (the last reg lists, the newest). The last is for a per-user install never added to
rem PATH, where `python` is only the Microsoft Store stub and fails with 9009 (the VMware guest's case).
set "PY="
if exist ".venv\Scripts\python.exe" set "PY=.venv\Scripts\python.exe"
if not defined PY python -c "import sys" >nul 2>&1 && set "PY=python"
if not defined PY for %%K in (HKLM HKCU) do (
    for /f "tokens=2,*" %%A in ('reg query "%%K\SOFTWARE\Python\PythonCore" /s /v ExecutablePath 2^>nul ^| findstr /i "ExecutablePath"') do set "PY=%%B"
)
if not defined PY (
    echo   no Python found: neither .venv, a python on PATH, nor one in the registry
    set /a FAILED+=1
    set /a RAN+=1
    goto :python_done
)
echo   using !PY!
"!PY!" -W ignore::DeprecationWarning -m unittest discover -s scripts\tests
if errorlevel 1 set /a FAILED+=1
set /a RAN+=1
:python_done

echo.
if %FAILED%==0 (echo All %RAN% suites passed. & popd & exit /b 0)
echo %FAILED% of %RAN% suites failed.
popd
exit /b 1
