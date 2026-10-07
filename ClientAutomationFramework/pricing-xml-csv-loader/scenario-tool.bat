@echo off
setlocal

rem Resolve every relative path (src\..., default scenarios.csv, etc.) against this
rem script's own folder, regardless of where it's run from.
cd /d "%~dp0"

set "cmd=%~1"

if "%cmd%"=="build-csv" goto run
if "%cmd%"=="update-csv" goto run
if "%cmd%"=="add-elements" goto run

echo Usage:
echo   scenario-tool.bat build-csv [--input-dir ^<dir^>] [--csv ^<path^>]
echo   scenario-tool.bat update-csv --edits ^<path^> [--csv ^<path^>]
echo   scenario-tool.bat add-elements --elements ^<path^> [--csv ^<path^>]
echo.
echo Examples:
echo   scenario-tool.bat build-csv
echo   scenario-tool.bat update-csv --edits edits.example.json
echo   scenario-tool.bat add-elements --elements elements.example.json
echo.
echo This is a terminal backup for the browser UI's steps 1 (bulk load) and 2 (bulk
echo add/edit) only - use ui\index.html for the same thing with a browser UI.
exit /b 1

:run
dotnet run --project src\PricingXml.ScenarioTool -- %*
exit /b %errorlevel%
