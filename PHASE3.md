# Voice Assistant — Phase 3 Completion

> Context restore file — paste at the start of any new chat to resume with full context.

---

## Phase 3 — Financial Assistant

### Goal
Query spending history, track budgets, and get voice reports on finances — all driven by bill scan data already in SQLite from Phase 2.

---

## Changes Made

### DB Layer

**`Budget.cs`** — new model in `VoiceAssistant.Core/Models/`
- `Id`, `Category` (e.g. "groceries", "total"), `MonthlyLimit`, `CreatedAt`, `UpdatedAt`
- Unique index on `Category`

**`AppDbContext.cs`**
- Added `DbSet<Budget> Budgets`
- Added model config — unique index on `Category`
- Fixed typo: `/ Cupboard` → `// Cupboard`

**`IFinancialRepository.cs`** — new interface in `VoiceAssistant.Core/Interfaces/`
```
GetTotalSpendAsync(from, to)
GetSpendByCategoryAsync(from, to)
GetTopItemsAsync(from, to, topN)
GetMonthlyTrendAsync(months)
GetAllBudgetsAsync()
UpsertBudgetAsync(category, monthlyLimit)
DeleteBudgetAsync(id)
```

**`FinancialRepository.cs`** — new file in `VoiceAssistant.Data/Repositories/`
- All queries run against `BillItems` joined with `Bills` and `Items`
- Spend grouped by `Item.Category` for category breakdown
- Monthly trend groups by `Bill.Date` year+month
- Budget upsert matches on `Category.ToLower()`

**`Program.cs`**
```csharp
builder.Services.AddScoped<IFinancialRepository, FinancialRepository>();
builder.Services.AddScoped<IFinancialService, FinancialService>();
builder.Services.AddScoped<ITool, GetMonthlySpendTool>();
builder.Services.AddScoped<ITool, GetSpendByCategoryTool>();
builder.Services.AddScoped<ITool, GetSpendTrendTool>();
builder.Services.AddScoped<ITool, GetTopItemsTool>();
builder.Services.AddScoped<ITool, GetBudgetStatusTool>();
builder.Services.AddScoped<ITool, SetBudgetTool>();
```

> ⚠️ DB was reset (`rm assistant.db`) to pick up the new `Budgets` table.

---

### Service Layer

**`IFinancialService.cs`** — new interface in `VoiceAssistant.Core/Interfaces/`
```
GetMonthlySpendAsync(year, month)
GetSpendByCategoryAsync(year, month)
GetSpendTrendAsync()
GetTopItemsAsync(year, month)
GetBudgetStatusAsync()
SetBudgetAsync(category, limit)
GetDashboardDataAsync(year, month)   ← for UI
```

**`FinancialService.cs`** — new file in `VoiceAssistant.Services/Services/`
- All amounts formatted as `₹N,NNN` (Indian locale)
- `GetSpendTrendAsync` compares last 2 months, returns % change and direction
- `GetBudgetStatusAsync` checks both `total` budget and per-category budgets
- `GetDashboardDataAsync` returns a single object with all data the UI needs — total, prev total, change %, categories, top items, trend, budgets with spent amounts

---

### Tools (all in `VoiceAssistant.Infrastructure/Tools/`)

| Tool | Name | Trigger examples |
|---|---|---|
| `GetMonthlySpendTool` | `get_monthly_spend` | "how much did I spend this month", "spend in April" |
| `GetSpendByCategoryTool` | `get_spend_by_category` | "spending by category", "how much on groceries" |
| `GetSpendTrendTool` | `get_spend_trend` | "am I spending more than last month", "spending trend" |
| `GetTopItemsTool` | `get_top_items` | "what do I spend most on", "top expenses" |
| `GetBudgetStatusTool` | `get_budget_status` | "am I over budget", "budget status" |
| `SetBudgetTool` | `set_budget` | "set budget groceries 5000", "set total budget 15000" |

Input format for date-based tools: `YYYY-MM` — empty input defaults to current month.
Input format for `set_budget`: `category, amount`

**`VoiceHub.cs`** — added 7 new tool examples to `BuildToolsPrompt()`:
```
get_monthly_spend, get_spend_by_category, get_spend_trend,
get_top_items, get_budget_status, set_budget
```

---

### API

**`FinanceController.cs`** — new file in `VoiceAssistant.API/Controllers/`

| Method | URL | Purpose |
|---|---|---|
| GET | `/api/finance/dashboard?year=&month=` | Full dashboard data |
| GET | `/api/finance/trend?months=6` | Monthly trend array |
| GET | `/api/finance/budgets` | List all budgets |
| POST | `/api/finance/budgets` | Add / update budget |
| DELETE | `/api/finance/budgets/{id}` | Delete budget |

---

### UI

**`finance.html`** — new file in `VoiceAssistant.API/wwwroot/`

Features:
- Month navigator (‹ / ›) — loads data for any past month
- Summary cards — total spend, top category, budget count, % change vs last month
- 6-month trend bar chart — drawn on HTML5 Canvas, responsive, redraws on resize
- Category breakdown — horizontal bar chart, relative to top category
- Top 5 items by spend — ranked list
- Budget manager — progress bars per budget (green/amber/red), delete button, add form
- Reloads on tab visibility change

**All other HTML pages** — added `💰 Finance` nav link

**`SettingsController.cs`** — added `budgetCount` to DB stats

---

## Complete File Inventory (all phases)

### `VoiceAssistant.Core`
```
Interfaces/
  ILLMService.cs              ← ChatAsync + ChatWithImageAsync
  ISTTService.cs
  ITTSService.cs              ← SynthesiseAsync(text, speed, ct)
  ITool.cs
  IInventoryService.cs
  IInventoryRepository.cs     ← 13 methods
  IBillService.cs
  IOCRService.cs
  IFinancialRepository.cs     ← new (Phase 3)
  IFinancialService.cs        ← new (Phase 3)
Models/
  Item.cs                     ← +CupboardCode, SlotNumber, CategoryNumber
  Inventory.cs
  Bill.cs
  BillItem.cs
  StockEvent.cs
  Cupboard.cs                 ← Phase 2.2
  StorageCategory.cs          ← Phase 2.2
  Budget.cs                   ← Phase 3
```

### `VoiceAssistant.Infrastructure`
```
Services/
  OllamaService.cs            ← +ChatWithImageAsync
  STTService.cs
  TTSService.cs               ← +speed param
  OCRService.cs
Tools/
  UpdateStockTool.cs
  GetInventoryTool.cs
  ShoppingListTool.cs
  FindItemTool.cs             ← Phase 2.2
  UpdateLocationTool.cs       ← Phase 2.2
  GetCupboardContentsTool.cs  ← Phase 2.2
  GetCategoryItemsTool.cs     ← Phase 2.2
  GetMonthlySpendTool.cs      ← Phase 3
  GetSpendByCategoryTool.cs   ← Phase 3
  GetSpendTrendTool.cs        ← Phase 3
  GetTopItemsTool.cs          ← Phase 3
  GetBudgetStatusTool.cs      ← Phase 3
  SetBudgetTool.cs            ← Phase 3
```

### `VoiceAssistant.Services`
```
Services/
  InventoryService.cs         ← +NormaliseName
  BillService.cs              ← +vision mode, +normalisation
  FinancialService.cs         ← new (Phase 3)
```

### `VoiceAssistant.Data`
```
AppDbContext.cs               ← +Cupboards, StorageCategories, Budgets
Repositories/
  InventoryRepository.cs      ← +7 location methods
  FinancialRepository.cs      ← new (Phase 3)
```

### `VoiceAssistant.API`
```
Program.cs                    ← +FinancialRepository, FinancialService, 6 tools
appsettings.json              ← Services, AssistantSettings, Database sections
Hubs/
  VoiceHub.cs                 ← +ProcessText, +TtsEnabled, +VoiceSpeed, +13 tool prompts
Controllers/
  SettingsController.cs       ← +budgetCount in dbstats
  CupboardController.cs       ← Phase 2.2
  FinanceController.cs        ← Phase 3
  BillController.cs
  InventoryController.cs
  TestController.cs
wwwroot/
  index.html                  ← +text input, +finance nav
  bill.html                   ← +finance nav
  inventory.html              ← +cupboard view, +location editor, +finance nav
  settings.html               ← +cupboard manager, +category manager, +finance nav
  finance.html                ← new (Phase 3)
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

## Phase 4 — Internet Price Comparison

### Goal
Search the web for current prices of items in your shopping list and surface the best deals — all triggered by voice or from the inventory page.

### What's Needed

**New Dependency — Web Search**
- Option A: SerpAPI / Google Search API (paid, reliable)
- Option B: Bing Search API (cheaper)
- Option C: Local scraping via Playwright headless browser (free, fragile)

**New Tools**
- `SearchItemPriceTool` — "how much does rice cost online", "find best price for toor dal"
- `CompareShoppingListPricesTool` — "find prices for my shopping list"

**New Service**
- `IPriceSearchService` + `PriceSearchService` in `VoiceAssistant.Services`
- Calls search API, parses top results for price mentions
- Optional: scrapes specific sites (BigBasket, Blinkit, Amazon)

**New Infrastructure**
- `WebSearchService.cs` in `VoiceAssistant.Infrastructure/Services/`
- HTTP client to search API of choice

**New Controller**
- `PriceController.cs` — REST endpoints for price search results

**New UI additions**
- `inventory.html` — "Find Prices" button on shopping list
- `prices.html` — price comparison results page (optional standalone)

### Key Decision Before Starting
Need to decide on the search/scraping approach — SerpAPI, Bing, or Playwright. Each has different setup cost and reliability tradeoffs. Playwright is free but requires a headless Chromium install and site-specific scrapers. SerpAPI is the simplest integration.

### Build Order
1. Choose and set up search provider, add API key to `appsettings.json`
2. `IPriceSearchService` + `PriceSearchService`
3. `WebSearchService` HTTP client
4. `SearchItemPriceTool` + `CompareShoppingListPricesTool`
5. `PriceController.cs`
6. UI additions
