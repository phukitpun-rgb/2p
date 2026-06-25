@echo off
REM Build the whole solution (Release) and run the unit tests.
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK not found. Install .NET 8 from https://aka.ms/dotnet/download
    exit /b 1
)

echo === Restoring and building JmdExplorer.sln (Release) ===
dotnet build JmdExplorer.sln -c Release
if errorlevel 1 exit /b 1

echo.
echo === Running unit tests ===
dotnet test JmdExplorer.sln -c Release --no-build
if errorlevel 1 exit /b 1

echo.
echo Build + tests complete.
endlocal
