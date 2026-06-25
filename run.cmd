@echo off
REM Launch the JMD Explorer WPF app.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Install .NET 8 from https://aka.ms/dotnet/download
    exit /b 1
)

echo === Launching JMD Explorer ===
dotnet run --project src/JmdExplorer.App -c Release
endlocal
