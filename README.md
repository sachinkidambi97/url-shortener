# URL Shortener Service

A production-grade URL shortener REST API built with .NET 8 (ASP.NET Core), PostgreSQL, and Redis. This project was designed as a Schwab interview assignment demonstrating three real-world engineering scenarios:

- **Greenfield build** — clean architecture from scratch, defensible decisions, complete delivery
- **Brownfield enhancement** — adding features to an existing codebase with full backward compatibility
- **Ambiguous requirement handling** — decomposing "improve reliability" into concrete, deliverable work

The service shortens URLs, redirects via short codes, tracks click analytics, enforces rate limiting, and includes production reliability features (graceful degradation, structured logging, correlation IDs, global exception handling).

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full architecture overview and [docs/ENGINEERING-SUMMARY.md](docs/ENGINEERING-SUMMARY.md) for the interview-ready engineering summary.

---

## Prerequisites

| Requirement | Version | Purpose |
|---|---|---|
| Docker Desktop | 4.x+ | Runs PostgreSQL, Redis, and the app |
| .NET 8 SDK | 8.0+ | Local development and running tests |
| Git | any | Cloning the repo |

Docker Desktop must be running before any `docker compose` commands.

---

## Quick Start (Docker)

Clone and run everything with one command:

```bash
git clone <repo-url>
cd url-shortener
docker compose up --build
```

The service will be available at `http://localhost:8080`.

Docker Compose starts three services in dependency order:

1. `postgres` — PostgreSQL 16, health-checked before the app starts
2. `redis` — Redis 7 Alpine, health-checked before the app starts
3. `app` — the .NET 8 API, auto-migrates the database on startup

To run in detached mode:

```bash
docker compose up --build -d
```

To stop and remove containers:

```bash
docker compose down
```

To stop and remove containers AND volumes (wipes database data):

```bash
docker compose down -v
```

---

## Local Development (without Docker)

You still need PostgreSQL 16 and Redis 7 running locally (or via Docker for just the dependencies):

```bash
# Start only the infrastructure services
docker compose up postgres redis -d
```

Then run the application with the .NET SDK:

```bash
cd UrlShortener.Api
dotnet run
```

The development profile uses `http://localhost:5000`. Connection strings default to `localhost:5432` and `localhost:6379` via `appsettings.json`.

For hot reload during development:

```bash
dotnet watch run
```

### Configuration

Configuration is in `UrlShortener.Api/appsettings.json`. Override any value with environment variables using double-underscore notation:

```bash
ConnectionStrings__DefaultConnection="Host=myhost;..."
ConnectionStrings__Redis="myredis:6379"
Cache__DefaultTtlHours=2.0
Cache__KeyPrefix="url:"
```

---

## Running Tests

Unit tests run without any infrastructure — no Docker required:

```bash
dotnet test UrlShortener.sln
```

Expected output:

```
Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25
```

Test suites:

| Suite | File | Tests |
|---|---|---|
| ShortCodeGenerator | `UrlShortener.Tests/Unit/ShortCodeGeneratorTests.cs` | 6 |
| UrlShorteningService | `UrlShortener.Tests/Unit/UrlShorteningServiceTests.cs` | 8 |
| RedirectService | `UrlShortener.Tests/Unit/RedirectServiceTests.cs` | 4 |
| AnalyticsService | `UrlShortener.Tests/Unit/AnalyticsServiceTests.cs` | 5 |

---

## API Usage

Base URL: `http://localhost:8080` (Docker) or `http://localhost:5000` (local).

### Shorten a URL

```bash
curl -X POST http://localhost:8080/api/shorten \
  -H "Content-Type: application/json" \
  -d '{"url": "https://www.example.com/some/very/long/path?with=query&params=here"}'
```

Response `201 Created`:

```json
{
  "shortCode": "aB3xY7z",
  "shortUrl": "http://localhost:8080/aB3xY7z"
}
```

Error response `400 Bad Request` (invalid URL):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid URL: 'not-a-url'. Must be an absolute http or https URL."
}
```

Validation rules:
- Must be an absolute URL
- Scheme must be `http` or `https` (not `ftp`, `file`, etc.)

### Redirect via Short Code

```bash
# Follow the redirect automatically
curl -L http://localhost:8080/aB3xY7z

# Inspect the redirect without following it
curl -v http://localhost:8080/aB3xY7z
```

Response `302 Found`:

```
HTTP/1.1 302 Found
Location: https://www.example.com/some/very/long/path?with=query&params=here
```

The response also includes `X-Correlation-Id` on every request.

Error response `404 Not Found` (unknown code):

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Short code 'aB3xY7z' not found."
}
```

### View Click Analytics

```bash
curl http://localhost:8080/api/stats/aB3xY7z
```

Response `200 OK`:

```json
{
  "shortCode": "aB3xY7z",
  "originalUrl": "https://www.example.com/some/very/long/path?with=query&params=here",
  "totalClicks": 3,
  "clicks": [
    {
      "clickedAt": "2026-10-04T18:30:00+00:00",
      "referrer": "https://google.com",
      "userAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)...",
      "ipAddress": "1.2.3.4"
    },
    {
      "clickedAt": "2026-10-04T18:25:00+00:00",
      "referrer": null,
      "userAgent": "curl/8.1.2",
      "ipAddress": "5.6.7.8"
    }
  ]
}
```

The `clicks` array returns the most recent 100 clicks, ordered newest-first. Pagination is supported:

```bash
# Page 2, 50 clicks per page
curl "http://localhost:8080/api/stats/aB3xY7z?page=2&pageSize=50"
```

### Health Check

```bash
curl http://localhost:8080/health
```

The health endpoint is not yet implemented in the current build (planned for Phase 6). The service starts and responds at the three endpoints above.

### Rate Limiting

All endpoints enforce a fixed-window limit of **100 requests per minute per IP address**.

When exceeded, the response is `429 Too Many Requests`:

```json
{
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please try again later."
}
```

### Swagger UI (Development only)

When running in development mode, Swagger UI is available at:

```
http://localhost:5000/swagger
```

---

## Project Structure

```
url-shortener/
├── UrlShortener.sln
├── docker-compose.yml
├── .dockerignore
├── docs/
│   ├── ARCHITECTURE.md          # Architecture overview, data model, design decisions
│   └── ENGINEERING-SUMMARY.md  # Interview-ready engineering summary
├── UrlShortener.Api/
│   ├── Dockerfile               # Multi-stage build, non-root user
│   ├── appsettings.json         # Production configuration
│   ├── appsettings.Development.json
│   ├── Program.cs               # DI registration, middleware pipeline, startup migration
│   ├── Controllers/
│   │   ├── UrlController.cs     # POST /api/shorten, GET /api/stats/{code}
│   │   └── RedirectController.cs# GET /{code} — 302 redirect
│   ├── Services/
│   │   ├── UrlShorteningService.cs  # URL validation, Base62 code generation, collision retry
│   │   ├── RedirectService.cs       # Cache-aside redirect with fire-and-forget analytics
│   │   ├── AnalyticsService.cs      # Click recording and stats aggregation
│   │   ├── RedisCacheService.cs     # Redis wrapper with graceful degradation
│   │   └── ShortCodeGenerator.cs   # Crypto-secure Base62 code generator
│   ├── Repositories/
│   │   ├── IUrlRepository.cs
│   │   ├── UrlRepository.cs        # EF Core PostgreSQL implementation
│   │   ├── IClickEventRepository.cs
│   │   └── ClickEventRepository.cs # EF Core PostgreSQL implementation
│   ├── Models/
│   │   ├── ShortenedUrl.cs         # Domain entity
│   │   ├── ClickEvent.cs           # Analytics entity
│   │   └── Dtos/
│   │       ├── ShortenRequest.cs
│   │       ├── ShortenResponse.cs
│   │       ├── StatsResponse.cs
│   │       └── ClickDto.cs
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/
│   │   │   ├── ShortenedUrlConfiguration.cs
│   │   │   └── ClickEventConfiguration.cs
│   │   └── Migrations/
│   │       └── 20261004145708_InitialCreate.cs
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs  # Global exception handler (no stack trace leaks)
│   │   └── CorrelationIdMiddleware.cs      # X-Correlation-Id header propagation
│   └── Infrastructure/
│       └── CacheOptions.cs         # Cache configuration (TTL, key prefix)
└── UrlShortener.Tests/
    ├── GlobalUsings.cs
    └── Unit/
        ├── ShortCodeGeneratorTests.cs
        ├── UrlShorteningServiceTests.cs
        ├── RedirectServiceTests.cs
        └── AnalyticsServiceTests.cs
```

---

## Technology Stack

| Component | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | 8.0 |
| Database | PostgreSQL | 16 |
| Cache | Redis | 7 |
| ORM | Entity Framework Core (Npgsql) | 8.0.11 |
| Redis client | StackExchange.Redis | 2.8.24 |
| Logging | Serilog | 9.0.0 |
| Resilience | Polly.Extensions.Http | 3.0.0 |
| API docs | Swashbuckle (Swagger) | 6.6.2 |
| Testing | xUnit + Moq + FluentAssertions | - |
| Containers | Docker / Docker Compose | - |
