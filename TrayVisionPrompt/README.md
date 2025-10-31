# deskLLM

deskLLM is a lightweight Windows tray companion that lets you capture a screen region with annotations or operate on the current text selection, send it to a local LLM backend (OpenAI-compatible), and view or apply the response.

It targets quick day-to-day workflows like proofreading and translating selected text in editors such as Notepad++, and visual questions on screenshots with LLMs that support vision.

**Highlights**
- System tray app with a context menu: prompts, Settings, Test Backend, Copy Last Response, Open Logs, Exit.
- Global hotkeys for each prompt; per-prompt activation: Capture Screen, Foreground Selection, or Text Dialog.
- Annotation overlay for screen capture (select area, draw/undo, reset, confirm/cancel).
- OpenAI-compatible clients: Ollama, vLLM, llama.cpp; optional OCR fallback when vision is disabled.
- Instant feedback on hotkeys: yellow dot appears immediately when a hotkey is pressed; turns green while the request is sent to the LLM.
- Tray menu prompts fall back to clipboard input/output, so Electron apps and browsers work reliably.
- Configuration and logs stored under `%APPDATA%\deskLLM`.

**What’s New**
- Custom prompt shortcuts: create your own hotkey + prompt pairs in Settings.
- Smooth Notepad++ flow: select text, press a hotkey, get a response, and replace the selection.

**Repository Layout**
- `TrayVisionPrompt.sln`
- `src/TrayVisionPrompt` (WPF core, workflows, services)
- `src/TrayVisionPrompt.Avalonia` (packaged UI host for single-file distribution)
- `tests/TrayVisionPrompt.Tests` (xUnit)
- `tools/` (build/package scripts)
- `Installer.md`, `README.md`

**Requirements**
- Windows 10/11 x64
- .NET 8 SDK (for building) or .NET Desktop Runtime 8 (for running)
- Local LLM server compatible with OpenAI Chat Completions API

**Build and Test**
- Release build + publish to `dist`: `tools\build.cmd`
- Debug build only: `tools\build.cmd -Debug`
- Run tests: `dotnet test TrayVisionPrompt.sln`

PowerShell execution policy locked down? Use the `.cmd` scripts or the raw `dotnet` commands:
- `dotnet restore TrayVisionPrompt.sln`
- `dotnet build TrayVisionPrompt.sln -c Release`
- `dotnet publish src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist`

**Install**
- See `Installer.md` for packaging, distribution, and update notes.

**Configuration**
- First run creates `%APPDATA%\deskLLM\config.json`.
- Key options: backend (`ollama`, `vllm`, `llamacpp`), `endpoint`, `model`, timeouts, `useVision`, `useOcrFallback`, and `promptShortcuts`.
- Example (abbreviated):
```
{
  "backend": "ollama",
  "endpoint": "http://127.0.0.1:11434/v1/chat/completions",
  "model": "llava:latest",
  "useVision": true,
  "useOcrFallback": true,
  "promptShortcuts": [
    { "id": "...", "name": "Capture Screen", "hotkey": "Ctrl+Shift+S", "prompt": "Describe the selected region succinctly.", "activation": "CaptureScreen" },
    { "id": "...", "name": "Proofread Selection", "hotkey": "Ctrl+Shift+P", "prompt": "Proofread and improve grammar...", "activation": "ForegroundSelection" },
    { "id": "...", "name": "Translate Selection", "hotkey": "Ctrl+Shift+T", "prompt": "If the text is not German...", "activation": "ForegroundSelection" }
  ]
}
```

**Usage in Notepad++**
- Select text in Notepad++ (or most editors/apps that support Ctrl+C/Ctrl+V).
- Press the configured hotkey, e.g. `Ctrl+Shift+P` for “Proofread Selection”.
- The app captures the selection from the foreground window and sends it with your prompt.
- The response dialog appears; the selection is replaced automatically when possible. If replacement isn’t possible, the result is copied to the clipboard.

Tips
- Works best when a selection exists; if none is available, the original clipboard text may be used.
- The service avoids resizing Notepad++ when bringing it to foreground.

**Image Capture and Annotation**
- Trigger a “Capture Screen” prompt via tray menu or hotkey (e.g. `Ctrl+Shift+S`).
- An overlay appears:
  - Select a rectangle to capture a region, or leave empty to capture the full virtual screen.
  - Switch to draw mode to annotate; undo by clearing strokes (Reset).
  - Confirm to open the instruction dialog with a live preview; choose or edit the prompt.
- If `useVision` is false but `useOcrFallback` is true, extracted OCR text is sent instead of the image.

**Add New Hotkey + Prompt Pairs**
- Via Settings (tray icon → Settings):
  - Add a new prompt, set a descriptive name, a global hotkey (e.g., `Ctrl+Alt+R`), an activation mode, and the prompt text.
  - Activation modes:
    - `CaptureScreen` – shows the overlay, captures an image, and sends it.
    - `ForegroundSelection` – captures the current selection from the active window and replaces it with the response.
    - `TextDialog` – pops up a text input dialog; response is shown and copied.
  - Save to persist and auto-register hotkeys.
- Or edit `%APPDATA%\deskLLM\config.json` directly under `promptShortcuts`.

**Backend Support**
- Uses an OpenAI-compatible Chat Completions endpoint; image content is sent as base64 when vision is enabled.
- Clients: `OllamaClient`, `VllmClient`, `LlamaCppClient`.
- Test your setup from the tray menu: “Test Backend”.

**Logging and Privacy**
- Logs: `%APPDATA%\deskLLM\TrayVisionPrompt.log` (rolling).
- No telemetry; requests go only to your configured endpoint.
- Open the logs folder from the tray menu.

**Troubleshooting**
- If a hotkey fails to register, you’ll see a warning; adjust the hotkey to avoid conflicts.
- Some apps restrict clipboard access; in that case, the app falls back to using the clipboard directly.
- Very long selections are chunked automatically so they stay within Gemma 3 27B’s context window; each segment is processed in sequence and combined for the final output.

**Rocket.Chat Desktop (Chromium/Electron)**
  - Foreground selection and replacement are optimized for Chromium/Electron apps (including Rocket.Chat) by trying alternative accelerators (Ctrl+Insert for copy, Shift+Insert for paste) and allowing slightly longer timing.
  - New clipboard-first flow: if Rocket.Chat is focused when you press a hotkey, deskLLM reuses the current clipboard text, sends it to the LLM, and drops the answer back into the clipboard; just press `Ctrl+V` to send, no pop-up.
  - If global hotkeys don't trigger while Rocket.Chat is focused:
    - Change your hotkey combinations to avoid conflicts (e.g., avoid ones Rocket.Chat uses).
    - Ensure deskLLM and Rocket.Chat run at the same privilege level (both non-admin). Mixed elevation can interfere with system-wide hotkeys.
    - If problems persist, use the tray menu to trigger prompts as a workaround.

**License**
- Internal/for local use. No telemetry. See repository policies if present.





