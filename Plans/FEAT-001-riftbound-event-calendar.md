# FEAT-001 — Riftbound Event Calendar
**Purpose**: Riftbound eseményeket jelenít meg naptárban Budapest 50km-es körzetéből, folyamatos frissítéssel.
**Audience**: Végfelhasználók (magyarországi Riftbound játékosok)
**Status**: To Do

---

## Background
A https://locator.riftbound.uvsgames.com/ oldal **Next.js App Router + React Server Components (RSC)** alapú alkalmazás. Az `_rsc=` query paraméteres kérések és a `0:{"b":...,"f":[...]}` formátumú válaszok a Next.js RSC wire protokollt jelzik — nincs hagyományos REST API. Az adatok headless böngészővel (Microsoft.Playwright) kinyerhetők a DOM-ból. Az oldal nem kínál beágyazható naptár nézetet vagy könnyen exportálható adatot. A cél egy saját web alkalmazás, amely automatikusan lekéri, szűri és folyamatosan frissítve naptárban mutatja a Budapest vonzáskörzetében lévő eseményeket.

## Goal
Egy működő Blazor WebAssembly + ASP.NET Core alkalmazás, amely:
- lekéri a Riftbound eseményeket (API-n vagy scrapinggel),
- Budapest koordinátáitól (47.4979°N, 19.0402°E) legfeljebb 50km-re szűri azokat (Haversine),
- MudBlazor naptárban jeleníti meg,
- 30 percenként automatikusan frissíti az adatokat a háttérben.

---

## Scope

### In Scope
- A locator.riftbound.uvsgames.com eseményadatainak lekérése (API vagy Playwright scraper)
- Budapest 50km-es sugárban való szűrés (Haversine formula)
- MudBlazor naptár nézet esemény listával
- Esemény részletei (cím, dátum, helyszín, formátum, URL) popup-ban
- ASP.NET Core BackgroundService 30 perces frissítési ciklussal
- IMemoryCache a legutóbb lekért adatok tárolásához

### Out of Scope
- Felhasználói autentikáció
- Személyre szabott szűrők (sugár módosítás, formátum szűrő) — v2
- Értesítések (push notification, email) — v2
- Adatbázis alapú perzisztencia (IMemoryCache elegendő v1-hez)
- Mobil app

---

## Acceptance criteria
- [ ] Az alkalmazás elindul és a böngészőben elérhető
- [ ] A naptár eseményeket jelenít meg Budapest 50km körzetéből
- [ ] 50km-en kívüli esemény nem jelenik meg
- [ ] Eseményre kattintva részletek láthatók (cím, dátum, helyszín, formátum, link)
- [ ] 30 percenként az adatok automatikusan frissülnek (BackgroundService fut)
- [ ] A GET /api/events végpont JSON-t ad vissza az eseményekkel
- [ ] Nincs compiler warning a buildben

---

## What does NOT change
- C:\Repos többi projektje (Moonset stb.) érintetlen marad
- A locator.riftbound.uvsgames.com oldal természetesen nem módosul

---

## Known limitations / accepted trade-offs
- Ha a forrásoldal API-ja változik, a fetcher/scraper frissítést igényel
- IMemoryCache: app újrainduláskor az adat elvész (következő frissítésig üres naptár)
- Playwright scraper esetén a headless böngésző erőforrásigényes; v1-ben elfogadott
- A 30 perces frissítési ciklus v1-ben fix; konfigurálhatóság v2

---

## Architecture

### Projektek
```
RiftboundCalendar.sln
├── RiftboundCalendar.Core          — domain entitások, interfészek (nincs külső függőség)
├── RiftboundCalendar.Infrastructure — fetcher, szűrő, cache, background service
├── RiftboundCalendar.Api           — ASP.NET Core Web API (Blazor WASM host)
└── RiftboundCalendar.Web           — Blazor WebAssembly frontend
```

### Fő komponensek

**Core:**
- `RiftboundEvent` — domain entitás (Id, Title, StartDate, EndDate, Location, Latitude, Longitude, Format, Url)
- `IEventRepository` — `Task<IReadOnlyList<RiftboundEvent>> GetEventsAsync(CancellationToken)`
- `IEventFetcher` — `Task<IReadOnlyList<RiftboundEvent>> FetchAllEventsAsync(CancellationToken)`

**Infrastructure:**
- `RiftboundApiClient` — HttpClient alapú fetcher (ha van API) VAGY `RiftboundPlaywrightScraper` — Playwright alapú scraper
- `HaversineFilter` — `IReadOnlyList<RiftboundEvent> Filter(IEnumerable<RiftboundEvent>, double lat, double lng, double radiusKm)`
- `EventCacheRepository` — IEventRepository implementáció IMemoryCache felett
- `EventRefreshBackgroundService` — IHostedService, 30 percenként hívja a fetchert és frissíti a cache-t

**Api:**
- `EventsController` — `GET /api/events` → `IReadOnlyList<RiftboundEventDto>`
- `RiftboundEventDto` — API DTO (Id, Title, StartDate, EndDate, LocationName, Format, Url)

**Web (Blazor WASM):**
- `CalendarPage.razor` — MudCalendar az eseményekkel
- `EventDetailDialog.razor` — MudDialog esemény részletekkel
- `EventApiClient` — HttpClient wrapper a `/api/events` híváshoz

### Adatfolyam
```
BackgroundService (30 perc)
  → IEventFetcher.FetchAllEventsAsync()
    → HaversineFilter.Filter(..., Budapest, 50km)
      → IMemoryCache frissítés

HTTP kérés → EventsController.GetEvents()
  → IEventRepository.GetEventsAsync()
    → IMemoryCache olvasás
      → RiftboundEventDto[] → JSON válasz

Blazor WASM → EventApiClient.GetEventsAsync()
  → CalendarPage renderelés → MudCalendar
    → kattintás → EventDetailDialog
```

### Környezeti változók / konfiguráció
| Kulcs | Típus | Default | Leírás |
|---|---|---|---|
| `Riftbound:BaseUrl` | string | `https://locator.riftbound.uvsgames.com` | Forrásoldal URL |
| `Riftbound:RefreshIntervalMinutes` | int | `30` | Frissítési ciklus |
| `Riftbound:Budapest:Latitude` | double | `47.4979` | Budapest lat |
| `Riftbound:Budapest:Longitude` | double | `19.0402` | Budapest lng |
| `Riftbound:RadiusKm` | double | `50` | Szűrési sugár km-ben |

---

## Tests

- **test_haversine_within_radius** (unit): Budapest–Győr (~100km) kívül esik, Budapest–Budaörs (~10km) belül
- **test_haversine_boundary** (unit): pontosan 50km határon lévő pont
- **test_haversine_zero_distance** (unit): Budapest–Budapest = 0km
- **test_event_cache_returns_stored_events** (unit): cache-be tett esemény visszaolvasható
- **test_event_cache_empty_before_first_refresh** (unit): frissítés előtt üres lista
- **test_background_service_triggers_fetch** (unit): BackgroundService meghívja a fetchert
- **test_events_controller_returns_200** (integration): GET /api/events → 200 JSON
- **test_events_controller_returns_empty_list** (integration): üres cache esetén üres tömb
- **test_fetcher_parses_event_fields** (integration): fetcher helyesen értelmezi a forrásadatot
- **test_filter_applied_in_repository** (integration): controller csak 50km-en belüli eseményeket ad
- **test_calendar_renders_events** (e2e): naptárban megjelenik az esemény
- **test_event_detail_dialog_opens** (e2e): kattintásra megnyílik a részlet popup

---

## Documentation update
- [ ] `README.md`, section: Projekt leírás + futtatás, path: `C:\Repos\riftbound-calendar\README.md`

---

## Task breakdown

### Phase 0 — API feltérképezés
> **Releasable**: amikor ismert a forrásoldal adatszerkezete; ez határozza meg a Phase 2 fetchert.

#### Task 0.1 — Forrásoldal DOM struktúrájának feltérképezése
- [x] **File**: `Plans/api-discovery-notes.md` (dokumentáció)
- **Depends on**: nothing
- **Description**:
  - **Ismert**: az oldal Next.js App Router + RSC (`_rsc=` query paraméter, `0:{...}` wire formátum) — nincs REST API
  - **Teendő**: DevTools → Elements tab → azonosítani az esemény kártya DOM struktúráját
  - Megkeresendők: esemény cím, dátum, helyszín, koordináták (data-attribútum vagy rejtett mező), formátum, link
  - Keresési selector az esemény listához (pl. `[data-testid="event-card"]` vagy CSS osztálynév)
  - Rögzíteni: `Plans/api-discovery-notes.md`-ben a pontos selectorokat és attribútum neveket
- **Releasable**: Playwright scraper selectorok ismertek, Task 2.4 megkezdhető
- **Tests (TDD)**: N/A (kutatási lépés)
- **Checkpoint**: N/A

---

### Phase 1 — Solution és projekt struktúra
> **Releasable**: minden Phase után; az alap solution buildelhető és tesztelhető.

#### Task 1.1 — Solution és projektek létrehozása
- [x] **File**: `C:\Repos\riftbound-calendar\RiftboundCalendar.slnx`
- **Depends on**: nothing
- **Description**:
  - `dotnet new sln -n RiftboundCalendar` → `.slnx` formátum (.NET 10)
  - `dotnet new classlib -n RiftboundCalendar.Core --framework net10.0`
  - `dotnet new classlib -n RiftboundCalendar.Infrastructure --framework net10.0`
  - `dotnet new webapi -n RiftboundCalendar.Api --framework net10.0`
  - `dotnet new blazorwasm -n RiftboundCalendar.Web --framework net10.0`
  - Solution-höz adás: `dotnet sln add **/*.csproj`
  - Referenciák: Infrastructure → Core; Api → Infrastructure, Core; Web → (standalone, HttpClient)
  - MudBlazor hozzáadása Web projekthez: `dotnet add RiftboundCalendar.Web package MudBlazor`
- **Releasable**: `dotnet build RiftboundCalendar.slnx` sikeresen lefut
- **Tests (TDD)**: N/A (scaffolding)
- **Checkpoint**: `dotnet build RiftboundCalendar.slnx`

#### Task 1.2 — Test projektek létrehozása
- [x] **File**: `RiftboundCalendar.Core.Tests/RiftboundCalendar.Core.Tests.csproj`
- **Depends on**: Task 1.1
- **Description**:
  - `dotnet new xunit -n RiftboundCalendar.Core.Tests --framework net10.0`
  - `dotnet new xunit -n RiftboundCalendar.Infrastructure.Tests --framework net10.0`
  - `dotnet new xunit -n RiftboundCalendar.Api.Tests --framework net10.0`
  - Referenciák: Core.Tests → Core; Infrastructure.Tests → Infrastructure, Core; Api.Tests → Api, Infrastructure, Core
  - NuGet: `Moq`, `FluentAssertions`, `Microsoft.AspNetCore.Mvc.Testing` (Api.Tests-hez)
  - Solution-höz adás
- **Releasable**: `dotnet test RiftboundCalendar.slnx` (3 teszt, 0 hiba — scaffold tesztek)
- **Tests (TDD)**: N/A (scaffolding)
- **Checkpoint**: `dotnet test RiftboundCalendar.slnx`

---

### Phase 2 — Core domain és Infrastructure
> **Releasable**: minden task után a unit tesztek futtathatók; a Phase végén a fetcher + szűrő önállóan tesztelhető.

#### Task 2.1 — RiftboundEvent domain entitás
- [x] **File**: `RiftboundCalendar.Core/Entities/RiftboundEvent.cs`
- **Depends on**: Task 1.1
- **Description**:
  - `public sealed record RiftboundEvent(string Id, string Title, DateTimeOffset StartDate, DateTimeOffset EndDate, string LocationName, double Latitude, double Longitude, string Format, Uri Url)`
  - Immutable record; Id kötelező és nem üres (ArgumentException konstruktorban)
  - StartDate nem lehet nagyobb mint EndDate (ArgumentException)
- **Releasable**: entitás példányosítható, tesztek futnak
- **Tests (TDD)** — `RiftboundCalendar.Core.Tests/Entities/RiftboundEventTests.cs`:
  - Unit: `test_valid_event_creates_successfully` — érvényes adatokkal példányosítható
  - Unit: `test_empty_id_throws_argument_exception` — üres Id → ArgumentException
  - Unit: `test_end_before_start_throws_argument_exception` — EndDate < StartDate → ArgumentException
- **Checkpoint**: `dotnet test RiftboundCalendar.Core.Tests`

#### Task 2.2 — IEventFetcher és IEventRepository interfészek
- [x] **File**: `RiftboundCalendar.Core/Interfaces/IEventFetcher.cs`, `IEventRepository.cs`
- **Depends on**: Task 2.1
- **Description**:
  - `IEventFetcher`: `Task<IReadOnlyList<RiftboundEvent>> FetchAllEventsAsync(CancellationToken cancellationToken = default)`
  - `IEventRepository`: `Task<IReadOnlyList<RiftboundEvent>> GetEventsAsync(CancellationToken cancellationToken = default)`
  - Mindkét interfész a Core layerben, külső függőség nélkül
- **Releasable**: interfészek elérhetők, Infrastructure és Api hivatkozhat rájuk
- **Tests (TDD)**: N/A (interfészek, viselkedésük az implementáción tesztelendő)
- **Checkpoint**: `dotnet build RiftboundCalendar.Core`

#### Task 2.3 — HaversineFilter
- [x] **File**: `RiftboundCalendar.Infrastructure/Filtering/HaversineFilter.cs`
- **Depends on**: Task 2.1
- **Description**:
  - `public static class HaversineFilter`
  - `public static IReadOnlyList<RiftboundEvent> Filter(IEnumerable<RiftboundEvent> events, double centerLat, double centerLng, double radiusKm)`
  - Haversine formula: `R = 6371.0` (Föld sugara km), eredmény km
  - Befoglalás: `distance <= radiusKm` (határon lévő pont bent van)
  - Üres lista esetén üres listát ad vissza
- **Releasable**: szűrő hívható és tesztelt
- **Tests (TDD)** — `RiftboundCalendar.Infrastructure.Tests/Filtering/HaversineFilterTests.cs`:
  - Unit: `test_budapest_to_budaors_within_50km` — Budaörs (~10km) belül van
  - Unit: `test_budapest_to_gyor_outside_50km` — Győr (~120km) kívül esik
  - Unit: `test_exact_boundary_point_is_included` — pontosan 50km → bent
  - Unit: `test_empty_list_returns_empty` — üres bemenet → üres kimenet
  - Unit: `test_zero_distance_same_point` — Budapest–Budapest = bent
- **Checkpoint**: `dotnet test RiftboundCalendar.Infrastructure.Tests --filter FullyQualifiedName~HaversineFilter`

#### Task 2.4 — RiftboundLocatorFetcher (korábban: RiftboundPlaywrightScraper)
- [x] **File**: `RiftboundCalendar.Infrastructure/Fetching/RiftboundLocatorFetcher.cs`
- **Depends on**: Task 0.1, Task 2.2
- **Description**:
  - Task 0.1 feltárta: az esemény adat az initial HTML-ben van (RSC stream, `self.__next_f.push(...)` tagek) — Playwright felesleges, HttpClient elegendő
  - Háttér-API: `https://api.riftbound.uvsgames.com/api/magic-events/` (DRF paginated JSON)
  - `public sealed class RiftboundLocatorFetcher : IEventFetcher`
  - Konstruktor: `(HttpClient httpClient, IOptions<RiftboundOptions> options, ILogger<RiftboundLocatorFetcher> logger)`
  - `FetchAllEventsAsync`: GET `BaseUrl` HTML → RSC chunk parse → `PaginatedResponseDto` → pagination via `next` URL → `RiftboundEvent[]`
  - RSC unescape: `JsonSerializer.Deserialize<string>('"' + escaped + '"')` (JSON string unescaping)
  - HTTP hiba esetén: logolás + üres lista (nem dob)
  - Geolokáció: a locator szerver IP-alapján szűr (Budapest IP → Budapest eseményeket ad)
- **Releasable**: fetcher manuálisan hívható
- **Tests (TDD)** — `RiftboundCalendar.Infrastructure.Tests/Fetching/RiftboundLocatorFetcherTests.cs`:
  - Unit: `FetchAllEventsAsync_ReturnsEmpty_WhenRscHasZeroResults`
  - Unit: `FetchAllEventsAsync_ReturnsMappedEvent_WhenRscHasOneEvent`
  - Unit: `FetchAllEventsAsync_UsesEventUrl_WhenNotNull`
  - Unit: `FetchAllEventsAsync_ReturnsEmpty_WhenHttpThrows`
  - Unit: `FetchAllEventsAsync_FetchesBothPages_WhenNextUrlPresent`
- **Checkpoint**: `dotnet test RiftboundCalendar.Infrastructure.Tests --filter FullyQualifiedName~Fetching`

#### Task 2.5 — RiftboundOptions konfiguráció
- [x] **File**: `RiftboundCalendar.Infrastructure/Configuration/RiftboundOptions.cs`
- **Depends on**: nothing
- **Description**:
  - `public sealed class RiftboundOptions`
  - Properties: `BaseUrl` (string), `RefreshIntervalMinutes` (int, default 30), `BudapestLatitude` (double, default 47.4979), `BudapestLongitude` (double, default 19.0402), `RadiusKm` (double, default 50.0)
  - Konfigurációs szekció neve: `"Riftbound"`
- **Releasable**: konfiguráció olvasható `appsettings.json`-ból
- **Tests (TDD)**:
  - Unit: `test_options_default_values` — default értékek helyesek
- **Checkpoint**: `dotnet test RiftboundCalendar.Infrastructure.Tests --filter FullyQualifiedName~RiftboundOptions`

#### Task 2.6 — EventCacheRepository
- [x] **File**: `RiftboundCalendar.Infrastructure/Caching/EventCacheRepository.cs`
- **Depends on**: Task 2.2
- **Description**:
  - `public sealed class EventCacheRepository : IEventRepository`
  - Konstruktor: `EventCacheRepository(IMemoryCache cache)`
  - Cache kulcs: `const string CacheKey = "riftbound_events"`
  - `GetEventsAsync`: cache-ből olvassa a listát; ha nincs bejegyzés, üres listát ad vissza
  - `UpdateCache(IReadOnlyList<RiftboundEvent> events)`: publikus metódus a BackgroundService-nek; nincs lejárat (explicit frissítés)
- **Releasable**: repository olvasható és írható, tesztek zöldek
- **Tests (TDD)** — `RiftboundCalendar.Infrastructure.Tests/Caching/EventCacheRepositoryTests.cs`:
  - Unit: `test_get_events_returns_empty_before_update` — frissítés előtt üres lista
  - Unit: `test_get_events_returns_stored_events_after_update` — UpdateCache után visszaolvassa
  - Unit: `test_update_cache_replaces_previous_events` — második UpdateCache felülírja az elsőt
- **Checkpoint**: `dotnet test RiftboundCalendar.Infrastructure.Tests --filter FullyQualifiedName~EventCacheRepository`

#### Task 2.7 — EventRefreshBackgroundService
- [x] **File**: `RiftboundCalendar.Infrastructure/BackgroundServices/EventRefreshBackgroundService.cs`
- **Depends on**: Task 2.3, Task 2.4, Task 2.5, Task 2.6
- **Description**:
  - `public sealed class EventRefreshBackgroundService : BackgroundService`
  - Konstruktor: `(IEventFetcher fetcher, EventCacheRepository cache, HaversineFilter filter, IOptions<RiftboundOptions> options, ILogger<EventRefreshBackgroundService> logger)`
  - `ExecuteAsync`: induláskor azonnal fut, majd `RefreshIntervalMinutes` percenként ismétli
  - Minden ciklusban: `FetchAllEventsAsync` → `HaversineFilter.Filter(Budapest, 50km)` → `cache.UpdateCache(...)`
  - CancellationToken tiszteletben tartása; exception esetén logolás, service nem áll le
- **Releasable**: service DI-ba regisztrálva fut az app indításakor
- **Tests (TDD)** — `RiftboundCalendar.Infrastructure.Tests/BackgroundServices/`:
  - Unit: `test_background_service_calls_fetcher_on_start` — mock fetcher, ExecuteAsync → FetchAllEventsAsync meghívva
  - Unit: `test_background_service_updates_cache_after_fetch` — fetch eredménye bekerül a cache-be (szűrés után)
  - Unit: `test_background_service_logs_and_continues_on_fetcher_exception` — fetcher dob → service nem dob, logol
- **Checkpoint**: `dotnet test RiftboundCalendar.Infrastructure.Tests --filter FullyQualifiedName~EventRefreshBackground`

---

### Phase 3 — API réteg
> **Releasable**: Phase végén a backend önállóan futtatható, GET /api/events tesztelhető Swagger-ből vagy curl-ből.

#### Task 3.1 — RiftboundEventDto
- [ ] **File**: `RiftboundCalendar.Api/Dtos/RiftboundEventDto.cs`
- **Depends on**: Task 2.1
- **Description**:
  - `public sealed record RiftboundEventDto(string Id, string Title, DateTimeOffset StartDate, DateTimeOffset EndDate, string LocationName, string Format, string Url)`
  - Koordináták nem kerülnek ki az API-n (szükségtelen a frontendnek)
  - Mapping metódus: `public static RiftboundEventDto FromDomain(RiftboundEvent e)`
- **Releasable**: DTO mappelhető, tesztek zöldek
- **Tests (TDD)** — `RiftboundCalendar.Api.Tests/Dtos/RiftboundEventDtoTests.cs`:
  - Unit: `test_from_domain_maps_all_fields_correctly` — minden mező helyesen leképzett
- **Checkpoint**: `dotnet test RiftboundCalendar.Api.Tests --filter FullyQualifiedName~RiftboundEventDto`

#### Task 3.2 — EventsController
- [ ] **File**: `RiftboundCalendar.Api/Controllers/EventsController.cs`
- **Depends on**: Task 2.2, Task 3.1
- **Description**:
  - `[ApiController] [Route("api/[controller]")]`
  - Konstruktor: `EventsController(IEventRepository repository)`
  - `[HttpGet] GetEvents(CancellationToken cancellationToken)` → `Task<ActionResult<IReadOnlyList<RiftboundEventDto>>>`
  - 200 OK + JSON lista; üres lista esetén is 200 OK (nem 404)
- **Releasable**: GET /api/events elérhető és JSON-t ad vissza
- **Tests (TDD)** — `RiftboundCalendar.Api.Tests/Controllers/EventsControllerTests.cs`:
  - Unit: `test_get_events_returns_200_with_dto_list` — mock repository, controller → 200 + helyes DTO lista
  - Unit: `test_get_events_returns_200_with_empty_list` — üres repository → 200 + `[]`
  - Integration: `test_get_events_endpoint_via_test_server` — WebApplicationFactory, GET /api/events → 200 JSON
- **Checkpoint**: `dotnet test RiftboundCalendar.Api.Tests --filter FullyQualifiedName~EventsController`

#### Task 3.3 — DI regisztráció és appsettings
- [ ] **File**: `RiftboundCalendar.Api/Program.cs`, `appsettings.json`
- **Depends on**: Task 2.5, Task 2.6, Task 2.7, Task 3.2
- **Description**:
  - `builder.Services.Configure<RiftboundOptions>(builder.Configuration.GetSection("Riftbound"))`
  - `builder.Services.AddMemoryCache()`
  - `builder.Services.AddSingleton<EventCacheRepository>()`
  - `builder.Services.AddSingleton<IEventRepository>(sp => sp.GetRequiredService<EventCacheRepository>())`
  - `builder.Services.AddHttpClient<IEventFetcher, RiftboundApiClient>()` VAGY `AddSingleton<IEventFetcher, RiftboundPlaywrightScraper>()`
  - `builder.Services.AddHostedService<EventRefreshBackgroundService>()`
  - CORS: `builder.Services.AddCors()` → allow Blazor WASM origin (development: `https://localhost:*`)
  - `appsettings.json`: Riftbound szekció default értékekkel
- **Releasable**: `dotnet run` az Api projektben elindul, Swagger UI elérhető, GET /api/events működik
- **Tests (TDD)**: N/A (infrastruktúra konfiguráció)
- **Checkpoint**: `dotnet build RiftboundCalendar.Api`

---

### Phase 4 — Blazor Frontend
> **Releasable**: Phase végén az app böngészőben futtatható és a naptár eseményeket jelenít meg.

#### Task 4.1 — EventApiClient (Blazor WASM)
- [ ] **File**: `RiftboundCalendar.Web/Services/EventApiClient.cs`
- **Depends on**: Task 3.2
- **Description**:
  - `public sealed class EventApiClient`
  - Konstruktor: `EventApiClient(HttpClient httpClient)`
  - `Task<IReadOnlyList<RiftboundEventDto>> GetEventsAsync(CancellationToken cancellationToken = default)`
  - GET `api/events`; deszeriálizálja a JSON választ `List<RiftboundEventDto>`-ba
  - HttpRequestException esetén logolás + üres lista (nem dobja tovább)
  - `RiftboundEventDto` record a Web projektben (megismételt, hogy ne legyen Core→Web függőség) VAGY shared DTO projekt (v1-ben egyszerűbb a megismétlés)
- **Releasable**: EventApiClient DI-ba regisztrálva, esemény lista lekérhető
- **Tests (TDD)**: N/A (Blazor WASM unit teszt infrastruktúra v1-ben kihagyva; e2e fedi)
- **Checkpoint**: `dotnet build RiftboundCalendar.Web`

#### Task 4.2 — CalendarPage (MudCalendar)
- [ ] **File**: `RiftboundCalendar.Web/Pages/Calendar.razor`
- **Depends on**: Task 4.1
- **Description**:
  - `@page "/"`
  - `OnInitializedAsync`: `EventApiClient.GetEventsAsync()` → `_events` lista
  - MudBlazor `<MudCalendar>` komponens az eseményekkel (hónap nézet)
  - Betöltés közben: `<MudProgressCircular>`
  - Hiba / üres állapot: "Jelenleg nincsenek események a közelben." üzenet
  - Magyar lokalizáció: `MudLocalizer` / `CultureInfo("hu-HU")`
- **Releasable**: a naptár betöltődik és eseményeket mutat a böngészőben
- **Tests (TDD)**:
  - E2E: `test_calendar_renders_events` — Playwright e2e: oldal betölt, naptárban esemény látható
- **Checkpoint**: manuális böngészős teszt + `dotnet build RiftboundCalendar.Web`

#### Task 4.3 — EventDetailDialog
- [ ] **File**: `RiftboundCalendar.Web/Components/EventDetailDialog.razor`
- **Depends on**: Task 4.2
- **Description**:
  - `MudDialog` komponens esemény részletekkel: cím, dátum/idő, helyszín, formátum, link a forrásra
  - Megnyitás: eseményre kattintva `CalendarPage`-ből `IDialogService.ShowAsync<EventDetailDialog>(...)`
  - "Megnyitás Riftbound oldalon" gomb: `target="_blank"` link
  - Bezárás: Cancel gomb vagy backdrop kattintás
- **Releasable**: kattintásra megnyílik a részlet dialog
- **Tests (TDD)**:
  - E2E: `test_event_detail_dialog_opens_on_click` — Playwright: eseményre kattint → dialog megjelenik a helyes adatokkal
- **Checkpoint**: manuális böngészős teszt

---

### Phase 5 — Integráció és polish
> **Releasable**: Phase végén az app production-ready állapotban van (0 warning, minden teszt zöld).

#### Task 5.1 — End-to-end integrációs teszt
- [ ] **File**: `RiftboundCalendar.Api.Tests/Integration/EventsEndToEndTests.cs`
- **Depends on**: Task 3.3, Task 2.7
- **Description**:
  - `WebApplicationFactory<Program>` alapú teszt
  - Ellenőrzi: BackgroundService elindult → cache feltöltődött (mock fetcher) → GET /api/events → szűrt, helyes DTO lista
  - Mock fetcher: visszaad 2 eseményt (1 Budapest közelében, 1 Berlinben) → csak a budapesti jelenik meg
- **Releasable**: bizalom a teljes stack működésében
- **Tests (TDD)**:
  - Integration: `test_only_budapest_nearby_events_returned_via_api`
  - Integration: `test_empty_response_when_fetcher_returns_no_events`
- **Checkpoint**: `dotnet test RiftboundCalendar.Api.Tests --filter FullyQualifiedName~EndToEnd`

#### Task 5.2 — README és futtatási útmutató
- [ ] **File**: `C:\Repos\riftbound-calendar\README.md`
- **Depends on**: Task 5.1
- **Description**:
  - Előfeltételek: .NET 9 SDK, `playwright install chromium`
  - Futtatás: `dotnet run --project RiftboundCalendar.Api`
  - Tesztek: `dotnet test RiftboundCalendar.sln`
  - Konfiguráció: appsettings.json mezők leírása
- **Releasable**: új fejlesztő el tudja indítani a projektet a README alapján
- **Tests (TDD)**: N/A
- **Checkpoint**: N/A

---

### Phase 6 — Fly.io Deployment (Docker)
> **Releasable**: az alkalmazás publikusan elérhető URL-en fut Fly.io-n.

#### Task 6.1 — Dockerfile
- [ ] **File**: `C:\Repos\riftbound-calendar\Dockerfile`
- **Depends on**: Task 5.2
- **Description**:
  - Multi-stage build: `build` stage (.NET 9 SDK), `publish` stage, `final` stage (ASP.NET runtime)
  - Stage 1 (`build`): `mcr.microsoft.com/dotnet/sdk:9.0` — `dotnet publish RiftboundCalendar.Api -c Release -o /app/publish`
  - Stage 2 (`final`): `mcr.microsoft.com/dotnet/aspnet:9.0` — csak a publish output másolódik át
  - Playwright: a `final` stage-ben Chromium telepítése (`playwright install --with-deps chromium`)
  - `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "RiftboundCalendar.Api.dll"]`
  - `.dockerignore`: kizárja a `bin/`, `obj/`, `.git/` mappákat
  - Lokális tesztelés: `docker build -t riftbound-calendar .` && `docker run -p 8080:8080 riftbound-calendar`
- **Releasable**: `docker run` lokálisan működik, az app elérhető `http://localhost:8080`-on
- **Tests (TDD)**: N/A (infrastruktúra)
- **Checkpoint**: `docker build -t riftbound-calendar . && docker run --rm -p 8080:8080 riftbound-calendar`

#### Task 6.2 — Fly.io konfiguráció és deploy
- [ ] **File**: `C:\Repos\riftbound-calendar\fly.toml`
- **Depends on**: Task 6.1
- **Description**:
  - Előfeltétel: `flyctl` CLI telepítése, `fly auth login`
  - `fly launch --no-deploy` — generálja a `fly.toml`-t (app neve, régió: `ams` Amsterdam, legközelebb Budapest-hez)
  - `fly.toml` beállítások: `internal_port = 8080`, `auto_stop_machines = true`, `min_machines_running = 0` (free tier)
  - `fly deploy` — buildeli a Docker image-et és feltolja Fly.io-ra
  - Secrets (ha szükséges): `fly secrets set RIFTBOUND__BASEURL=https://locator.riftbound.uvsgames.com`
  - Ellenőrzés: `fly status`, `fly logs`
- **Releasable**: az app publikus URL-en (`https://<app-name>.fly.dev`) elérhető
- **Tests (TDD)**: N/A (infrastruktúra)
- **Checkpoint**: `fly status` → running állapot, böngészőben megnyílik
