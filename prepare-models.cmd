@echo off
setlocal

rem Prepares Modelado's models on this PC, start to finish:
rem   1. a Python virtual environment in .venv with scripts\requirements.txt (CPU torch);
rem   2. the upstream weights, into .cache\weights (about 2 GB, each file checked by SHA-256);
rem   3. the ONNX conversion, into .cache\models, each model checked against PyTorch;
rem   4. with --publish, the upload to Hugging Face (alegauss/modelado-models unless --repo).
rem
rem   prepare-models.cmd
rem   prepare-models.cmd --publish D:\Keys\hf.txt
rem   prepare-models.cmd --publish D:\Keys\hf.txt --repo me/my-models
rem
rem The token file holds a Hugging Face write token (huggingface.co/settings/tokens); it is
rem never printed. Steps 2 and 3 skip what is already done, so a second run is quick.

pushd "%~dp0"
set "PY=.venv\Scripts\python.exe"

if not exist "%PY%" (
    where python >nul 2>&1 || (echo Python 3.11 or later is needed: https://www.python.org/downloads/ & goto :fail)
    echo Creating .venv
    python -m venv .venv || goto :fail
    "%PY%" -m pip install --upgrade pip || goto :fail
    "%PY%" -m pip install torch==2.14.1 --index-url https://download.pytorch.org/whl/cpu || goto :fail
    "%PY%" -m pip install -r scripts\requirements.txt || goto :fail
)

echo.
echo === 1/3 Downloading the upstream weights ===
"%PY%" scripts\download_models.py || goto :fail

echo.
echo === 2/3 Converting to ONNX and checking against PyTorch ===
"%PY%" scripts\export_models.py || goto :fail

if /i not "%~1"=="--publish" (
    echo.
    echo Models ready in .cache\models. run.cmd uses them; to publish them:
    echo   prepare-models.cmd --publish ^<token file^>
    popd
    exit /b 0
)

echo.
echo === 3/3 Publishing to Hugging Face ===
if "%~2"=="" (echo --publish needs the token file & goto :fail)
if /i "%~3"=="--repo" (
    "%PY%" scripts\publish_models.py --token-file "%~2" --repo "%~4" || goto :fail
) else (
    "%PY%" scripts\publish_models.py --token-file "%~2" || goto :fail
)
echo Rebuild the app: src\Modelado.Inference\models-manifest.json now pins the published files.
popd
exit /b 0

:fail
echo.
echo FAILED - see the message above.
popd
exit /b 1
