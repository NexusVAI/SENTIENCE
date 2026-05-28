@echo off
REM ===========================================================
REM   Sentience V5 - Anima Launcher (development mode)
REM   Runs the Python source directly. Useful while iterating.
REM ===========================================================
setlocal

cd /d "%~dp0"

where python >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python is not on PATH. Install Python 3.10+ first.
    pause
    exit /b 1
)

if not exist ".venv\Scripts\python.exe" (
    echo [INFO] Creating virtual environment...
    python -m venv .venv
    if errorlevel 1 (
        echo [ERROR] Failed to create venv.
        pause
        exit /b 1
    )
)

call ".venv\Scripts\activate.bat"

python -m pip install --quiet --upgrade pip
python -m pip install --quiet -r requirements.txt
if errorlevel 1 (
    echo [ERROR] Dependency install failed.
    pause
    exit /b 1
)

python main.py
endlocal
