# Riftbound Eseménynaptár

Budapest 50 km-es körzetéből gyűjti össze a Riftbound Magic eseményeket, és naptárban jeleníti meg. Az adatok 30 percenként automatikusan frissülnek.

## Előfeltételek

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Gyors indítás

Két terminálra van szükség — az API és a Web frontend külön folyamat.

**1. API szerver** (terminal 1):
```bash
cd RiftboundCalendar.Api
dotnet run
```

**2. Web frontend** (terminal 2):
```bash
cd RiftboundCalendar.Web
dotnet run
```

Nyisd meg: [http://localhost:5253](http://localhost:5253)

### HTTPS fejlesztői módban

```bash
dotnet dev-certs https --trust
```

Ezután indítsd mindkét projektet `--launch-profile https` kapcsolóval. A Web `https://localhost:7254`-en, az API `https://localhost:7219`-en lesz elérhető.

## Tesztek futtatása

```bash
dotnet test
```

41 teszt fut: unit tesztek (Core, Infrastructure) és integrációs tesztek (API + teljes pipeline).

## Konfiguráció

`RiftboundCalendar.Api/appsettings.json` → `Riftbound` szekció:

| Kulcs | Default | Leírás |
|---|---|---|
| `BaseUrl` | `https://locator.riftbound.uvsgames.com` | Riftbound locator URL |
| `RefreshIntervalMinutes` | `30` | Automatikus frissítési ciklus |
| `BudapestLatitude` | `47.4979` | Szűrési középpont (szélesség) |
| `BudapestLongitude` | `19.0402` | Szűrési középpont (hosszúság) |
| `RadiusKm` | `50` | Szűrési sugár km-ben |

`RiftboundCalendar.Web/wwwroot/appsettings.json`:

| Kulcs | Default | Leírás |
|---|---|---|
| `ApiBaseUrl` | `http://localhost:5232` | API szerver alap URL-je |

## Architektúra

```
RiftboundCalendar.sln
├── RiftboundCalendar.Core           — domain entitások, interfészek (külső függőség nélkül)
├── RiftboundCalendar.Infrastructure — fetcher, Haversine-szűrő, cache, background service
├── RiftboundCalendar.Api            — ASP.NET Core Web API  (GET /api/events)
└── RiftboundCalendar.Web            — Blazor WebAssembly frontend (MudBlazor naptár)
```

### Adatfolyam

```
EventRefreshBackgroundService (30 percenként)
  → RiftboundLocatorFetcher   — RSC HTML-ből ID-k kinyerése, backend API per-event hívás
    → HaversineFilter         — 50 km-en kívüli események eltávolítása
      → EventCacheRepository  — IMemoryCache frissítés

GET /api/events
  → EventsController → EventCacheRepository → RiftboundEventDto[] → JSON

Blazor WASM
  → EventApiClient → MudCalendar → EventDetailDialog (eseményre kattintva)
```

### Ismert korlátok

- **Nincs perzisztencia**: app újraindításakor a cache ürül; az első frissítésig (≤ 30 mp) a naptár üres.
- **ISR cache**: a locator oldal CDN-je nem mindig adja vissza az eseményadatokat az első kérésre. A background service 4-szer próbálkozik újra (45 mp-es közökkel) mielőtt feladná.
- **Geoszűrés**: a locator szerver IP-alapon adja vissza a helyi eseményeket. Szerver-oldalú deployment esetén VPN vagy proxy szükséges, ha a szerver nem magyarországi IP-ről fut.
