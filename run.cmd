@echo off
setlocal

rem Builds Modelado (Debug) and starts it. Arguments go to the app, for example
rem   .\run.cmd --open photo.jpg --provider cpu
rem The models prepare-models.cmd left in .cache\models are used unless --models is given;
rem without that folder the app downloads them from Hugging Face on first use.

pushd "%~dp0"
dotnet build src\Modelado.App\Modelado.App.csproj --configuration Debug --nologo -v quiet || (popd & exit /b 1)

set "EXE=%CD%\src\Modelado.App\bin\Debug\net10.0-windows\win-x64\Modelado.exe"
set "MODELS="
echo %* | findstr /i /c:"--models" >nul
if errorlevel 1 if exist ".cache\models" set MODELS=--models "%CD%\.cache\models"
start "Modelado" "%EXE%" %MODELS% %*
popd
