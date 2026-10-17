@echo off
rem Builds the self-contained Windows packages (x64 and arm64) as .zip files.
rem Native cmd.exe script: no Git Bash / WSL needed (only dotnet and PowerShell, both already required to build this repo).
rem   tools\build_windows.bat [version]
setlocal enabledelayedexpansion

set "PKGVER=%~1"
if "%PKGVER%"=="" set "PKGVER=dev"

set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"

for %%R in (win-x64 win-arm64) do (
    call :package %%R
    if errorlevel 1 exit /b 1
)

exit /b 0

:package
set "RID=%~1"
set "NAME=TheDoomBuilder-%PKGVER%-%RID%"
set "OUT=%ROOT%\artifacts\publish-%RID%"

if exist "%OUT%" rd /s /q "%OUT%"
dotnet publish "%ROOT%\src\TheDoomBuilder.Application" -c Release -r %RID% --self-contained -o "%OUT%\%NAME%"
if errorlevel 1 exit /b 1

del /s /q "%OUT%\%NAME%\*.pdb" >nul 2>&1

rem Cosmetic rename for the end user: the assembly is "TheDoomBuilder.Application", only the apphost .exe on disk is renamed.
ren "%OUT%\%NAME%\TheDoomBuilder.Application.exe" "TheDoomBuilder.exe"
if errorlevel 1 exit /b 1

if not exist "%ROOT%\artifacts" mkdir "%ROOT%\artifacts"
set "ZIP=%ROOT%\artifacts\%NAME%.zip"
if exist "%ZIP%" del /q "%ZIP%"

powershell -NoProfile -Command "Compress-Archive -Path '%OUT%\%NAME%' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 exit /b 1

echo Package: %ZIP%
exit /b 0
