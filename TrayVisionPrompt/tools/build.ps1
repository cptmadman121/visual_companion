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
    Write-Host "Publishing self-contained build" -ForegroundColor Cyan
    dotnet publish ..\src\TrayVisionPrompt\TrayVisionPrompt.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
}
