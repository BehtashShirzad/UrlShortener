# URL Shortener

A production-minded URL shortening service built with ASP.NET Core, PostgreSQL, Redis, Polly, and .NET Aspire.

The project focuses on building a fast and resilient redirect flow while keeping PostgreSQL as the source of truth and Redis as an optional performance layer.

---

## Features

* Short URL generation
* Custom aliases
* `301` and `302` redirects
* Link expiration
* Active/inactive links
* Maximum click limits
* Redis caching
* PostgreSQL fallback
* Circuit breaker and timeout handling
* Cache stampede protection
* Short-lived L1 memory cache
* Domain events
* EF Core migrations
* .NET Aspire orchestration

---

## Tech Stack

* ASP.NET Core
* Entity Framework Core
* PostgreSQL
* Redis
* StackExchange.Redis
* Polly
* MediatR
* AsyncKeyedLock
* .NET Aspire
* Clean Architecture

---

## Architecture

The solution follows Clean Architecture:

```text
src/
├── Domain
├── Application
├── Infrastructure
├── Api
├── DatabaseMigrator
└── AppHost
```

The Domain layer stays independent from infrastructure concerns such as EF Core, Redis, and ASP.NET Core.

---

## Redirect Flow

The redirect path is designed around a cache-aside strategy.

```text
Request
   │
   ▼
Redis
   │
   ├── HIT ─────────────► Redirect
   │
   └── MISS / FAILURE
          │
          ▼
      Memory Cache
          │
          ├── HIT ──────► Redirect
          │
          └── MISS
                │
                ▼
          Per-key Lock
                │
                ▼
            PostgreSQL
                │
                ▼
          Populate Cache
                │
                ▼
             Redirect
```

PostgreSQL remains the source of truth.

Redis is used only as a performance optimization, so a Redis outage should not make the redirect endpoint unavailable.

---

## Redis Resilience

Redis operations are protected using Polly.

The resilience pipeline includes:

* Timeout
* Circuit Breaker
* Database fallback

When Redis is unavailable:

```text
Redis Timeout
Redis Error
Circuit Open
      │
      ▼
 PostgreSQL
      │
      ▼
  Redirect
```

The redirect path intentionally avoids retries to keep latency predictable.

---

## Cache Stampede Protection

Popular short links can generate many simultaneous requests after cache expiration.

To prevent all requests from hitting PostgreSQL at the same time, the application uses keyed asynchronous locking.

```text
Many requests for the same short code
              │
              ▼
         Redis MISS
              │
              ▼
       Lock(shortCode)
              │
              ▼
         PostgreSQL
              │
              ▼
        Populate Cache
              │
              ▼
     Other requests reuse it
```

Locks are isolated by short code, so unrelated links do not block each other.

---

## L1 + L2 Cache

The application uses two cache layers:

```text
L1 → In-memory cache
L2 → Redis
DB → PostgreSQL
```

The in-memory cache uses a very short TTL and mainly protects PostgreSQL during Redis outages.

Redis remains the main distributed cache.

---

## Cache Expiration

Redis entries never live longer than the actual short-link expiration.

A small random jitter is subtracted from the TTL to reduce synchronized cache expiration.

```csharp
private static TimeSpan AddSafeJitter(TimeSpan ttl)
{
    var ratio = Random.Shared.NextDouble() * 0.10;

    var reduction =
        TimeSpan.FromTicks(
            (long)(ttl.Ticks * ratio));

    return ttl - reduction;
}
```

The jitter only reduces the TTL and never extends it beyond the business expiration time.

---

## Domain Model

`ShortLink` is modeled as an aggregate root.

It contains the main business state, including:

* Original URL
* Short code
* Custom alias
* Redirect type
* Expiration
* Active state
* Maximum clicks

Short-code generation is handled through the domain layer and checked for collisions before creation.

The database should also enforce a unique constraint on short codes.

---

## Database Migrations

Database migrations are handled by a separate executable project.

```text
PostgreSQL
    │
    ▼
DatabaseMigrator
    │
    ▼
API
```

The migrator runs:

```csharp
await dbContext.Database.MigrateAsync();
```

and exits before the API starts.

---

## .NET Aspire

.NET Aspire is used to orchestrate the local development environment.

It starts and connects:

* PostgreSQL
* Redis
* Database Migrator
* ASP.NET Core API

Example AppHost:

```csharp
var builder =
    DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume();

var database = postgres
    .AddDatabase("shortlinks");

var redis = builder
    .AddRedis("redis");

var migrations = builder
    .AddProject<Projects.DatabaseMigrator>("migrations")
    .WithReference(database)
    .WaitFor(database);

var api = builder
    .AddProject<Projects.Api>("api")
    .WithReference(database)
    .WithReference(redis)
    .WaitForCompletion(migrations)
    .WaitFor(redis);

builder.Build().Run();
```

---

## Running Locally

### Requirements

* .NET SDK
* Docker Desktop, Docker Engine, or another Aspire-compatible container runtime

Clone the repository:

```bash
git clone <repository-url>
cd UrlShortener
```

Restore packages:

```bash
dotnet restore
```

Run the Aspire AppHost:

```bash
dotnet run --project src/AppHost
```

Aspire will start the required resources and expose the Aspire Dashboard.

---

## Reliability Principles

A few simple principles guide the design:

* PostgreSQL is the source of truth.
* Redis improves performance but is not required for correctness.
* Cache failures should not cause API failures.
* The redirect path should fail fast when Redis is unhealthy.
* Database traffic should be minimized.
* Hot links should not create cache stampedes.
* Infrastructure concerns should stay outside the domain layer.

---

## License

MIT
