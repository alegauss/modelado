@echo off
rem MOD61: run-tests-vm.cmd for the cases on the real models. Carries this working tree to the
rem VMware guest and runs run-live.cmd there, which downloads the models on the guest (the tree
rem carries none) and runs cases-live. Not part of the gate: run it when a change touches what
rem those cases hold, a reconstruction, the provider or the CLI's parity.
rem
rem Usage:  run-live-vm.cmd [runner options]
setlocal

set "WINWRIGHT=%WINWRIGHT_HOME%"
if "%WINWRIGHT%"=="" set "WINWRIGHT=D:\Git\alegauss\winwright"
if not exist "%WINWRIGHT%\run-tests-vm.cmd" (
    echo winwright's run-tests-vm.cmd was not found in %WINWRIGHT%; set WINWRIGHT_HOME to its checkout.
    exit /b 3
)

set "CONFIG=%MODELADO_CONFIG%"
if "%CONFIG%"=="" set "CONFIG=Debug"

call "%WINWRIGHT%\run-tests-vm.cmd" -Tree "%~dp0." -Run "run-live.cmd %CONFIG%" -ResultsIn "TestResults\reports" %*
exit /b %ERRORLEVEL%
