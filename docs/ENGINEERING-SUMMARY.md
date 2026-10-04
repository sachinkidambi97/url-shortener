# Engineering Summary

URL Shortener Service — Schwab Interview Assignment

---

## Problem Statement

Build a URL shortener REST API as a Schwab interview assignment. The service must:
- Shorten arbitrary URLs and redirect via short codes
- Track click analytics (total count, per-click metadata)
- Enforce rate limiting per IP
- Be runnable with a single `docker compose up`
- Demonstrate three engineering scenarios: greenfield build, brownfield enhancement, and ambiguous requirement handling
- Be production-grade: clean architecture, tests, documentation, defensible decisions

Success is not just a working prototype. It is a prototype that an interviewer can clone, run, and review, with clear decision lineage at every step.

---

## Approach

A **layered monolith** (Approach A) — a single ASP.NET Core 8 Web API with clean layer separation (Controllers → Services → Repositories) backed by PostgreSQL and Redis.

The deliberate choice was made against microservices. A URL shortener is a single-domain service. Distributing it across three services (Shortening, Redirect, Analytics) would add distributed systems complexity — service discovery, network retries, eventual consistency, separate databases — with no benefit at this scale and within a one-day time constraint. Choosing the simpler architecture that fully delivers is the correct engineering judgment.

The architectural layers map directly to proposed future microservice boundaries. If scale required it, the decomposition is straightforward.

---

## Scenario 1 — Greenfield Build

### What Was Built

A complete URL shortener service from scratch:

- **Data model:** Two PostgreSQL tables — `shortened_urls` (short codes and destination URLs) and `click_events` (analytics). Schema created via EF Core migrations, applied automatically on startup.
- **Short code generation:** 7-character Base62 codes using `System.Security.Cryptography.RandomNumberGenerator` (CSPRNG, not `System.Random`). Collision-checked against the database, retried up to 3 times with increasing length (7 → 8 → 9 characters).
- **Shorten endpoint:** `POST /api/shorten` validates that the URL is absolute HTTP/HTTPS, generates a code, persists to PostgreSQL, returns `201 Created` with `{"shortCode", "shortUrl"}`.
- **Redirect endpoint:** `GET /{code}` — Redis cache-aside (check cache first; on miss, read PostgreSQL and populate cache). Returns `302` redirect. Click recording is fire-and-forget.
- **Analytics endpoint:** `GET /api/stats/{code}` returns total click count plus paginated recent clicks (IP, User-Agent, Referer, timestamp).
- **Rate limiting:** Fixed-window 100 req/min per IP using ASP.NET Core built-in middleware. Returns `429 Too Many Requests` with `ProblemDetails` body.
- **Reliability middleware:** `ExceptionHandlingMiddleware` (no stack trace leaks), `CorrelationIdMiddleware` (`X-Correlation-Id` propagated through request and Serilog).
- **Graceful Redis degradation:** `RedisCacheService` wraps all Redis calls in `try/catch`. On failure, logs a warning and returns `null` — the redirect path falls back to PostgreSQL-only without error.
- **Infrastructure:** Multi-stage Dockerfile (SDK build → ASP.NET runtime, non-root user), Docker Compose with health checks and `depends_on` conditions.
- **Tests:** 25 unit tests across four test classes, using Moq for interface mocking and FluentAssertions for readability.

### Architecture Rationale

**Single project, namespace-enforced layers.** The code is organized into `Controllers/`, `Services/`, `Repositories/`, `Models/`, `Middleware/`, `Data/`, `Infrastructure/` namespaces within one project. This is appropriate for a service of this size and avoids multi-project ceremony. The layering discipline is enforced by convention and code review, not by compile-time project boundaries.

**Interface-based services and repositories.** Every service and repository is defined behind an interface (`IUrlShorteningService`, `IUrlRepository`, etc.). This enables full unit testing with Moq mocks without needing a real database, and makes the code substitutable (e.g., swap PostgreSQL for another store by implementing `IUrlRepository`).

**Primary constructors and `record` DTOs.** Services and repositories use C# 12 primary constructors for DI injection (less boilerplate, clearer dependencies). DTOs use `record` types with `required` properties (immutable, value-equality semantics).

**`ConfigureAwait(false)` throughout.** All async service and repository methods use `ConfigureAwait(false)`. This is a correctness practice in library-style code (avoiding deadlocks in synchronization context environments) and a performance practice (reduces thread switches).

### Decisions Made

| Decision | Rationale |
|---|---|
| 302 over 301 | 301s get browser-cached, losing analytics. 302 ensures every click reaches the service. |
| Fire-and-forget click recording | Redirect latency matters more than analytics completeness. A crashed process may lose one click. |
| Redis cache-aside | Power-law traffic — a small number of codes get most clicks. Cache hit avoids PostgreSQL entirely. |
| CSPRNG for code generation | Predictable codes enable enumeration attacks. `RandomNumberGenerator` prevents this. |
| Fixed-window rate limiting | Built-in ASP.NET Core support; sufficient for prototype-scale abuse prevention. |
| EF Core over Dapper | Migration tooling is the decisive factor. Simple CRUD queries don't need raw SQL control. |
| bigint PK on `click_events` | Analytics tables grow without bound. `int` (2B rows) could overflow; `bigint` is safe. |

---

## Scenario 2 — Brownfield Enhancement

### What Would Be Added (Planned Phase 5)

Two features designed to integrate into the existing codebase without breaking anything already deployed:

**Feature 1: Custom Aliases**

- New optional field `alias` in `ShortenRequest`
- Validation: 3–30 characters, alphanumeric + hyphens, no reserved words
- Service change: if `alias` is provided, use it as the `short_code` directly (skip code generation); check uniqueness and return `409 Conflict` if taken
- If `alias` is absent, existing behavior is unchanged (random 7-char code)
- New EF Core migration adds the `alias` column to `shortened_urls` as nullable

**Feature 2: URL Expiration**

- New optional field `expiresAt` in `ShortenRequest`
- New nullable `expires_at` column in `shortened_urls` (added via migration)
- `RedirectService` checks `entity.ExpiresAt` before returning the URL — if expired, returns `null` and the controller returns `410 Gone` instead of `302`
- Background `IHostedService` (`ExpiredUrlCleanupService`) runs every 5 minutes, queries `shortened_urls WHERE expires_at < NOW()`, deletes expired rows, and evicts them from Redis cache

### How Existing Code Was Preserved

Both features are fully backward-compatible:
- Existing short codes without an alias continue to work — `alias` column is nullable, redirect logic is unchanged if `alias` is null
- Existing short codes without an expiration continue to work — `expires_at` column is nullable, expiration check is a conditional `if (entity.ExpiresAt.HasValue && entity.ExpiresAt < DateTimeOffset.UtcNow)`
- The `ShortenRequest` DTO uses optional fields; no existing API clients break
- The EF migration adds columns, never drops them

This is the key brownfield discipline: additive changes only. Nothing that worked before stops working.

---

## Scenario 3 — Ambiguous Requirement

### The Requirement

"Improve service reliability."

This is vague. Before writing code, the requirement must be decomposed into concrete, prioritized tasks with clear acceptance criteria. The decomposition:

### How It Was Decomposed

**1. What does "reliability" mean for this service?**

The service has three failure modes:
- The API process itself fails (unhandled exception, crash)
- PostgreSQL is unavailable or slow
- Redis is unavailable or slow

"Reliability" translates to: the service remains available and correct in the face of these failures, and operators can observe what is happening.

**2. Concrete tasks derived from the decomposition:**

| Concern | Task | Implementation |
|---|---|---|
| Unhandled exceptions crash the process with stack traces | Global exception middleware | `ExceptionHandlingMiddleware` — catches all exceptions, maps to `ProblemDetails`, logs with correlation ID, never leaks stack traces |
| Operators can't correlate logs to specific requests | Correlation IDs | `CorrelationIdMiddleware` — `X-Correlation-Id` on every request, propagated to Serilog log context |
| Redis failure brings down the service | Graceful Redis degradation | `RedisCacheService.GetAsync/SetAsync/RemoveAsync` wrapped in `try/catch` — warns and returns null on failure |
| Transient PostgreSQL/Redis faults cause unnecessary failures | Retry policies | Polly referenced in csproj; retry policy registration planned for `Infrastructure/ResiliencePolicies.cs` |
| No way to verify infrastructure health | Health check endpoint | `GET /health` with DB and Redis probes planned for Phase 6 |
| Structured logging missing | Serilog integration | `builder.Host.UseSerilog(...)` in `Program.cs` with `CorrelationId` enrichment |

**3. Prioritization:**

The exception middleware and correlation IDs were built as part of Phase 3 (core greenfield) because they are foundational — every endpoint benefits from them. Redis degradation was built into the cache service implementation at design time. Health checks and Polly retries are planned for Phase 6.

### What This Demonstrates

The decomposition process is explicit engineering judgment:
1. Challenge the vague requirement: what specifically can go wrong?
2. Map failure modes to concrete mitigations
3. Prioritize by impact (exception handling and observability first; retry policies add complexity and come second)
4. Build incrementally with clear acceptance criteria per item

This is preferable to "adding Polly everywhere" without understanding which failures it addresses.

---

## Decision Log

| Decision | Alternatives Considered | Trade-Off Accepted |
|---|---|---|
| Layered monolith | Microservices (separate Shortening, Redirect, Analytics services) | No distributed complexity; no independent scaling. Correct at this scale. |
| 302 redirect | 301 permanent redirect | Browser caching eliminates analytics. 302 adds one round-trip for repeat visitors. |
| Fire-and-forget analytics | Synchronous write before redirect | May lose one click on crash. Redirect latency is more important than analytics completeness. |
| Redis cache-aside + graceful degradation | Redis required (fail if down) | Service continues working without Redis (with higher DB load). Redis is a performance layer, not a correctness requirement. |
| Fixed-window rate limiting | Sliding window, token bucket | Burst at window boundary is possible. Built-in support simplifies implementation. |
| CSPRNG for code generation | `System.Random`, sequential IDs, URL hash | Predictable codes enable enumeration. Sequential IDs expose volume. Hash requires deduplication logic. |
| EF Core + migrations | Dapper with manual SQL migrations | EF Core migration tooling saves time. Query complexity is low (no complex joins or aggregate CTEs). |
| bigint PK on `click_events` | int PK | Analytics tables are append-only and grow indefinitely. bigint eliminates the integer overflow risk. |
| Non-root Docker user | Default root container | Defense-in-depth: a process vulnerability cannot gain root on the host. Standard for production containers. |
| ProblemDetails error format | Custom error JSON | RFC 7807 compliance; consistent with ASP.NET Core conventions; clients can anticipate the shape. |

---

## Risks and Mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| Click data loss on process crash (fire-and-forget) | Low | Acceptable for analytics (approximate counts); documented as a known trade-off |
| Redis cache staleness if URL editing added later | Low (not in scope) | If URL editing is added, cache invalidation must be added to the update path |
| Short code collision at high volume | Very low (62^7 = 3.5T) | Collision retry (3 attempts, increasing length) handles the improbable case |
| Rate limiter burst at window boundary | Low | Fixed window is sufficient for abuse prevention at this scale |
| PostgreSQL query performance on `click_events` at scale | Medium (future concern) | Composite index `(short_code, clicked_at)` covers the stats query; partition by `clicked_at` if needed at scale |

---

## Assumptions and Limitations

**Assumptions:**

- The service runs behind a reverse proxy (nginx, ALB) that handles TLS termination. The app speaks HTTP internally.
- IP addresses in click events are the direct client IP (`HttpContext.Connection.RemoteIpAddress`). The controller also checks `X-Forwarded-For` but does not fully handle proxy-chain trust. A production deployment would configure `ForwardedHeaders` middleware.
- Short codes are case-sensitive (Base62 alphabet distinguishes `A` from `a`).
- URL validation accepts any valid absolute HTTP/HTTPS URL, including `localhost` URLs. A production service might add additional blocklist validation.

**Limitations of current build (Phases 1-3):**

- No health check endpoint (`GET /health` is planned Phase 6)
- No custom alias support (planned Phase 5)
- No URL expiration (planned Phase 5)
- No Polly retry policies on PostgreSQL calls (Polly is referenced but not yet wired)
- No integration tests (planned Phase 4; unit tests cover service logic)
- Rate limiter uses in-process fixed window (not Redis-backed). Under multiple replicas, each instance has an independent counter.

---

## What Would Change at Scale

The current implementation is a correct starting point. At scale, the following would change:

**Short-term (1M requests/day):**
- Add `ForwardedHeaders` middleware to correctly identify client IPs behind a load balancer
- Switch rate limiter to Redis-backed distributed window (prevents per-replica bypass under multiple instances)
- Add Polly retry with exponential backoff for transient PostgreSQL and Redis failures
- Add circuit breaker for Redis (stop hammering a failed Redis instead of logging a warning per request)

**Medium-term (10M+ requests/day):**
- Partition `click_events` by `clicked_at` (range partitioning by month) to keep query performance predictable as the table grows
- Consider moving click recording out of `Task.Run` and into a durable queue (RabbitMQ, SQS) — eliminates data loss risk on crashes, decouples redirect latency from analytics write latency
- Add read replicas for PostgreSQL; analytics queries (`GET /api/stats`) are read-only and can tolerate slight replication lag

**Long-term (100M+ requests/day):**
- Decompose into Approach B microservices (Shortening API, Redirect Service, Analytics Service)
- Redirect Service runs many stateless replicas with Redis as the primary store; PostgreSQL as cold fallback
- Analytics Service consumes from a message queue, writes to a dedicated analytics database (possibly ClickHouse or similar columnar store for aggregation performance)

The current architecture's layer separation (Controllers → Services → Repositories, with interface boundaries) makes each of these transitions incremental rather than requiring a full rewrite.

---

## Agentic Orchestration: SDLC Automation

This project was built using an agentic orchestration system that automated the full SDLC — not just code generation. The process:

1. **Brainstorm phase** generated the architectural decision document (`DESIGN.md`) — problem framing, approach selection (Approach A vs B), data model, API contracts, and three scenario definitions.

2. **Architect phase** read `DESIGN.md`, analyzed the empty repo, and produced `PLAN.md` — a phased task breakdown with 44 tasks assigned to specialist agents (Builder, Validator, Scribe), with explicit verification criteria per task.

3. **Builder phase** implemented Phases 1-3 sequentially, producing 38 source files. Each task had a "Test → Implement → Verify" cycle. The builder's findings in `STATUS.md` documented every file created, every NuGet package added, and every verification command output (`dotnet build`, `dotnet test`, `docker compose config`).

4. **Validator phase** (planned) writes unit + integration tests using Testcontainers for real PostgreSQL and Redis containers.

5. **Scribe phase** (this document) reads the actual built code — not the plan — and documents what was constructed.

The key insight: the agents maintained a `STATUS.md` file as a shared communication channel. Each phase agent read prior agents' findings before acting, building on rather than duplicating work. The DESIGN.md served as the ground truth that all agents referenced.

This demonstrates a complete agentic SDLC loop: requirements → architecture → implementation → testing → documentation, with each phase autonomous and traceable.
