@echo off
echo ===================================================
echo Setting up the HCI Vocabulary Game Environment...
echo ===================================================

echo Creating virtual environment...
python -m venv .venv

echo Activating virtual environment...
call .venv\Scripts\activate.bat

echo Installing required packages...
pip install -r requirements.txt

echo ===================================================
echo Setup Complete! You can now use run_server.bat
echo ===================================================
pause
