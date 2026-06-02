# VoiceAssistant

## Overview

VoiceAssistant is a local, offline personal voice assistant built with .NET and local ML services. It runs on a home PC and exposes a browser-based UI over the local network. The system is designed to keep data on-device and avoid cloud dependencies.

Two assistant personas are implemented:
- **Nova** — the general assistant for conversational questions and general tasks.
- **Stocky** — the inventory assistant for grocery stock management, shopping list generation, and cupboard queries.

The app supports speech input, LLM processing, tool execution, and optional local TTS.

## Architecture

### High-level flow

```
Browser UI
    ↕ SignalR / HTTP
ASP.NET Core API
    ↕ HTTP clients
STT service  |  LLM service  |  TTS service  |  OCR service
```

### Components

- `VoiceAssistant.API/`
  - ASP.NET Core web API and SignalR hub
  - Serves browser UI from `wwwroot/`
  - Orchestrates STT, LLM, TTS, and inventory tools
  - Exposes `/voicehub` SignalR endpoint

- `VoiceAssistant.Core/`
  - Shared interfaces and domain models
  - `ISTTService`, `ILLMService`, `ITTSService`, `IOCRService`, `ITool`
  - Inventory models such as `Item`, `Inventory`, `Cupboard`, `Bill`, and `StockEvent`

- `VoiceAssistant.Infrastructure/`
  - HTTP client implementations for local services
  - `OllamaService` for LLM access
  - `STTService`, `TTSService`, `OCRService`
  - Inventory tool adapters for `update_stock`, `get_inventory`, `shopping_list`, `find_item`, `update_location`, `get_cupboard_contents`, `get_category_items`

- `VoiceAssistant.Services/`
  - Business logic for inventory and billing
  - `InventoryService` and `BillService`

- `VoiceAssistant.Data/`
  - Entity Framework Core `AppDbContext`
  - SQLite repository implementation

- `stt-service/`, `tts-service/`, `ocr-service/`
  - Python microservices for speech-to-text, text-to-speech, and OCR
  - Designed to run locally and be consumed via HTTP

- `certs/`
  - Local HTTPS certificates used by ASP.NET Core for secure browser access

- `data/`
  - SQLite database and runtime data storage
  - `assistant.db` and bill image storage

## Assistant Personas

### Nova

Nova is the default general assistant. It handles general conversational queries, answers questions, and can switch to Stocky when the user requests it.

### Stocky

Stocky is the inventory assistant. It is responsible for:
- Tracking items in the pantry and cupboards
- Updating stock quantities
- Finding item locations
- Generating shopping lists
- Showing inventory categories

Stocky uses a strict tool-oriented prompt and should not invent unsupported tool names.

## Key features

- **SignalR voice pipeline** from browser to API
- **Local Ollama LLM integration** via HTTP
- **Inventory tool execution** through JSON tool messages
- **Two persona modes**: Nova and Stocky
- **Browser UI** with text and voice input
- **Optional voice response** via local TTS
- **SQLite database** for persistent inventory and bill data

## Current configuration

The app is configured in `VoiceAssistant.API/appsettings.json`.

Important values:

- `Ollama:BaseUrl` — `http://localhost:11434/`
- `Ollama:Model` — `gemma3:4b`
- `Services:SttUrl` — `http://localhost:5001/`
- `Services:TtsUrl` — `http://localhost:5002/`
- `Services:OcrUrl` — `http://localhost:5003/`
- `Database:Path` — `/home/viddharth/VoiceAssistant/data/assistant.db`
- `Kestrel:Endpoints:Https:Url` — `https://0.0.0.0:5443`

## Run instructions

1. Start the local Python microservices:
   - `stt-service`
   - `tts-service`
   - `ocr-service`
2. Ensure Ollama is running on `http://localhost:11434/`.
3. Run the API:
   ```bash
   dotnet run --project VoiceAssistant.API/VoiceAssistant.API.csproj
   ```
4. Open the browser and go to the local UI, usually on the configured HTTPS port.

## Developer notes

- The API uses `Context.Items` to track the active assistant persona per SignalR connection.
- The browser UI currently uses press/hold-to-speak; true wake-word activation is not implemented in the browser.
- To switch assistants, say or type:
  - `switch to Stocky`
  - `switch to Nova`

## Project structure

```
/VoiceAssistant
├── VoiceAssistant.sln
├── README.md
├── .gitignore
├── start.sh
├── certs/
├── data/
├── stt-service/
├── tts-service/
├── ocr-service/
├── VoiceAssistant.API/
├── VoiceAssistant.Core/
├── VoiceAssistant.Infrastructure/
├── VoiceAssistant.Services/
├── VoiceAssistant.Data/
├── PHASE1.md
├── PHASE2.md
├── Phase2x.md
```

## Notes

- Keep `.gitignore` at the repo root.
- `.codex/` and `.agents/` are local metadata folders and should remain at the root unless you want to remove them entirely.
- This README documents the current architecture and execution flow as implemented.
