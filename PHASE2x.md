# Voice Assistant — Phase 2.1 & 2.2 Completion

> Context restore file — paste at the start of any new chat to resume with full context.

---

## Phase 2.1 — Polish + UX Improvements

### Step 2 — Tailscale Remote Access
- Installed Avahi + Tailscale on Ubuntu
- Regenerated HTTPS cert to include both home IP and Tailscale IP as SANs
- Access from anywhere via `https://<tailscale-ip>:5443`

### Step 3 — Chat Mode + Response Toggle
**`VoiceHub.cs`**
- Added `ProcessText(string text, bool audioResponse)` hub method
- Mirrors `ProcessAudio` pipeline — enters after STT step
- Respects both per-session `audioResponse` param and global `TtsEnabled` config
- Added `SendAudioAsync(string text)` private helper — checks `TtsEnabled` and passes `VoiceSpeed` to TTS

**`index.html`**
- Added text input bar + Send button below mic button
- Enter key support on text input
- `sendText()` function invokes `ProcessText` hub method
- Per-session audio toggle removed later in favour of settings (see Step 4 cleanup)

### Step 4 — Settings Page
**`appsettings.json`** — added sections:
```json
"Services": {
  "SttUrl": "http://localhost:5001/",
  "TtsUrl": "http://localhost:5002/",
  "OcrUrl": "http://localhost:5003/"
},
"AssistantSettings": {
  "TtsEnabled": true,
  "VoiceSpeed": 1.0,
  "DefaultAudioResponse": true,
  "BillScanMode": "ocr"
},
"Database": {
  "Path": "/home/viddharth/VoiceAssistant/data/assistant.db"
}
```

**`Program.cs`**
- Service URLs moved from hardcoded to config-driven
- DB path moved from hardcoded to config-driven

**`SettingsController.cs`** — new file in `VoiceAssistant.API/Controllers/`
- `GET /api/settings` — returns all settings
- `GET /api/settings/models` — calls Ollama `api/tags`, returns pulled model list
- `GET /api/settings/dbstats` — returns item/bill/event count and DB file size
- `POST /api/settings` — writes updated settings to `appsettings.json`
- `POST /api/settings/restart` — graceful API stop (process manager restarts)

**`settings.html`** — new file in `wwwroot/`
- LLM model selector (live from Ollama)
- Ollama base URL + timeout
- STT / TTS / OCR service URLs
- TTS enabled toggle
- Voice speed slider (0.5–2.0)
- Default audio response toggle
- Bill scan mode toggle (OCR vs Vision)
- DB stats panel (read-only)
- Save + Restart buttons

**`VoiceHub.cs`**
- Added `IConfiguration` injection
- `TtsEnabled` and `VoiceSpeed` properties read live from config
- `SendAudioAsync` uses `VoiceSpeed` when calling `_tts.SynthesiseAsync`

**All HTML pages** — added `⚙️ Settings` nav link

**`index.html` cleanup**
- Removed per-session audio toggle (redundant with settings)
- Removed `fetch('/api/settings')` on load
- `sendText()` always passes `audioResponse = true` (TtsEnabled in settings is the switch)

### Step 5 — jarvis.local Hostname
- Installed Avahi daemon
- Set hostname to `jarvis` via `hostnamectl`
- Regenerated HTTPS cert with `DNS:jarvis.local` + home IP as SANs
- Access via `http://jarvis.local:5000` (PC) and `https://jarvis.local:5443` (phone)

### Step 6 — Item Name Normalisation
**`InventoryService.cs`**
- Added `NormaliseName(string name)` static method
  - Lowercases and trims
  - Strips size/weight tokens (`1kg`, `500g`, `2l`, `1ltr` etc.)
  - Strips noise words (`pack`, `packet`, `pouch`, `bag`, `box`, `bottle`, `tin`, `jar`, `pkt`)
  - Collapses multiple spaces
- Applied to `UpdateStockAsync` before `GetOrCreateItemAsync`

**`BillService.cs`**
- Applied `InventoryService.NormaliseName()` when parsing bill items from LLM response

### Step 7 — Systemd Auto-start
- **Skipped by user** — services started manually via `start.sh` when needed

### Step 8 — gemma3:4b Vision for Bill Scanning
**`ILLMService.cs`**
- Added `ChatWithImageAsync(string prompt, byte[] imageData, CancellationToken ct)` to interface

**`OllamaService.cs`**
- Implemented `ChatWithImageAsync` — sends image as base64 in Ollama `api/generate` request

**`ITTSService.cs`**
- Added `speed` parameter: `SynthesiseAsync(string text, double speed = 1.0, CancellationToken ct)`

**`TTSService.cs`**
- Updated to pass `speed` in JSON payload to Kokoro Flask service

**`BillService.cs`**
- Added `IConfiguration` injection
- Added `BillPrompt` const
- `ScanBillAsync` now branches on `AssistantSettings:BillScanMode`
  - `"vision"` → calls `ChatWithImageAsync` directly, confidence 0.9
  - `"ocr"` → existing Tesseract + LLM text parse flow

**`SettingsController.cs`** + **`settings.html`**
- Added `BillScanMode` to GET, POST, and UI toggle

---

## Phase 2.2 — Smart Storage Location

### DB Layer

**`Item.cs`**
- Added 3 nullable location columns: `CupboardCode`, `SlotNumber`, `CategoryNumber`

**`Cupboard.cs`** — new model in `VoiceAssistant.Core/Models/`
- `Id`, `Code` (e.g. "C1"), `Name`, `Description`

**`StorageCategory.cs`** — new model in `VoiceAssistant.Core/Models/`
- `Id`, `Number` (e.g. 1), `Name` (e.g. "Grains & Pulses")

**`AppDbContext.cs`**
- Added `DbSet<Cupboard>` and `DbSet<StorageCategory>`
- Added model config for both — unique index on `Code` and `Number`

**`IInventoryRepository.cs`**
- Added 7 new method signatures:
  - `GetItemsByCupboardAsync`
  - `GetItemsByCategoryNumberAsync`
  - `UpdateItemLocationAsync`
  - `GetAllCupboardsAsync`
  - `UpsertCupboardAsync`
  - `GetAllCategoriesAsync`
  - `UpsertCategoryAsync`

**`InventoryRepository.cs`**
- Implemented all 7 new methods

> ⚠️ DB was reset (`rm assistant.db`) to pick up new schema. `EnsureCreated()` recreates on startup.

### Tools (all in `VoiceAssistant.Infrastructure/Tools/`)

**`FindItemTool.cs`**
- Name: `find_item`
- "Where is the rice?" → returns cupboard code + slot

**`UpdateLocationTool.cs`**
- Name: `update_location`
- Input: `item_name, cupboard_code, slot_number`
- "Rice is in cupboard C1 slot 2" → updates DB

**`GetCupboardContentsTool.cs`**
- Name: `get_cupboard_contents`
- Input: cupboard code
- "What's in cupboard C1?" → lists items with slot and stock status

**`GetCategoryItemsTool.cs`**
- Name: `get_category_items`
- Input: category number
- "Show me category 1" → lists items with stock status

**`Program.cs`** — registered all 4 new tools as `ITool`

**`VoiceHub.cs`** — added 4 new tool examples to `BuildToolsPrompt()`

### API

**`CupboardController.cs`** — new file in `VoiceAssistant.API/Controllers/`
- `GET  /api/cupboards` — list all cupboards
- `POST /api/cupboards` — add or update cupboard
- `GET  /api/cupboards/{code}/items` — items in a cupboard
- `POST /api/cupboards/item-location` — update item location
- `GET  /api/categories` — list all categories
- `POST /api/categories` — add or update category

### UI

**`inventory.html`**
- By Status / By Cupboard view toggle
- Cupboard view groups items by cupboard, ordered by slot then name
- Items without location grouped under "No Location"
- 📍 button on every card opens inline location editor
  - Cupboard dropdown (populated from `/api/cupboards`)
  - Slot number input
  - Save posts to `/api/cupboards/item-location`

**`settings.html`**
- Cupboards manager section — list + add/update form (code, name, description)
- Categories manager section — list + add/update form (number, name)
- Both load on page load alongside other settings

---

## Current File Inventory

### `VoiceAssistant.Core`
```
Interfaces/
  ILLMService.cs         ← ChatAsync + ChatWithImageAsync
  ISTTService.cs
  ITTSService.cs         ← SynthesiseAsync(text, speed, ct)
  ITool.cs
  IInventoryService.cs
  IInventoryRepository.cs ← 13 methods total
  IBillService.cs
  IOCRService.cs
Models/
  Item.cs                ← +CupboardCode, SlotNumber, CategoryNumber
  Inventory.cs
  Bill.cs
  BillItem.cs
  StockEvent.cs
  Cupboard.cs            ← new
  StorageCategory.cs     ← new
```

### `VoiceAssistant.Infrastructure`
```
Services/
  OllamaService.cs       ← +ChatWithImageAsync
  STTService.cs
  TTSService.cs          ← +speed param
  OCRService.cs
Tools/
  UpdateStockTool.cs
  GetInventoryTool.cs
  ShoppingListTool.cs
  FindItemTool.cs        ← new
  UpdateLocationTool.cs  ← new
  GetCupboardContentsTool.cs ← new
  GetCategoryItemsTool.cs    ← new
```

### `VoiceAssistant.Services`
```
Services/
  InventoryService.cs    ← +NormaliseName
  BillService.cs         ← +vision mode, +normalisation
```

### `VoiceAssistant.Data`
```
AppDbContext.cs          ← +Cupboards, StorageCategories
Repositories/
  InventoryRepository.cs ← +7 location methods
```

### `VoiceAssistant.API`
```
Program.cs               ← config-driven URLs, +4 tools registered
appsettings.json         ← +Services, AssistantSettings, Database sections
Hubs/
  VoiceHub.cs            ← +ProcessText, +TtsEnabled, +VoiceSpeed, +4 tool prompts
Controllers/
  SettingsController.cs  ← new
  CupboardController.cs  ← new
  BillController.cs
  InventoryController.cs
  TestController.cs
wwwroot/
  index.html             ← +text input, -audio toggle
  bill.html              ← +settings nav
  inventory.html         ← +cupboard view, +location editor
  settings.html          ← new
```

---

## Ports Reference (unchanged)

| Service | Port |
|---|---|
| .NET API HTTP  | 5000 |
| .NET API HTTPS | 5443 |
| STT (Whisper)  | 5001 |
| TTS (Kokoro)   | 5002 |
| OCR (Tesseract)| 5003 |
| Ollama         | 11434 |

---

## Phase 3 — What's Next: Financial Assistant

### Goal
Query spending history, track budgets, and get voice reports on finances — all from bill scan data already in SQLite.

### New DB
- `Budgets` table — `id`, `category`, `monthly_limit`, `created_at`

### New Tools
- `GetMonthlySpendTool` — "how much did I spend this month / in April?"
- `GetSpendByCategoryTool` — "how much did I spend on groceries?"
- `GetSpendTrendTool` — "am I spending more than last month?"
- `GetTopItemsTool` — "what do I spend most on?"
- `GetBudgetStatusTool` — "am I over budget?"

### New Service
- `IFinancialService` + `FinancialService` in `VoiceAssistant.Services`
- `IFinancialRepository` + `FinancialRepository` in `VoiceAssistant.Data`

### New Controller
- `FinanceController.cs` — REST endpoints for charts and budget management

### New UI
- `finance.html` — monthly spend bar chart, category breakdown, budget status cards, voice-queryable

### Build Order
1. DB migration — `Budgets` table + migration or EnsureCreated reset
2. `IFinancialRepository` + `FinancialRepository` — all SQL queries
3. `IFinancialService` + `FinancialService` — business logic
4. 5 new `ITool` implementations
5. `FinanceController.cs`
6. `finance.html`
