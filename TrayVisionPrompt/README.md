# deskLLM

deskLLM is a lightweight Windows companion that combines a modern Avalonia desktop UI with a background tray workflow. Capture annotated screen regions or operate on text selections, send them to a local OpenAI-compatible LLM, and review, apply, or automate the response.

It targets fast everyday tasks—proofreading, translating, anonymising, answering visual questions—across editors like Notepad++, browsers, and Electron apps.

**Highlights**
- Fluent-style Avalonia front-end with conversation timeline, quick actions, and a compact instruction dialog for capture workflows.
- Animated tray icon with global hotkeys for every prompt; activation modes cover Capture Screen, Foreground Selection, and Text Dialog and can auto-apply results without showing a dialog.
- Prompt shortcuts support custom names, hotkeys, prompt text, optional prefill, and per-prompt response dialogs for inline automation flows.
- OpenAI-compatible backends (Ollama, vLLM, llama.cpp) with endpoint normalization, proxy support, optional vision/OCR fallback, automatic language detection, and long-selection chunking.
- Local HTTP API (`http://127.0.0.1:27124/v1/process`) for integrations, including bundled Chrome/Firefox context menu extensions.
- Configurable experience stored under `%APPDATA%\deskLLM`: choose icon assets, enable clipboard logging, review transcripts, and open logs straight from the tray.

**What’s New**
- Avalonia UI replaces the legacy WPF host for day-to-day use, delivering a Fluent dark theme, card layout, and conversation history.
- Local API server powers browser extensions and other clients; proofread/translate is one POST away.
- Chrome and Firefox extensions ship in `browser-extensions/` and hook into the local API for right-click proofreading/translation.
- Prompt shortcuts gained `showResponseDialog`, `prefill`, and a new default “Anonymize Selection” preset; Settings now edits every aspect including icon choice and clipboard logging.
- Improved integrations: Rocket.Chat clipboard-first flow, smarter text chunking, sanitised translation responses, and adaptive timeouts based on request size.

**Repository Layout**
- `TrayVisionPrompt.sln`
- `src/TrayVisionPrompt` (core services shared with the tray workflow)
- `src/TrayVisionPrompt.Avalonia` (Avalonia desktop front-end and Windows tray host)
- `tests/TrayVisionPrompt.Tests` (xUnit)
- `browser-extensions/` (Chrome and Firefox MV3 extensions)
- `tools/` (build/package scripts)
- `Installer.md`, `README.md`

**Requirements**
- Windows 10/11 x64
- .NET 8 SDK to build (Desktop Runtime 8.0.x to run the packaged app)
- Local LLM server compatible with the OpenAI Chat Completions API

**Build and Test**
- Release build + self-contained publish to `dist`: `tools\build.cmd`
- Debug build only (skip publish): `tools\build.cmd -Debug`
- Run tests: `dotnet test TrayVisionPrompt.sln`
- Quick run of the Avalonia host: `.\run-avalonia.ps1` or `dotnet run --project src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj`

PowerShell execution policy locked down? Use the `.cmd` scripts or raw `dotnet` commands:
- `dotnet restore TrayVisionPrompt.sln`
- `dotnet build TrayVisionPrompt.sln -c Release`
- `dotnet publish src\TrayVisionPrompt.Avalonia\TrayVisionPrompt.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist`

**Install**
- See `Installer.md` for packaging, distribution, and update notes. The Release build drops a single-file `deskLLM.exe` into `dist`.

**Configuration**
- First run creates `%APPDATA%\deskLLM\config.json`.
- Core options: backend (`ollama`, `vllm`, `llamacpp`), `endpoint`, `model`, timeouts, `useVision`, `useOcrFallback`, `language`, `proxy`, `iconAsset`, `enableClipboardLogging`, `keepTranscripts`, and `promptShortcuts`.
- `promptShortcuts` entries now support `prefill` (for TextDialog prompts) and `showResponseDialog` (skip the dialog when replacing text inline).
- Example (abbreviated):
```json
{
  "backend": "ollama",
  "endpoint": "http://127.0.0.1:11434/v1/chat/completions",
  "model": "llava:latest",
  "useVision": true,
  "useOcrFallback": true,
  "language": "English",
  "iconAsset": "ollama-companion",
  "enableClipboardLogging": false,
  "keepTranscripts": true,
  "promptShortcuts": [
    {
      "id": "...",
      "name": "Capture Screen",
      "hotkey": "Ctrl+Shift+S",
      "prompt": "Describe the selected region succinctly.",
      "activation": "CaptureScreen",
      "showResponseDialog": true
    },
    {
      "id": "...",
      "name": "Proofread Selection",
      "hotkey": "Ctrl+Shift+P",
      "prompt": "Proofread and improve grammar...",
      "activation": "ForegroundSelection",
      "showResponseDialog": false
    },
    {
      "id": "...",
      "name": "Anonymize Selection",
      "hotkey": "Ctrl+Shift+A",
      "prompt": "Anonymize the provided text...",
      "activation": "ForegroundSelection",
      "showResponseDialog": false
    }
  ]
}
```

**Avalonia Desktop + Tray**
- Launch the Avalonia host to access the conversation view, quick actions, and Settings window.
- The tray icon animates (yellow pending, green pulse while busy) and exposes Open, per-prompt entries, Settings, Test Backend, Open Logs, and Exit.
- Settings lets you adjust prompts, select icon assets, toggle OCR fallback, choose clipboard logging, change languages, and configure proxies/endpoints.

**Usage in Notepad++ and Editors**
- Select text, press a text-selection hotkey (Proofread/Translate/Anonymize). The app captures the selection, applies your prompt, and replaces the selection automatically if `showResponseDialog` is `false`. Otherwise it opens the response dialog first.
- If replacement fails in a specific editor, the fallback copies the answer to the clipboard without losing the prior clipboard contents.
- Long selections are chunked to respect the configured `maxTokens`. Responses are merged back before replacement.
- For clipboard-driven flows (Rocket.Chat, Chromium apps), the app reuses the existing clipboard text and drops the reply back onto the clipboard, so `Ctrl+V` posts the answer instantly.

**Image Capture and Annotation**
- Trigger a Capture Screen prompt via tray menu or a hotkey (e.g. `Ctrl+Shift+S`).
- The overlay supports rectangle selection, drawing/undo, reset, and cancel. Confirming opens the instruction dialog with a live preview and optional prompt tweaks.
- When `useVision` is disabled but `useOcrFallback` is on, captured regions run through Windows OCR and the recognised text is sent instead of the image.

**Add New Hotkey + Prompt Pairs**
- Tray icon → Settings → Prompts:
  - Add a prompt, choose name, global hotkey, activation mode, prompt text, optional prefill, and whether to show the response dialog.
  - Activation modes:
    - `CaptureScreen` – capture annotated screenshots (always shows a dialog).
    - `ForegroundSelection` – capture the active selection and optionally auto-apply the response.
    - `TextDialog` – open a text input dialog with optional prefill.
  - Save updates the config and re-registers hotkeys on the fly.
- You can also edit `%APPDATA%\deskLLM\config.json` directly under `promptShortcuts`.

**Browser Extensions & Local API**
- The Avalonia app hosts `http://127.0.0.1:27124/v1/process`. POST `{ "action": "proofread" | "translate", "text": "..." }` or supply `extraPrompt` for custom behaviour.
- Chrome/Edge: load `browser-extensions/chrome` unpacked; Firefox: load `browser-extensions/firefox` as a temporary add-on. Each adds “Proofread with deskLLM” and “Translate with deskLLM” context menu items.
- If the API fails to start, add a URL ACL from an elevated terminal: `netsh http add urlacl url=http://127.0.0.1:27124/ user=Everyone`.
- Extensions replace the selection inline; on failure they fall back to inserting via the DOM.

**Backend Support**
- Works with OpenAI-compatible Chat Completions endpoints; images are sent as base64 when vision is enabled.
- Clients cover Ollama, vLLM, and llama.cpp; use “Test Backend” from the tray to validate connectivity.
- System prompts adapt to the detected language of your selection or instruction, so responses arrive in the expected language automatically.

**Logging and Privacy**
- Rolling application logs: `%APPDATA%\deskLLM\TrayVisionPrompt.log`.
- Optional clipboard log (Settings → Enable Clipboard Logging): `%APPDATA%\deskLLM\logs\clipboard.log`.
- Conversation transcripts (`keepTranscripts` enabled): `%APPDATA%\deskLLM\logs\transcripts\session_*.txt`.
- No telemetry; requests only hit your configured backend or the local API.

**Troubleshooting**
- Hotkey conflicts show warnings in the app and tray notifications; adjust the shortcut or run both apps at the same privilege level.
- If Rocket.Chat or another Chromium app does not react, enable `showResponseDialog` for that prompt, or trigger it from the tray menu as a fallback.
- Keep `maxTokens` aligned with your model’s context window; deskLLM reserves instruction tokens automatically but respects your ceiling.
- For API errors, check the clipboard log or TrayVisionPrompt.log and run “Test Backend” to confirm endpoint reachability.

**Rocket.Chat Desktop (Chromium/Electron)**
- Optimised for Rocket.Chat and similar Electron apps: the service tries Ctrl+C, Ctrl+Insert, and Shift+Insert sequences to copy/paste reliably.
- Clipboard-first flow: when Rocket.Chat is focused, deskLLM reuses the current clipboard, sends it to the LLM, and places the answer back on the clipboard—press `Ctrl+V` to send without a dialog.
- If global hotkeys still do not trigger, adjust the combinations, ensure both apps run non-admin, or use the tray menu as a workaround.

**License**
- Internal/for local use. No telemetry. See repository policies if present.

