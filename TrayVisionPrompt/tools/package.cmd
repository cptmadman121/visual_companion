@echo off
setlocal enableextensions

rem Package Avalonia single-file into dist and copy docs

set OUT=..\dist
if not exist "%OUT%" mkdir "%OUT%"

echo Publishing Avalonia win-x64 single-file to %OUT%
dotnet publish ..\src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%OUT%" || exit /b %errorlevel%

echo Copying README and installer notes
copy /Y "..\README.md" "%OUT%\" >NUL
copy /Y "..\Installer.md" "%OUT%\" >NUL

endlocal
exit /b 0

