# Voice Assistant — Phase 2 Documentation

> **Context restore file** — paste this at the start of any new chat to resume work with full context.
> Phase 1 doc is in `PHASE1.md`. Read both for full context.

---

## Project Overview

A fully local, offline personal voice assistant running on a home PC, accessible from any device on home WiFi via a browser. Phase 2 adds smart home grocery inventory management — bill scanning, stock tracking, voice updates, and shopping list generation.

**Developer:** .NET developer, 9 years experience, Ubuntu 24.04 LTS
**Hardware:** AMD Ryzen 5 5600GT · 16GB RAM · No dedicated GPU
**Local IP:** 192.168.1.106 (static, locked via nmcli)

---

## Full Roadmap Status

| Phase | Description | Status |
|---|---|---|
| 1 | Local voice assistant — STT, LLM, TTS, WebSocket, browser UI | ✅ Complete |
| 2 | Smart home inventory — bill scanning, item tracking, shopping list | ✅ Complete |
| 3 | Financial assistant — expense tracking, budget analysis, voice reports | 🔜 Next |
| 4 | Internet price comparison — web search, price scraping, deal finder | ⏳ Future |

---

## Architecture (Phase 1 + Phase 2)

```
Browser (phone/laptop on home WiFi)
        ↕ HTTPS WebSocket (SignalR) + REST
ASP.NET Core 9 Web API  (port 5000 HTTP / 5443 HTTPS)
        ↕ HttpClient calls (localhost)
┌──────────────┬─────────────┬─────────────┬─────────────┐
│ STT Service  │ LLM Service │ TTS Service │ OCR Service │
│ Python/Flask │ Ollama      │ Python/Flask│ Python/Flask│
│ port 5001    │ port 11434  │ port 5002   │ port 5003   │
│ faster-whis  │ gemma3:4b   │ Kokoro-82M  │ Tesseract   │
│ per          │             │             │ + OpenCV    │
└──────────────┴─────────────┴─────────────┴─────────────┘
        ↕
┌─────────────────────────────────────────────────────────┐
│ VoiceAssistant.Services (domain layer)                  │
│ InventoryService · BillService                          │
└─────────────────────────────────────────────────────────┘
        ↕
┌─────────────────────────────────────────────────────────┐
│ VoiceAssistant.Data (data layer)                        │
│ AppDbContext · InventoryRepository · SQLite DB          │
└─────────────────────────────────────────────────────────┘
```

### Voice Pipeline Flow (Phase 1, unchanged)
```
User speaks → MediaRecorder (webm) → SignalR → VoiceHub
→ STTService → Whisper (webm→wav→text)
→ LLMService → Ollama (text→tool JSON or plain answer)
→ TryExecuteToolAsync → ITool → DB operation → result string
→ StripMarkdown → TTSService → Kokoro (text→wav)
→ base64 → SignalR → Browser → Audio plays
```

### Bill Scan Flow (Phase 2)
```
Phone camera / file upload → /api/bill/scan
→ OCRService → OpenCV preprocess → Tesseract → raw text + confidence
→ LLMService → parse OCR text → structured JSON items
→ Return to browser for user review
→ User fixes errors → /api/bill/confirm
→ BillService.SaveBillAsync → Bills + BillItems + StockEvents → SQLite
```

---

## Project Structure

```
~/VoiceAssistant/
├── VoiceAssistant.sln
├── start.sh                              ← starts all 4 services + API
├── PHASE1.md                             ← Phase 1 documentation
├── PHASE2.md                             ← this file
├── data/
│   ├── assistant.db                      ← SQLite database (gitignored)
│   └── bills/                            ← uploaded bill images (gitignored)
├── certs/                                ← HTTPS cert files (gitignored)
├── stt-service/                          ← Python STT microservice
│   ├── app.py
│   └── venv/
├── tts-service/                          ← Python TTS microservice
│   ├── app.py
│   ├── kokoro-v1.0.onnx
│   ├── voices-v1.0.bin
│   └── venv/
├── ocr-service/                          ← Python OCR microservice (Phase 2)
│   ├── app.py
│   └── venv/
├── VoiceAssistant.Core/                  ← interfaces and models
│   ├── Interfaces/
│   │   ├── ISTTService.cs
│   │   ├── ILLMService.cs
│   │   ├── ITTSService.cs
│   │   ├── IOCRService.cs                ← new in Phase 2
│   │   ├── IInventoryService.cs          ← new in Phase 2
│   │   ├── IInventoryRepository.cs       ← new in Phase 2
│   │   ├── IBillService.cs               ← new in Phase 2
│   │   └── ITool.cs
│   └── Models/
│       ├── Item.cs                       ← new in Phase 2
│       ├── Inventory.cs                  ← new in Phase 2
│       ├── Bill.cs                       ← new in Phase 2
│       ├── BillItem.cs                   ← new in Phase 2
│       └── StockEvent.cs                 ← new in Phase 2
├── VoiceAssistant.Data/                  ← new in Phase 2
│   ├── AppDbContext.cs
│   └── Repositories/
│       └── InventoryRepository.cs
├── VoiceAssistant.Services/              ← new in Phase 2
│   └── Services/
│       ├── InventoryService.cs
│       └── BillService.cs
├── VoiceAssistant.Infrastructure/
│   ├── Services/
│   │   ├── OllamaService.cs
│   │   ├── STTService.cs
│   │   ├── TTSService.cs
│   │   └── OCRService.cs                 ← new in Phase 2
│   └── Tools/
│       ├── UpdateStockTool.cs            ← new in Phase 2
│       ├── GetInventoryTool.cs           ← new in Phase 2
│       └── ShoppingListTool.cs           ← new in Phase 2
└── VoiceAssistant.API/
    ├── Program.cs
    ├── appsettings.json
    ├── Controllers/
    │   ├── TestController.cs
    │   ├── BillController.cs             ← new in Phase 2
    │   └── InventoryController.cs        ← new in Phase 2
    ├── Hubs/
    │   └── VoiceHub.cs
    └── wwwroot/
        ├── index.html                    ← voice assistant UI
        ├── bill.html                     ← bill scanner UI (Phase 2)
        └── inventory.html                ← inventory UI (Phase 2)
```

---

## Database Schema

### Tables

**Items** — master list of grocery items
```
Id           int PK
Name         string (unique)
Category     string default "grocery"
Unit         string default "units"
MinQty       decimal default 1
CreatedAt    datetime
```

**Inventory** — current stock level (one-to-one with Item)
```
Id           int PK
ItemId       int FK → Items.Id
CurrentQty   decimal
LastUpdated  datetime
```

**Bills** — scanned grocery bill records
```
Id           int PK
Date         datetime
Store        string
ImagePath    string
RawOcr       string
CreatedAt    datetime
```

**BillItems** — individual items parsed from a bill
```
Id           int PK
BillId       int FK → Bills.Id
ItemId       int FK → Items.Id
Qty          decimal
Price        decimal
Confidence   float  ← OCR confidence 0.0-1.0
ParsedAt     datetime
```

**StockEvents** — full audit trail of every stock change
```
Id           int PK
ItemId       int FK → Items.Id
EventType    string (BillScan / VoiceCommand / ManualUI)
QtyChange    decimal (positive = restock, negative = consumed)
Source       string (BillScan / VoiceCommand / ManualUI)
Note         string
CreatedAt    datetime
```

### Why StockEvents matters
Every stock change — whether from scanning a bill, saying "I finished the rice", or tapping +/- in the UI — writes a row to StockEvents. This gives you:
- Full undo capability
- Consumption trend analysis
- Smart shopping predictions in Phase 3
- Financial spend tracking in Phase 3

---

## EF Core Concepts Used

### What is EF Core?
Entity Framework Core is a .NET ORM (Object Relational Mapper). It lets you work with a database using C# classes instead of writing raw SQL. You define your tables as C# classes (models), and EF Core handles creating the DB, running queries, and saving data.

### Code First Approach
We used Code First — meaning we wrote C# model classes first, then EF Core created the database from them. No SQL scripts needed.

```
C# Models → DbContext → EnsureCreated() → SQLite DB file
```

### DbContext (AppDbContext.cs)
The DbContext is your gateway to the database. It holds DbSet<T> properties — one per table. When you query or save through these, EF Core translates it to SQL behind the scenes.

```csharp
public DbSet<Item> Items => Set<Item>();        // → Items table
public DbSet<Inventory> Inventories => Set<Inventory>(); // → Inventories table
```

### EnsureCreated()
Called on startup in Program.cs. If the DB file doesn't exist, EF Core creates it and all tables based on your model classes and `OnModelCreating` configuration. If it already exists, it does nothing.

```csharp
db.Database.EnsureCreated(); // creates assistant.db if not exists
```

### AsNoTracking()
By default EF Core tracks every entity it loads — it watches for changes so it can save them. This uses memory and can return stale cached data. `AsNoTracking()` tells EF Core to load data fresh without tracking — used on all read-only queries in InventoryRepository.

```csharp
return await _db.Items.AsNoTracking().Include(x => x.Inventory).ToListAsync();
```

### Repository Pattern
The repository pattern puts all database access behind an interface. Controllers and services never touch DbContext directly — they only call repository methods. This means:
- Business logic is separate from data access
- Easy to unit test (mock the repository)
- Easy to swap SQLite for PostgreSQL later — only repository changes

```
VoiceHub → IInventoryService → IInventoryRepository → AppDbContext → SQLite
```

### Service Layer
The service layer sits between tools/controllers and the repository. It contains business logic — things like "what does it mean to update stock?" (create item if not exists, log event, update inventory). This keeps the tools thin and the logic reusable.

```
UpdateStockTool.ExecuteAsync() → IInventoryService.UpdateStockAsync() → IInventoryRepository.UpdateStockAsync()
```

---

## Tool Call System

### How It Works
1. User speaks → Whisper transcribes to text
2. .NET sends text + system prompt to Ollama
3. System prompt tells LLM: "if inventory related, respond with ONLY JSON"
4. LLM returns `{"tool": "update_stock", "input": "rice, -1, finished"}`
5. `TryExecuteToolAsync` extracts JSON, finds matching ITool by name, calls `ExecuteAsync`
6. Tool calls service → service calls repository → DB updated
7. Tool returns result string → TTS speaks it

### ITool Interface
```csharp
public interface ITool
{
    string Name { get; }           // matches "tool" in LLM JSON response
    string Description { get; }   // sent to LLM so it knows when to call it
    Task<string> ExecuteAsync(string input, CancellationToken ct = default);
}
```

### Adding a New Tool (Phase 3+)
1. Create a new class implementing `ITool` in `VoiceAssistant.Infrastructure/Tools/`
2. Register it in `Program.cs`: `builder.Services.AddScoped<ITool, YourNewTool>()`
3. The system prompt auto-includes it — LLM immediately knows about it

### Why JSON Instead of Ollama Native Function Calling
Ollama supports OpenAI-style function calling but it's model-dependent and inconsistent on smaller models. Using a JSON prompt is more reliable with gemma3:4b on CPU — the LLM just needs to output a JSON string which we parse ourselves.

---

## OCR Pipeline

### Why OpenCV + Tesseract
Phone photos of bills have uneven lighting, slight tilt, shadows, and glossy paper glare. OpenCV preprocesses the image before Tesseract reads it, significantly improving accuracy on real-world photos.

### OpenCV Preprocessing Steps
```
Phone photo
  → Resize (max 1800px width, preserve aspect)
  → Grayscale conversion
  → Denoise (fastNlMeansDenoising)
  → Adaptive threshold (handles uneven lighting)
  → Deskew (correct tilt up to 15 degrees)
  → Sharpen
  → Feed to Tesseract
```

### Why Adaptive Threshold?
Regular threshold uses a single brightness value for the whole image. Adaptive threshold calculates the threshold per small region — much better for bills with shadows or uneven flash lighting from a phone camera.

### Confidence Scores
Tesseract returns a confidence score (0-100) per word. We average per line and return it as 0.0-1.0. In the bill scanner UI:
- Green bar = high confidence (≥0.7) — text read reliably
- Yellow bar = medium confidence (0.4-0.7) — review carefully
- Red bar = low confidence (<0.4) — likely wrong, fix before saving

### Why LLM Parsing After OCR
Tesseract makes mistakes on grocery bills — "Rice 5kg" becomes "Rice Skq", "Sugar 1kg" becomes "Sugartkg". The LLM understands context and corrects these errors when parsing into structured JSON. It knows "Skq" after "Rice" probably means "5kg".

---

## Key Files Explained (Phase 2)

### `ocr-service/app.py`
Flask service on port 5003. Accepts image upload, runs OpenCV preprocessing, then Tesseract OCR with word-level confidence data. Returns JSON with full text, per-line text with confidence, and overall confidence score.

### `VoiceAssistant.Data/AppDbContext.cs`
EF Core DbContext. Defines all 5 tables and their relationships via `OnModelCreating`. Enum values (EventType, EventSource) stored as strings for readability in SQLite.

### `VoiceAssistant.Data/Repositories/InventoryRepository.cs`
All database access. Uses `AsNoTracking()` on reads to avoid EF Core caching stale data. `UpdateStockAsync` always writes a StockEvent alongside the inventory update — audit trail is automatic.

### `VoiceAssistant.Services/Services/InventoryService.cs`
Business logic for inventory. `FormatQty()` cleans decimal display (1.0 → "1"). `Pluralise()` fixes grammar ("1 unit" not "1 units"). `GetShoppingListAsync()` queries low/out items and formats for voice output.

### `VoiceAssistant.Services/Services/BillService.cs`
Orchestrates bill scanning. Calls OCR service → sends raw text to LLM with parsing prompt (separate from tools prompt) → parses LLM JSON response → strips markdown fences → returns `ParsedBill` for user review. `SaveBillAsync` saves Bill + BillItems + StockEvents atomically.

### `VoiceAssistant.API/Controllers/BillController.cs`
Two endpoints:
- `POST /api/bill/scan` — receives image, runs OCR+LLM, returns parsed items for review. Does NOT save to DB yet.
- `POST /api/bill/confirm` — receives reviewed/corrected items from UI, saves to DB.

### `VoiceAssistant.API/Controllers/InventoryController.cs`
- `GET /api/inventory` — returns all items with current stock levels
- `POST /api/inventory/update` — manual +/- stock update from UI, logs as ManualUI event

### `wwwroot/bill.html`
Bill scanner page. Camera capture (`capture="environment"`) for phone, file upload for desktop. Shows parsed items with editable fields and colour-coded confidence bars. User reviews and corrects before confirming. Step qty/price increment is 1 (not 0.1).

### `wwwroot/inventory.html`
Inventory dashboard. Shows items grouped by Out/Low/In Stock with colour dots. +/- buttons call API directly. Summary cards show counts. Shopping list button shows items needing restock. Auto-reloads when page becomes visible again (visibilitychange event).

---

## Voice Commands That Work

| You say | Tool called | Response |
|---|---|---|
| "I finished the rice" | update_stock | "rice stock decreased. You have 0 units left." |
| "I bought 2kg of rice" | update_stock | "rice stock increased. You have 2 units left." |
| "rice is running low" | update_stock | updates with -1 |
| "what do I have at home" | get_inventory | lists all items with quantities |
| "what should I buy today" | get_shopping_list | lists low/out items |
| "going shopping" | get_shopping_list | lists low/out items |
| "check my stock" | get_inventory | lists all items |

---

## Ports Reference

| Service | Port | Protocol |
|---|---|---|
| .NET API (HTTP) | 5000 | HTTP |
| .NET API (HTTPS) | 5443 | HTTPS |
| STT (Whisper) | 5001 | HTTP (localhost only) |
| TTS (Kokoro) | 5002 | HTTP (localhost only) |
| OCR (Tesseract+OpenCV) | 5003 | HTTP (localhost only) |
| Ollama (LLM) | 11434 | HTTP (localhost only) |

---

## Starting All Services

```bash
~/VoiceAssistant/start.sh
```

Or manually in 4 terminals:

```bash
# Terminal 1 — STT
cd ~/VoiceAssistant/stt-service && source venv/bin/activate && python3 app.py

# Terminal 2 — TTS
cd ~/VoiceAssistant/tts-service && source venv/bin/activate && python3 app.py

# Terminal 3 — OCR
cd ~/VoiceAssistant/ocr-service && source venv/bin/activate && python3 app.py

# Terminal 4 — .NET API
cd ~/VoiceAssistant && dotnet run --project VoiceAssistant.API
```

---

## Accessing the Assistant

| Page | URL (PC) | URL (Phone) |
|---|---|---|
| Voice assistant | http://localhost:5000 | https://192.168.1.106:5443 |
| Bill scanner | http://localhost:5000/bill.html | https://192.168.1.106:5443/bill.html |
| Inventory | http://localhost:5000/inventory.html | https://192.168.1.106:5443/inventory.html |

---

## Configuration

### appsettings.json
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

### DB Location
```
/home/viddharth/VoiceAssistant/data/assistant.db
```

### Bill Images Location
```
/home/viddharth/VoiceAssistant/data/bills/
```

### Checking DB from VS Code
Install "SQLite Viewer" extension by Florian Klampfer. Open `assistant.db` directly.

### Checking DB from terminal
```bash
sqlite3 ~/VoiceAssistant/data/assistant.db ".tables"
sqlite3 ~/VoiceAssistant/data/assistant.db "SELECT i.Name, inv.CurrentQty FROM Items i JOIN Inventories inv ON i.Id = inv.ItemId;"
sqlite3 ~/VoiceAssistant/data/assistant.db "SELECT * FROM StockEvents ORDER BY CreatedAt DESC LIMIT 10;"
```

---

## Issues Hit and Fixed in Phase 2

| Issue | Fix |
|---|---|
| EF Core packages missing in VoiceAssistant.Data | Manually edited .csproj to add PackageReference |
| Raw string literal with JSON confused compiler | Replaced with string concatenation |
| LLM ignoring tool JSON prompt, answering conversationally | Rewrote prompt with explicit RULES and EXAMPLES |
| LLM wrapping JSON in markdown code fences | Added `ExtractJson()` to strip fences and find `{...}` |
| LLM adding conversational text before JSON | `ExtractJson()` finds first `{` to last `}` |
| EF Core returning stale cached data after update | Added `AsNoTracking()` to all read queries |
| Rice +/- not responding in inventory UI | Rewrote inventory.html with direct `.onclick` handlers |
| Bill qty counter going 1→1.1 instead of 1→2 | Changed input step from 0.1 to 1 |
| TTS saying "1.0 units" | Added `FormatQty()` and `Pluralise()` in InventoryService |
| Mobile mic inconsistent — first tap holds, second needs hold | Rewrote to detect mobile via `ontouchstart`, tap-toggle on mobile vs hold on desktop |
| IP changed from .106 to .107 breaking HTTPS | Set static IP via nmcli, regenerated cert for .107 |
| Voice stock update not persisting to DB | Strengthened LLM prompt with explicit rules and examples |
| Inventory page showing stale data after voice update | Added `visibilitychange` event listener to reload on page focus |

---

## Architectural Improvements Made (from code review suggestions)

| Suggestion | What we did |
|---|---|
| Add explicit API layer | Already done — UI never touches DB directly, always via API controllers |
| Add domain/service layer | Added VoiceAssistant.Services project between tools and data |
| Add event/history table early | StockEvents table logs every change with source and timestamp |
| OCR parser should return confidence | Tesseract confidence per line returned and shown in UI as colour bar |
| Background workers for OCR | Deferred — in bucket list |

---

## Bucket List (deferred items)

- [ ] Save raw OCR text to Bills.RawOcr field (currently empty)
- [ ] Better date parsing from bill images
- [ ] Item name normalisation (RED BULL ENER → Red Bull Energy Drink)
- [ ] Set hostname to `jarvis.local` via mDNS for cleaner URL
- [ ] Pi-hole local DNS for `assistant.home` domain
- [ ] Convert start.sh to systemd services (auto-start on boot)
- [ ] Add conversation history / memory to LLM context
- [ ] Background worker for OCR processing (IHostedService + queue)
- [ ] Android APK via Flutter when feature complete
- [ ] iOS via TestFlight when ready
- [ ] Switch LLM model via voice command without restart
- [ ] Unit tests for InventoryService and BillService

---

## Phase 3 — What's Next

**Financial Assistant**

New tools needed:
- `GetSpendingByCategory` — "how much did I spend on groceries this month?"
- `GetMonthlySpendSummary` — "summarise my spending for May"
- `GetSpendingTrend` — "am I spending more or less than last month?"

New DB queries needed (no schema changes — all data already in BillItems + Bills):
```sql
SELECT SUM(bi.Price * bi.Qty) as Total, strftime('%Y-%m', b.Date) as Month
FROM BillItems bi JOIN Bills b ON bi.BillId = b.Id
GROUP BY Month ORDER BY Month DESC;
```

New .NET project needed:
- `VoiceAssistant.Services/Services/FinancialService.cs`

No new Python microservices needed — all data is already in SQLite from Phase 2 bill scanning.

---

## GitHub

Repository: `https://github.com/saibhargav22/VoiceAssistant`
Branch: `main`
Last commit: `feat: phase 2 complete`

