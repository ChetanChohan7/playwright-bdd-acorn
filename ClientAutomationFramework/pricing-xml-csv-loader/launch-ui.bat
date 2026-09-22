@echo off
setlocal

rem Opens the Scenario XML Loader and its CSV Viewer companion page together in
rem the default web browser. Plain double-click launcher, no admin rights needed.

cd /d "%~dp0"

if not exist "ui\index.html" (
    echo Could not find ui\index.html - run this from the project root.
    exit /b 1
)
if not exist "ui\csv-viewer.html" (
    echo Could not find ui\csv-viewer.html - run this from the project root.
    exit /b 1
)

echo Opening Scenario XML Loader...
start "" "ui\index.html"

rem A short pause avoids a cold-start race where the browser hasn't finished
rem claiming its single-instance lock yet, which can otherwise open the second
rem page in its own separate window instead of a new tab in the same one.
ping -n 2 127.0.0.1 >nul

echo Opening CSV Viewer...
start "" "ui\csv-viewer.html"

exit /b 0
