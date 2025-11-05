# visual_companion

This repository now includes a new Avalonia UI project with a modern, responsive Fluent interface.

- Project: `TrayVisionPrompt/src/TrayVisionPrompt.Avalonia`
- Theme: Fluent (dark) with custom design tokens and card aesthetics
- Features: Polished layout, rounded controls, subtle transitions, and a conversation view

Default build: Avalonia UI

- Solution maps only `TrayVisionPrompt.Avalonia` (and tests) for Build by default.
- CLI: `./run-avalonia.ps1` or `dotnet run --project TrayVisionPrompt/src/TrayVisionPrompt.Avalonia`.
- Visual Studio: open `TrayVisionPrompt/TrayVisionPrompt.sln` and start `TrayVisionPrompt.Avalonia`.

Note: The original WPF tray app remains unchanged. The Avalonia UI is a new, separate desktop front‑end that can be wired to your existing services/ViewModels as desired.
