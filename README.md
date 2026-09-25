# URL Shortener

A production-minded URL shortening service built with ASP.NET Core, PostgreSQL, Redis, Polly, and .NET Aspire.

The project focuses on building a fast and resilient redirect flow while keeping PostgreSQL as the source of truth.

Redis serves two separate purposes:

- Distributed caching for redirect performance
- Redis Streams for asynchronous click tracking

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
* Asynchronous click tracking
* Redis Streams
* Consumer groups
* Idempotent click processing
* Atomic click counters
* Multi-instance safe click processing

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

PostgreSQL remains the source of truth.

Redis caching is treated as a performance optimization, while Redis Streams are used for asynchronous click analytics.

---

## Redirect Flow

The redirect path is designed around a cache-aside strategy.

```text
Request
   │
   ▼
Redis
   │
   ├── HIT ─────────────► Resolve Link
   │
   └── MISS / FAILURE
          │
          ▼
      Memory Cache
          │
          ├── HIT ──────► Resolve Link
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
           Resolve Link
                │
                ▼
       Publish Click Event
                │
                ▼
             Redirect
```

A Redis cache failure should not prevent a valid short link from being resolved.

---

## Click Tracking Flow

Click counting is asynchronous and intentionally kept outside direct database writes on the critical redirect path.

Each successful short-link resolution publishes a click event to a Redis Stream.

```text
Request
   │
   ▼
Resolve Short Link
   │
   ▼
Publish Click Event
   │
   ▼
Redirect


Redis Stream
   │
   ▼
Consumer Group
   │
   ▼
Background Consumer
   │
   ▼
Idempotent Processing
   │
   ▼
Atomic PostgreSQL Increment
   │
   ▼
Commit
   │
   ▼
ACK
```

Each click message contains a unique event identifier:

```text
EventId
ShortLinkId
ClickedAt
```

This allows redirect processing and click-count persistence to remain decoupled.

---

## Redis Streams

Redis Streams are used for asynchronous click processing.

Unlike Redis Pub/Sub, stream entries remain available for later consumption instead of being delivered only to subscribers that are currently connected.

A shared consumer group allows multiple application instances to distribute click-processing work.

```text
Redis Stream
      │
      ▼
Consumer Group
   ├── API Instance A
   ├── API Instance B
   └── API Instance C
```

Consumers acknowledge messages only after database processing succeeds.

Redis Streams provide at-least-once delivery semantics, so consumers must be idempotent.

---

## Idempotent Click Processing

Redis Stream entries may be delivered more than once.

To prevent the same click from incrementing the counter multiple times, every click event contains a unique `EventId`.

Processed event identifiers are stored in PostgreSQL:

```text
ProcessedClickEvents
├── EventId      (Primary Key)
├── ShortLinkId
└── ProcessedAt
```

Processing happens inside a database transaction:

```text
BEGIN
   │
   ├── INSERT ProcessedClickEvent(EventId)
   │
   ├── UPDATE ShortLinks
   │      SET TotalClicks = TotalClicks + 1
   │
   └── COMMIT
          │
          ▼
     ACK Redis Message
```

The primary key on `EventId` acts as the idempotency constraint.

If the same Redis message is delivered again, inserting the same `EventId` fails and the click counter is not incremented again.

This produces effectively-once database effects on top of at-least-once message delivery.

---

## Atomic Click Counters

`TotalClicks` is updated directly in PostgreSQL using an atomic database operation.

Conceptually:

```sql
UPDATE "ShortLinks"
SET "TotalClicks" = "TotalClicks" + 1
WHERE "Id" = @shortLinkId;
```

The application does not load the aggregate, increment the value in memory, and save it back for click processing.

This prevents lost updates when multiple consumers process different click events concurrently.

---

## Redis Resilience

Redis cache operations are protected using Polly.

The resilience pipeline includes:

* Timeout
* Circuit Breaker
* Database fallback

When the Redis cache is unavailable:

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

Click-event publishing is also treated as non-critical to URL resolution: analytics failures should not make a valid redirect unavailable.

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

## Redirect Caching and Click Tracking

Permanent redirects such as HTTP `301` may be cached by browsers or intermediaries.

Once cached, later navigations may bypass the URL shortener entirely:

```text
First request:

Browser → Shortener → 301 → Destination


Later requests:

Browser ───────────────→ Destination
```

When this happens, the application cannot observe or count those later clicks.

For links where click-tracking accuracy matters, temporary redirects such as `302` should be preferred.

Redirect responses intended to remain observable may also include cache-prevention headers such as:

```text
Cache-Control: no-store, no-cache, must-revalidate
Pragma: no-cache
Expires: 0
```

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
* Total clicks

Short-code generation is handled through the domain layer and checked for collisions before creation.

The database also enforces uniqueness on short codes.

`TotalClicks` is persisted on the short link but is updated atomically by the click-processing infrastructure rather than through an in-memory aggregate mutation.

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

Aspire resource names are also used as connection-string names:

```text
ConnectionStrings:shortlinks
ConnectionStrings:redis
```

Using the same names in local configuration allows projects to run both through Aspire and independently when needed.

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
* Redis caching improves redirect performance but is not required for URL resolution.
* Cache failures should not cause redirect failures.
* Click analytics should not make valid redirects unavailable.
* Redis Streams provide at-least-once delivery.
* Click consumers must therefore be idempotent.
* Redis messages are acknowledged only after successful database processing.
* Click counters are incremented atomically in PostgreSQL.
* Multiple application instances can safely share the same click consumer group.
* The redirect path should fail fast when Redis caching is unhealthy.
* Database traffic should be minimized.
* Hot links should not create cache stampedes.
* Infrastructure concerns should stay outside the domain layer.

---

## License

MIT