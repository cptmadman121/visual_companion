param(
    [string]$Output = "..\dist"
)

$project = "..\src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj"
$configuration = "Release"
$runtime = "win-x64"

New-Item -ItemType Directory -Force -Path $Output | Out-Null

Write-Host "Publishing Avalonia $runtime single-file" -ForegroundColor Cyan
dotnet publish $project -c $configuration -r $runtime --self-contained true -p:PublishSingleFile=true -o "$Output"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Copying README and installer notes" -ForegroundColor Cyan
Copy-Item "..\README.md" "$Output\" -Force
Copy-Item "..\Installer.md" "$Output\" -Force

