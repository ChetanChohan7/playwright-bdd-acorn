@echo off
setlocal

if "%~1"=="" (
    echo Usage: %~nx0 ^<target-directory^>
    exit /b 1
)

for %%I in ("%~dp0.") do set "SOURCE=%%~fI"
set "TARGET=%~f1"

if /I "%SOURCE%"=="%TARGET%" (
    echo The target directory must be different from the source directory.
    exit /b 1
)

if exist "%TARGET%\" (
    echo The target directory must not already exist: %TARGET%
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo The .NET SDK was not found on PATH.
    exit /b 1
)

echo Copying solution to %TARGET%
robocopy "%SOURCE%" "%TARGET%" /E /COPY:DAT /DCOPY:DAT /R:2 /W:1 /XD .git .vs bin obj TestResults
if errorlevel 8 (
    echo Solution copy failed.
    exit /b %errorlevel%
)

echo Restoring packages...
dotnet restore "%TARGET%\PricingValidationFramework.slnx"
if errorlevel 1 exit /b %errorlevel%

echo Building solution...
dotnet build "%TARGET%\PricingValidationFramework.slnx" --no-restore
if errorlevel 1 exit /b %errorlevel%

echo Solution recreated successfully at %TARGET%
exit /b 0