# Voice Assistant — Phase 1 Documentation

> **Context restore file** — paste this at the start of any new chat to resume work with full context.

---

## Project Overview

A fully local, offline personal voice assistant running on a home PC, accessible from any device on home WiFi via a browser. No cloud services. No data leaves the machine.

**Developer:** .NET developer, 9 years experience, comfortable with C#, ASP.NET, MSSQL, React, GitHub.

**Hardware:** AMD Ryzen 5 5600GT (12 threads) · Radeon iGPU · 16GB RAM · Ubuntu 24.04 LTS · No dedicated GPU.

---

## Full Roadmap

| Phase | Description | Status |
|---|---|---|
| 1 | Local voice assistant — STT, LLM, TTS, WebSocket, browser UI | ✅ Complete |
| 2 | Smart home inventory — bill scanning, item tracking, shopping list | 🔜 Next |
| 3 | Financial assistant — expense tracking, budget analysis, voice reports | ⏳ Future |
| 4 | Internet price comparison — web search, price scraping, deal finder | ⏳ Future |

---

## Architecture

### High Level

```
Browser (phone/laptop on home WiFi)
        ↕ HTTPS WebSocket (SignalR)
ASP.NET Core 9 Web API  (port 5000 HTTP / 5443 HTTPS)
        ↕ HttpClient calls (localhost)
┌─────────────────┬──────────────────┬─────────────────┐
│  STT Service    │   LLM Service    │   TTS Service   │
│  Python/Flask   │   Ollama         │  Python/Flask   │
│  port 5001      │   port 11434     │  port 5002      │
│  faster-whisper │   gemma3:4b      │  Kokoro-82M     │
└─────────────────┴──────────────────┴─────────────────┘
```

### Pipeline Flow

```
User speaks → MediaRecorder (webm) → SignalR → VoiceHub
→ STTService → Whisper (webm→wav→text)
→ LLMService → Ollama (text→answer)
→ StripMarkdown (clean text for TTS)
→ TTSService → Kokoro (text→wav bytes)
→ base64 → SignalR → Browser → Audio plays
```

### Design Decisions

- **Python microservices for STT/TTS** — Whisper and Kokoro have mature Python SDKs. Calling them via HTTP from .NET is cleaner than native bindings on Linux.
- **.NET as orchestrator** — All business logic, interfaces, and future tool calls live in C#. Python services are pure HTTP dependencies.
- **SignalR over raw WebSocket** — Built-in reconnection, typed hub methods, DI support, no boilerplate.
- **gemma3:4b over Qwen3/Llama** — Qwen3 with deep reasoning took 50+ seconds on CPU. gemma3:4b gives 4–8 tok/sec, fast enough for voice.
- **Kokoro-82M for TTS** — Lightest high-quality neural TTS, fast on CPU, natural voice output.
- **Self-signed cert for HTTPS** — Mobile browsers require HTTPS for microphone access. Self-signed cert on port 5443, accept once on each device.

---

## Project Structure

```
~/VoiceAssistant/
├── VoiceAssistant.sln
├── start.sh                          ← starts all services
├── certs/                            ← gitignored, HTTPS cert files
│   ├── assistant.pfx
│   ├── cert.pem
│   └── key.pem
├── stt-service/                      ← Python STT microservice
│   ├── app.py
│   └── venv/
├── tts-service/                      ← Python TTS microservice
│   ├── app.py
│   ├── kokoro-v1.0.onnx
│   ├── voices-v1.0.bin
│   └── venv/
├── VoiceAssistant.Core/              ← interfaces and models (no dependencies)
│   └── Interfaces/
│       ├── ISTTService.cs
│       ├── ILLMService.cs
│       ├── ITTSService.cs
│       └── ITool.cs                  ← plugin interface for future tool calls
├── VoiceAssistant.Infrastructure/    ← HTTP clients to Python/Ollama services
│   └── Services/
│       ├── OllamaService.cs
│       ├── STTService.cs
│       └── TTSService.cs
└── VoiceAssistant.API/               ← main web API
    ├── Program.cs
    ├── appsettings.json
    ├── Controllers/
    │   └── TestController.cs
    ├── Hubs/
    │   └── VoiceHub.cs               ← SignalR hub, pipeline orchestrator
    └── wwwroot/
        └── index.html                ← browser UI served by API
```

---

## Tech Stack

| Layer | Technology | Notes |
|---|---|---|
| Backend | ASP.NET Core 9 Web API | Kestrel, DI, SignalR |
| STT | faster-whisper + Flask | whisper-small, CPU int8, port 5001 |
| LLM | Ollama — gemma3:4b | ~4–8 tok/sec on CPU, port 11434 |
| TTS | Kokoro-82M + Flask | kokoro-onnx, port 5002 |
| Frontend | Plain HTML + JS | MediaRecorder, SignalR client |
| Protocol | SignalR WebSocket | audio as base64 strings |
| HTTPS | Self-signed cert (openssl) | port 5443, pfx format for Kestrel |

---

## Key Files Explained

### `VoiceAssistant.Core/Interfaces/ITool.cs`
The plugin interface every future tool implements. LLM calls tools by name. Adding Phase 2/3/4 features = adding new ITool implementations.

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}
```

### `VoiceAssistant.API/Hubs/VoiceHub.cs`
SignalR hub. Receives audio as base64 string, runs STT→LLM→TTS pipeline, sends back transcription, answer text, and audio as base64. Strips markdown before TTS so asterisks and bullets are not read aloud.

### `VoiceAssistant.Infrastructure/Services/OllamaService.cs`
Calls Ollama REST API. Has a system prompt instructing the LLM to respond in plain conversational sentences without markdown. Model is configurable via `appsettings.json` — no recompile needed to switch models.

### `stt-service/app.py`
Flask service on port 5001. Accepts audio file upload (webm or wav). Converts webm to wav via ffmpeg before passing to faster-whisper. Returns JSON with transcribed text, detected language, and duration.

### `tts-service/app.py`
Flask service on port 5002. Accepts JSON with text field. Returns WAV audio bytes via Kokoro-82M ONNX model. Voice: `af_heart`, speed: 1.0.

### `wwwroot/index.html`
Single page UI. Tap-and-hold mic button records audio. On release, sends base64 audio to SignalR hub. Displays transcription and answer text. Auto-plays audio on desktop. Shows 🔊 play button on mobile (autoplay blocked by mobile browsers).

---

## Configuration

### `appsettings.json`

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434/",
    "Model": "gemma3:4b",
    "TimeoutSeconds": 60
  },
  "Kestrel": {
    "Endpoints": {
      "Http": { "Url": "http://0.0.0.0:5000" },
      "Https": {
        "Url": "https://0.0.0.0:5443",
        "Certificate": {
          "Path": "/home/viddharth/VoiceAssistant/certs/assistant.pfx",
          "Password": "voiceassistant"
        }
      }
    }
  }
}
```

### Switching LLM model
Change `"Model"` in appsettings.json and restart API. No code changes needed.
Available models already pulled: `gemma3:4b`, `phi3:mini`, `qwen3:4b`

---

## Ports Reference

| Service | Port | Protocol |
|---|---|---|
| .NET API (HTTP) | 5000 | HTTP |
| .NET API (HTTPS) | 5443 | HTTPS |
| STT (Whisper) | 5001 | HTTP (localhost only) |
| TTS (Kokoro) | 5002 | HTTP (localhost only) |
| Ollama (LLM) | 11434 | HTTP (localhost only) |

---

## Starting All Services

```bash
~/VoiceAssistant/start.sh
```

Or manually in 3 terminals:

```bash
# Terminal 1 — STT
cd ~/VoiceAssistant/stt-service && source venv/bin/activate && python3 app.py

# Terminal 2 — TTS
cd ~/VoiceAssistant/tts-service && source venv/bin/activate && python3 app.py

# Terminal 3 — .NET API
cd ~/VoiceAssistant && dotnet run --project VoiceAssistant.API
```

---

## Accessing the Assistant

| Device | URL |
|---|---|
| PC browser | http://localhost:5000 |
| Phone/laptop on WiFi | https://192.168.1.106:5443 |

On first visit from phone: tap Advanced → Proceed anyway (self-signed cert warning). Allow microphone when prompted. Works on Chrome Android and Safari iOS.

---

## Hardware Performance

| Component | Time |
|---|---|
| STT (Whisper small) | ~2–4 sec per 10 sec audio |
| LLM (gemma3:4b) | ~4–8 sec typical answer |
| TTS (Kokoro-82M) | ~1–2 sec per sentence |
| **Total round trip** | **~10–15 seconds** |

RAM usage: ~6.5GB total (OS + all services). 9.5GB headroom on 16GB.

---

## Issues Hit and Fixed

| Issue | Fix |
|---|---|
| dotnet-sdk-9.0 not found in apt | Used Microsoft install script via wget |
| Port 5000 already in use | `sudo fuser -k 5000/tcp` |
| Missing IConfiguration in Infrastructure | Added `Microsoft.Extensions.Configuration.Abstractions` package |
| SignalR message too large | Set `MaximumReceiveMessageSize = 10MB` |
| SignalR byte[] type mismatch | Changed hub to accept `string audioBase64`, convert with `Convert.FromBase64String` |
| Mobile browser blocks autoplay | Added 🔊 play button, autoplay attempted first, fallback to button |
| Whisper reads silence / Welsh | Fixed mic device: `arecord -D hw:1,0`, set capture volume to 100% |
| LLM returns markdown (asterisks read aloud) | Added `StripMarkdown()` in VoiceHub + system prompt to avoid markdown |
| webm audio from browser not accepted by Whisper | Added ffmpeg conversion in stt-service/app.py |
| 10K files staged in git | Created .gitignore, used `git rm -r --cached -f .` |

---

## Bucket List (deferred items)

- [ ] Set static local IP via router DHCP reservation (MAC: check with `ip link show`)
- [ ] Switch model via voice command without restarting API
- [ ] Convert start.sh to systemd services (auto-start on boot)
- [ ] Add conversation history / memory to LLM context

---

## Phase 2 — What's Next

**Smart Home Inventory**

- Bill image → OCR → structured item data → SQLite DB
- Voice commands to update stock ("I finished the rice")
- Shopping list generation based on usage patterns
- Tools needed: `BillScannerTool`, `InventoryQueryTool`, `ShoppingListTool`
- New dependency: Tesseract OCR or LLaVA vision model via Ollama
- New project: `VoiceAssistant.Data` (SQLite + EF Core)

---

## GitHub

Repository: `https://github.com/saibhargav22/VoiceAssistant`
Branch: `main`
Last commit: `feat: complete phase 1 - full voice pipeline working`

