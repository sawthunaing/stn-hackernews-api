# Hacker News Best Stories API

[![CI](https://github.com/sawthunaing/stn-hackernews-api/actions/workflows/ci.yml/badge.svg)](https://github.com/sawthunaing/stn-hackernews-api/actions/workflows/ci.yml)

An ASP.NET Core (.NET 8) Minimal API that returns the best `n` stories from the
[Hacker News API](https://github.com/HackerNews/API), sorted by score in descending order.
It is designed to serve large numbers of requests **without ever passing that load on to
Hacker News**: user requests are always served from a cache that a background service
refreshes on a fixed schedule.

```http
GET /api/stories/best?n=10
```

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
    "postedBy": "ismaildonmez",
    "time": "2019-10-12T13:43:01+00:00",
    "score": 1716,
    "commentCount": 572
  }
]
```

## How to run

### Option 1: .NET SDK (no other dependencies)

**Requirements:** [.NET 8 SDK](https://dotnet.microsoft.com/download) or later

```bash
git clone https://github.com/sawthunaing/stn-hackernews-api.git
cd stn-hackernews-api
dotnet run --project src/Stn.HackerNews.Api
```

| URL | Description |
|-----|-------------|
| http://localhost:5000/api/stories/best?n=10 | Best 10 stories |
| http://localhost:5000/swagger | Swagger UI |
| http://localhost:5000/health | Health check |

Sample requests are also in [`Stn.HackerNews.Api.http`](src/Stn.HackerNews.Api/Stn.HackerNews.Api.http)
(Visual Studio, or VS Code with the REST Client extension).

### Option 2: Docker Compose (API + Redis)

```bash
docker compose up --build
```

The API is then available at http://localhost:8080 (e.g. http://localhost:8080/swagger),
using Redis as a shared cache.

### Run the tests

```bash
dotnet test
```

39 tests (29 unit + 10 integration). None of them call the real Hacker News API.

Code coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool restore
dotnet reportgenerator -reports:"tests/**/TestResults/*/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:Html
```

## API

### `GET /api/stories/best?n={n}`

| Status | When |
|--------|------|
| `200 OK` | Array of up to `n` stories, highest score first |
| `400 Bad Request` | `n` is missing or less than 1 (ProblemDetails) |
| `429 Too Many Requests` | Client exceeded the rate limit; see `Retry-After` (ProblemDetails) |
| `503 Service Unavailable` | Hacker News is unreachable and nothing is cached yet (ProblemDetails) |

## How it avoids overloading Hacker News

```
                      ┌──────────────────────────────────────────────┐
HTTP request ──► Rate limiter ──► Endpoint ──► Handler ──► Cache (pre-sorted snapshot)
                 (per client)     (validates n)                  ▲
                                                                 │ writes
   BestStoriesRefreshService ──► BestStoriesRefresher ───────────┘
   (startup + every 5 min)              │
                                        ▼
                         HackerNewsClient (retries, timeouts, circuit breaker)
                                        │
                                        ▼
                                 Hacker News API
```

User requests only read from the cache. The refresher is the only component that calls
Hacker News, so **upstream calls depend on the refresh interval, not on traffic**:
about 201 calls every 5 minutes whether the API receives one request or a million.

1. **Pre-sorted snapshot.** All best stories (~200) are fetched, sorted by score and cached
   as one immutable snapshot. A request just takes the first `n` items: no locks, no upstream calls.
   (`beststories.json` is not ordered by score, so all stories must be fetched to find the top `n`.)
2. **Background refresh.** A hosted service warms the cache at startup and refreshes it every
   5 minutes with `PeriodicTimer`. Requests keep reading the previous snapshot during a refresh.
3. **Request coalescing.** If requests arrive before the cache is warm, a `SemaphoreSlim`
   ensures only one load reaches Hacker News; the others wait and reuse its result.
   Tested end to end: 200 concurrent HTTP requests → 1 Hacker News load.
4. **Outbound throttling.** Story details are fetched with `Parallel.ForEachAsync`,
   at most 10 at a time.
5. **Resilience.**
   - `AddStandardResilienceHandler`: retries with backoff, timeouts and a circuit breaker
   - A story that fails to load keeps its previous version
   - A failed refresh keeps the previous snapshot; the background service never crashes the host
   - After a failure, a 10-second cooldown stops waiting requests from retrying against a failing upstream

### Protecting this API

A per-client (IP) fixed-window rate limiter (100 requests per 10 seconds) applies to `/api/*`.
This protects **this API** from abuse; it is not what protects Hacker News. The limit is
generous because cached responses are cheap. `/health` and Swagger are not rate limited.

### Caching: memory or Redis

| `Caching:Provider` | Use for | Behaviour |
|--------------------|---------|-----------|
| `Memory` (default) | Single instance | `IMemoryCache`; no external dependency |
| `Redis` | Multiple instances | L1 in-process memory (30s) + L2 Redis shared by all instances |

With Redis, an instance skips its scheduled refresh if another instance refreshed within the
last half interval, so Hacker News isn't called once per instance. If Redis is unavailable,
each instance keeps serving the last snapshot it saw.

## Architecture

**Minimal API + Vertical Slice Architecture + lightweight CQRS** (query + handler, no mediator library).

```
src/Stn.HackerNews.Api/
├── Program.cs                          # composition root only
├── Features/
│   └── BestStories/                    # everything for this use case
│       ├── GetBestStories.cs           # query, handler and endpoint
│       ├── BestStoriesRefresher.cs     # the only component that calls Hacker News
│       ├── BestStoriesRefreshService.cs
│       ├── BestStoriesFeature.cs       # DI and endpoint registration
│       ├── BestStoriesSnapshot.cs
│       ├── BestStoriesOptions.cs
│       ├── StoriesUnavailableException.cs
│       └── StoryDto.cs
└── Infrastructure/                     # shared, feature-agnostic
    ├── HackerNews/                     # typed HttpClient + resilience
    ├── Caching/                        # ICacheStore: Memory or Redis
    └── RateLimiting/
tests/
├── Stn.HackerNews.UnitTests/
├── Stn.HackerNews.IntegrationTests/    # WebApplicationFactory, fake Hacker News
└── Shared/                             # FakeHackerNewsClient used by both
```

**Why this structure**

- **Minimal API**: a single read-only endpoint; controllers would add boilerplate without benefit.
- **Vertical slice**: everything for the use case lives together; a new feature (e.g. top stories) is a new folder.
- **Lightweight CQRS**: the endpoint handles HTTP concerns and the handler holds the logic,
  so the logic is testable without HTTP. There are no writes, so a mediator library or command
  side would be unnecessary ceremony.
- **No layered projects**: with no database and one use case, separate projects per layer would add structure without value.

## Configuration

All settings are in `appsettings.json` and can be overridden by environment variables
(e.g. `Caching__Provider=Redis`). Invalid values stop the app at startup.

```json
"HackerNews":   { "BaseUrl": "https://hacker-news.firebaseio.com/v0/", "MaxConcurrentRequests": 10 },
"BestStories":  { "RefreshInterval": "00:05:00", "FailureCooldown": "00:00:10" },
"Caching":      { "Provider": "Memory", "RedisConnectionString": "localhost:6379,abortConnect=false", "LocalCacheDuration": "00:00:30" },
"RateLimiting": { "PermitLimit": 100, "WindowSeconds": 10 }
```

## Assumptions

- `n` is required and must be at least 1. Hacker News returns about 200 best story IDs;
  if `n` is larger, all available stories are returned rather than an error.
- Data up to 5 minutes old is acceptable. Scores change slowly, and this is the trade-off
  that keeps the load on Hacker News constant.
- Stories without a URL (e.g. "Ask HN" text posts) return their Hacker News discussion page
  (`https://news.ycombinator.com/item?id={id}`) as `uri` instead of `null`.
- Deleted, dead and missing (`null`) items are skipped.
- `commentCount` is the item's `descendants` field (total comments), defaulting to 0.
- `time` is returned in ISO 8601 with a UTC offset, matching the example in the brief.
- Stories with equal scores are ordered most recent first, so the order is deterministic.

## Enhancements given more time

- **Upgrade to .NET 10 (LTS)**: .NET 8 support ends in November 2026.
- **`HybridCache`** (.NET 9+) to replace the custom L1/L2 cache store.
- **Distributed lock** (e.g. Redis `SET NX`) so exactly one instance refreshes per cycle;
  today two instances can occasionally refresh at the same moment.
- **Incremental refresh** using Hacker News' `updates.json` to re-fetch only changed items.
- **HTTP caching**: `Cache-Control`/`ETag` or output caching so clients and CDNs can cache responses.
- **Forwarded headers** so the rate limiter sees the real client IP behind a load balancer,
  and a **distributed rate limiter** (or API gateway) for a global limit across instances.
- **Observability**: OpenTelemetry metrics and tracing (cache age, refresh duration,
  upstream errors) and a health check that reports cache staleness and Redis connectivity.
- **Load testing** with k6 or NBomber to measure throughput and latency.
- **Contract test** against the live Hacker News API on a schedule (not on every CI run)
  to detect upstream format changes.