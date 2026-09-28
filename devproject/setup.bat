@echo off
setlocal enabledelayedexpansion

rem Links this repo (the addon itself) into devproject/addons so edits are picked up live. Each subfolder is junctioned individually rather than junctioning the repo root, since MSBuild won't glob through a link that points at one of its own ancestors. Re-run after pulling or adding new top-level folders/files, as git replaces hardlinked files on checkout.

for %%I in ("%~dp0..") do set REPO_DIR=%%~fI
set ADDON_DIR=%~dp0addons\vaudio-godot-mono-openal-3d

rem If the addon folder is itself a link, only remove the link - clearing its contents would delete the real repo files
for %%A in ("%ADDON_DIR%") do set ADDON_ATTR=%%~aA
if exist "%ADDON_DIR%" if "!ADDON_ATTR:~8,1!"=="l" rmdir "%ADDON_DIR%"

if exist "%ADDON_DIR%" (
    for /d %%D in ("%ADDON_DIR%\*") do rmdir "%%D"
    del /q "%ADDON_DIR%\*" 2>nul
    rmdir "%ADDON_DIR%"
    if exist "%ADDON_DIR%" (
        echo Failed to clear %ADDON_DIR%
        exit /b 1
    )
)

mkdir "%ADDON_DIR%"

for /d %%D in ("%REPO_DIR%\*") do (
    set NAME=%%~nxD
    if /i not "!NAME!"=="devproject" if not "!NAME:~0,1!"=="." (
        mklink /J "%ADDON_DIR%\!NAME!" "%%~fD" >nul || exit /b 1
    )
)

for %%F in ("%REPO_DIR%\plugin*") do (
    mklink /H "%ADDON_DIR%\%%~nxF" "%%~fF" >nul || exit /b 1
)

echo Linked %REPO_DIR% into %ADDON_DIR%

endlocal
