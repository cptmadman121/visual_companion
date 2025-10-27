param(
    [string]$Output = "..\dist"
)

$project = "..\src\TrayVisionPrompt\TrayVisionPrompt.csproj"
$configuration = "Release"
$runtimes = @("win-x64")

foreach ($runtime in $runtimes) {
    Write-Host "Publishing $runtime build" -ForegroundColor Cyan
    dotnet publish $project -c $configuration -r $runtime --self-contained true -p:PublishSingleFile=true -o "$Output\$runtime"
}

Write-Host "Copying README and installer notes" -ForegroundColor Cyan
Copy-Item "..\README.md" "$Output\" -Force
Copy-Item "..\Installer.md" "$Output\" -Force
