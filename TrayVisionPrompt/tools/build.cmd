@echo off
setlocal enableextensions enabledelayedexpansion

rem Simple build script that avoids PowerShell execution policy requirements.
rem - Restores and builds the solution
rem - On Release, publishes Avalonia single-file into dist

set CONFIG=Release
if /I "%1"=="-Debug" set CONFIG=Debug

echo Building deskLLM (%CONFIG%)
dotnet restore ..\TrayVisionPrompt.sln || exit /b %errorlevel%
dotnet build ..\TrayVisionPrompt.sln -c %CONFIG% || exit /b %errorlevel%

if /I "%CONFIG%"=="Release" (
  set "OUT=..\dist"
  if exist "!OUT!" (
    echo Cleaning !OUT! ...
    rmdir /s /q "!OUT!"
  )
  mkdir "!OUT!" >NUL 2>&1
  echo Publishing Avalonia single-file to !OUT!
  dotnet publish ..\src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=false -p:DebugType=none -o "!OUT!" || exit /b %errorlevel%
  if exist "!OUT!\TrayVisionPrompt.Avalonia.exe" (
    rem Rename to product name in dist
    move /Y "!OUT!\TrayVisionPrompt.Avalonia.exe" "!OUT!\deskLLM.exe" >NUL
  )
)

endlocal
exit /b 0

