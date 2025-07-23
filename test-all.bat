@echo off
cd /d "%~dp0"
echo Testing InstallApplications executable:
echo.
echo ========== Version Test ==========
.\publish\x64\installapplications.exe --version
echo.
echo ========== Help Test ==========  
.\publish\x64\installapplications.exe --help
echo.
echo ========== Usage Test (no args) ==========
.\publish\x64\installapplications.exe
echo.
echo Tests completed!
pause
