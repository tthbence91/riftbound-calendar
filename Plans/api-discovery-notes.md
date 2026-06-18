# Riftbound Locator — API Discovery Notes

**Investigated:** 2026-06-18  
**Locator URL:** https://locator.riftbound.uvsgames.com

---

## Architecture

- Next.js App Router + React Server Components (RSC), hosted on Vercel
- Backend API: `https://api.riftbound.uvsgames.com` (Django REST Framework, paginated JSON)
- Event data is server-rendered via RSC streaming — no dedicated REST API endpoint for the locator UI itself

---

## Event Data Source

**Endpoint:** `GET https://api.riftbound.uvsgames.com/api/magic-events/?format=json`

**Pagination format (DRF-style):**
```json
{
  "page_size": 25,
  "total": 79427,
  "count": 79427,
  "current": 1,
  "next": "https://api.riftbound.uvsgames.com/api/magic-events/?format=json&page=2",
  "previous": null,
  "results": [...]
}
```

**Event object schema (top-level fields):**
```json
{
  "id": 545120,
  "name": "SEASON III - Rift European League",
  "game": 3,
  "start_datetime": "2026-05-19T15:00+0200",
  "end_datetime": "2026-06-30T17:47+0200",
  "url": null,
  "format_pretty": "Constructed",
  "store": {
    "id": 18368,
    "name": "La Colmena TCG",
    "full_address": "18, C. Magallanes, Santander, CB, 39007, ES",
    "country": "ES",
    "latitude": 43.4626503,
    "longitude": -3.814156,
    "timezone": "Europe/Madrid"
  },
  "convention": null,
  "settings": { ... }
}
```

**Game IDs confirmed:**
- `game: 3` = Riftbound (confirmed from "Rift European League", "Summoner Skirmish", "Nexus Nights" events)
- `game: 1` = Magic: The Gathering (confirmed from "Weekly Play (Constructed)")

---

## Filtering Limitations

The public API does **not** support query-parameter-based filtering:
- `game=3`, `latitude=...`, `longitude=...`, `radius_km=...`, `start_datetime_gte=...` — none of these filter the results; the total stays at 79K+ regardless
- The API returns ALL games, ALL dates, globally (no auth required)

**Location filtering** on the locator site happens server-side via Vercel's geolocation headers (`X-Vercel-IP-Latitude`, `X-Vercel-IP-Longitude`) which Vercel sets from the visitor's real IP — these cannot be spoofed by external callers.

---

## Scraper Strategy

Use **Playwright** to load the locator page from the host machine:

1. Navigate to `https://locator.riftbound.uvsgames.com`
2. Listen to all HTTP responses (`page.on("Response", ...)`)
3. The RSC stream delivers event data inline with the HTML, embedded as:
   ```
   self.__next_f.push([1,"34:{\"page_size\":25,\"count\":N,\"results\":[...]}\n"])
   ```
4. Parse the RSC chunks to extract the JSON object matching `{"page_size":...,"results":[...]}`
5. If `next != null`, follow pagination via the backend API URL in `next`
6. Map each result to `RiftboundEvent`

**Why Playwright works:**
- The host machine's real IP is used — if deployed in or near Budapest, the locator returns Budapest-area Riftbound events (game=3 is already filtered by the site)
- No need to reverse-engineer auth or server-side filtering

**Important limitation:**
- If the server runs far from Budapest (e.g., Fly.io region `ams`, ~1400 km away), the locator may return Amsterdam events instead. In that case, set the Fly.io region to `waw` (Warsaw, ~250 km) or route traffic through a Budapest IP.
- Our `HaversineFilter` provides a secondary local safety net regardless.

---

## Event URL Construction

The locator does not expose individual event detail pages (HTTP 404 for `/event/{id}`).  
`url` field in the API response is `null` for many events.

Fallback URL strategy: use `https://locator.riftbound.uvsgames.com` as the base URL when `url` is null.

---

## Field Mapping to RiftboundEvent

| `RiftboundEvent` field | Source |
|---|---|
| `Id` | `event.id.ToString()` |
| `StartDate` | `event.start_datetime` (DateTimeOffset) |
| `EndDate` | `event.end_datetime` (DateTimeOffset) |
| `Location.Name` | `event.store.name` |
| `Location.Latitude` | `event.store.latitude` |
| `Location.Longitude` | `event.store.longitude` |
| `Info.Title` | `event.name` |
| `Info.Format` | `event.format_pretty` |
| `Info.Url` | `event.url` ?? `BaseUrl` |
