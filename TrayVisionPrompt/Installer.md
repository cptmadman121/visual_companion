# deskLLM Installer Notes

## Prerequisites
- Windows 10 or Windows 11 (x64)
- .NET Desktop Runtime 8.0.x
- Access to a local LLM server compatible with the OpenAI Chat Completions API (Ollama, vLLM, llama.cpp server)

## Installation Steps
1. Download the packaged archive from the `dist` folder (generated via `tools/package.ps1`).
2. Extract the archive to a writable folder, e.g. `C:\Program Files\deskLLM`.
3. Create a shortcut to `deskLLM.exe` and place it in `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup` to auto-start with Windows.
4. Launch the application once to generate the default configuration under `%APPDATA%\deskLLM\config.json`.
5. Adjust `config.json` to point to your local backend endpoint, model name, and desired hotkey.
6. Optional: Grant the application permissions in your security suite to capture the screen and communicate with your Ollama endpoint (e.g., `http://127.0.0.1` or `http://192.168.201.166`).

### Standard Ollama Configuration
If your Ollama server runs at `http://192.168.201.166:11434/`, use the following `config.json` example (note the required `/v1/chat/completions` path):

```json
{
  "hotkey": "Win+Shift+Q",
  "backend": "ollama",
  "endpoint": "http://192.168.201.166:11434/v1/chat/completions",
  "model": "llava:latest",
  "requestTimeoutMs": 45000,
  "maxTokens": 1024,
  "temperature": 0.2,
  "useVision": true,
  "useOcrFallback": true,
  "proxy": null,
  "telemetry": false,
  "logLevel": "Info"
}
```

## Updating
- Replace the installation folder contents with the new release build.
- Preserve the `%APPDATA%\deskLLM` folder to keep your configuration and logs.

## Uninstallation
1. Exit the Tray icon via `Beenden`.
2. Delete the installation directory.
3. Remove `%APPDATA%\deskLLM` if you no longer need logs or configuration.
4. Delete any startup shortcuts.

