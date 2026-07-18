@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "MSG=%~1"
if "%MSG%"=="" (
    echo Usage: build.bat "commit message"
    echo Example: build.bat "fix login window layout"
    exit /b 1
)

set "ARTIFACT=TaiJi-singlefile-x64"
set "WORKFLOW=ci.yml"

echo === git add / commit / push ===
git add .
git commit -m "%MSG%"
if errorlevel 1 (
    echo ERROR: git commit failed.
    exit /b 1
)

for /f %%s in ('git rev-parse HEAD 2^>nul') do set "COMMIT=%%s"
if not defined COMMIT (
    echo ERROR: cannot resolve HEAD commit.
    exit /b 1
)

git push -u origin HEAD
if errorlevel 1 (
    echo ERROR: git push failed.
    exit /b 1
)

echo.
echo Waiting for CI run (workflow=%WORKFLOW%, commit=%COMMIT%) ...
set "RUN_ID="
for /l %%n in (1,1,30) do (
    for /f "delims=" %%i in ('gh run list --workflow %WORKFLOW% --commit %COMMIT% --limit 1 --json databaseId --jq ".[0].databaseId" 2^>nul') do set "RUN_ID=%%i"
    if defined RUN_ID goto :found_run
    timeout /t 2 /nobreak >nul
)
echo ERROR: CI run not found for commit %COMMIT%.
exit /b 1

:found_run
echo Watching run %RUN_ID% ...
gh run watch %RUN_ID% --exit-status
if errorlevel 1 (
    echo.
    echo CI failed. Build log:
    gh run view %RUN_ID% --log-failed
    exit /b 1
)

if exist ".artifacts" rmdir /s /q ".artifacts"
mkdir ".artifacts"

echo.
echo Downloading artifact '%ARTIFACT%' from run %RUN_ID% ...
gh run download %RUN_ID% --name %ARTIFACT% --dir .artifacts
if errorlevel 1 exit /b 1

if not exist "publish" mkdir publish
for /r ".artifacts" %%f in (*) do (
    copy /y "%%f" "publish\" >nul
    echo   -^> publish\%%~nxf
)

rmdir /s /q ".artifacts"
echo.
echo Done. Output: %cd%\publish\
endlocal
