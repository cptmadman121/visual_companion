# TrayVisionPrompt

TrayVisionPrompt ist ein leichtgewichtiges Windows 10/11 Tray-Companion, das markierte Bildschirmbereiche per globalem Hotkey erfasst, Annotationen erlaubt und den Screenshot zusammen mit einem Prompt an einen lokalen LLM-Server sendet. Die Antwort wird als Pop-Up angezeigt und kann in die Zwischenablage kopiert werden.

## Features
- 🖥️ System-Tray-App mit Kontextmenü (Einstellungen, Backend testen, letzte Antwort kopieren, Logs öffnen, Beenden)
- ⌨️ Konfigurierbarer globaler Hotkey (Standard: `Win+Shift+Q`)
- ✏️ Bildschirmüberlagerung zur Rechteckauswahl mit Ink-Annotationen (Undo/Reset)
- 📝 Instruktionsdialog mit Prompt-Eingabe und Presets
- 🤖 Adapterbasiertes LLM-Interface (Ollama, vLLM, llama.cpp)
- 🧾 Optionale OCR-Fallback-Verarbeitung über Windows.Media.Ocr
- 🔒 Lokale Verarbeitung, Logging nach `%APPDATA%\TrayVisionPrompt`

## Projektstruktur
```
TrayVisionPrompt/
├── TrayVisionPrompt.sln
├── src/
│   └── TrayVisionPrompt/        # WPF-Anwendung (.NET 8)
├── tests/
│   └── TrayVisionPrompt.Tests/  # xUnit-Tests
├── tools/                       # Build- und Paket-Skripte
├── Installer.md                 # Installationshinweise
└── README.md
```

## Entwicklung
### Voraussetzungen
- Visual Studio 2022 (17.8+) mit .NET Desktop Development
- .NET 8 SDK
- Windows 10/11 x64

### Build & Test
```powershell
# Aus dem Projektstamm
pwsh tools/build.ps1
pwsh tools/build.ps1 -Release
pwsh -c "dotnet test TrayVisionPrompt.sln"
```

### Visual Studio Code
Die Datei `.vscode/tasks.json` definiert Build- und Test-Tasks, `.vscode/launch.json` startet die App nach einem Debug-Build.

## Konfiguration
Beim ersten Start wird `%APPDATA%\TrayVisionPrompt\config.json` erzeugt. Beispiel:
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

## Backend-Anbindung
TrayVisionPrompt kommuniziert über eine OpenAI-kompatible `/chat/completions`-Route. Bilder werden als `data:image/png;base64` in `content.parts` übergeben. Die Implementierungen sind austauschbar:
- **OllamaClient** – Standard für Ollama Vision-Modelle (`llava`, `llava:latest`)
- **VllmClient** – Für vLLM-Instanzen mit Vision-Support
- **LlamaCppClient** – Für `llama.cpp server.exe`

Falls Vision deaktiviert ist, aber `useOcrFallback = true`, wird der OCR-Text als Ergänzung gesendet.

## Logging & Datenschutz
- Serilog schreibt nach `%APPDATA%\TrayVisionPrompt\TrayVisionPrompt.log` (Rolling, 7 Tage)
- Keine Telemetrie oder externe Uploads; alle Requests laufen lokal
- Logs können über das Tray-Menü geöffnet werden

## Tests
```powershell
pwsh -c "dotnet test TrayVisionPrompt.sln"
```

## Installer
Siehe [Installer.md](Installer.md) für Paketierung, Installation und Update-Hinweise.

## Roadmap / Erweiterungen
- Zusätzliche Backend-Adapter (REST, gRPC)
- Fortgeschrittene Annotationstools (Text, Pfeile, Formen)
- Automatische Erkennung mehrerer Monitore
- Erweiterte Markdown-Anzeige der LLM-Antwort
