param(
    [switch]$Release
)

$configuration = if ($Release) { "Release" } else { "Debug" }

Write-Host "Building TrayVisionPrompt ($configuration)" -ForegroundColor Cyan

dotnet restore ..\TrayVisionPrompt.sln
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet build ..\TrayVisionPrompt.sln -c $configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($Release) {
    # Standard release: publish Avalonia single-file directly into dist
    $outDir = "..\dist"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    Write-Host "Publishing Avalonia single-file to $outDir" -ForegroundColor Cyan
    dotnet publish ..\src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $outDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
