@echo off
echo ===================================================
echo Starting the Server...
echo ===================================================

call .venv\Scripts\activate.bat
cd "hci project\hci project\HCI bullshit"
jupyter notebook serverPython.ipynb

pause
