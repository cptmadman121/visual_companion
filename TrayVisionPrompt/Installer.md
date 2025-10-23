# TrayVisionPrompt Installer Notes

## Prerequisites
- Windows 10 or Windows 11 (x64)
- .NET Desktop Runtime 8.0.x
- Access to a local LLM server compatible with the OpenAI Chat Completions API (Ollama, vLLM, llama.cpp server)

## Installation Steps
1. Download the packaged archive from the `dist` folder (generated via `tools/package.ps1`).
2. Extract the archive to a writable folder, e.g. `C:\Program Files\TrayVisionPrompt`.
3. Create a shortcut to `TrayVisionPrompt.exe` and place it in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` to auto-start with Windows.
4. Launch the application once to generate the default configuration under `%APPDATA%\TrayVisionPrompt\config.json`.
5. Adjust `config.json` to point to your local backend endpoint, model name, and desired hotkey.
6. Optional: Grant the application permissions in your security suite to capture the screen and communicate with `http://127.0.0.1`.

## Updating
- Replace the installation folder contents with the new release build.
- Preserve the `%APPDATA%\TrayVisionPrompt` folder to keep your configuration and logs.

## Uninstallation
1. Exit the Tray icon via `Beenden`.
2. Delete the installation directory.
3. Remove `%APPDATA%\TrayVisionPrompt` if you no longer need logs or configuration.
4. Delete any startup shortcuts.
