using Application.Abstractions;
using Application.Abstractions.Caching;
using AsyncKeyedLock;
using Domain.Aggregates.ShortLinks.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using StackExchange.Redis;

namespace Infrastructure.Services;

internal sealed class ShortLinkCacheService(
    ShortLinkRedisCache redisCache,
    IShortLinkRepository repository,
    IMemoryCache memoryCache,
    AsyncKeyedLocker<string> keyedLocker,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<ShortLinkCacheService> logger)
    : IShortLinkCacheService
{
    private readonly ResiliencePipeline _redisPipeline =
        pipelineProvider.GetPipeline("redis");

    public async Task<ShortLinkCacheEntry?> GetAsync(
      string shortCode,
      CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await _redisPipeline.ExecuteAsync(
                async ct => await redisCache.GetAsync(shortCode, ct),
                cancellationToken);

            if (cached is not null)
                return cached;
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(
                ex,
                "Redis circuit is open for {ShortCode}. Falling back to database.",
                shortCode);

            return await GetFromDatabaseAsync(
                shortCode,
                cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning(
                ex,
                "Redis timeout for {ShortCode}. Falling back to database.",
                shortCode);

            return await GetFromDatabaseAsync(
                shortCode,
                cancellationToken);
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Redis error for {ShortCode}. Falling back to database.",
                shortCode);

            return await GetFromDatabaseAsync(
                shortCode,
                cancellationToken);
        }

       //Key not found on redis
        return await GetFromDatabaseAsync(
            shortCode,
            cancellationToken);
    }

    public async Task SetAsync(
    string shortCode,
    ShortLinkCacheEntry entry,
    CancellationToken cancellationToken = default)
    {
        SetMemory(shortCode, entry);

        await TrySetRedisAsync(
            shortCode,
            entry,
            cancellationToken);
    }

    public async Task RemoveAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        memoryCache.Remove(GetMemoryKey(shortCode));

        await TryRemoveRedisAsync(
            shortCode,
            cancellationToken);
    }


    private async Task TryRemoveRedisAsync(
    string shortCode,
    CancellationToken cancellationToken)
    {
        try
        {
            await _redisPipeline.ExecuteAsync(
                async ct =>
                {
                    await redisCache.RemoveAsync(
                        shortCode,
                        ct);
                },
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(
                ex,
                "Redis circuit is open. Could not remove short code {ShortCode} from cache",
                shortCode);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning(
                ex,
                "Redis timeout while removing short code {ShortCode}",
                shortCode);
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Redis failure while removing short code {ShortCode}",
                shortCode);
        }
    }

   

    private async Task TrySetRedisAsync(
        string shortCode,
        ShortLinkCacheEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            await _redisPipeline.ExecuteAsync(
                async ct =>
                {
                    await redisCache.SetAsync(
                        shortCode,
                        entry,
                        ct);
                },
                cancellationToken);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(
                ex,
                "Redis circuit is open. Skipping cache population for {ShortCode}",
                shortCode);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning(
                ex,
                "Redis timeout while caching short code {ShortCode}",
                shortCode);
        }
        catch (RedisException ex)
        {
            logger.LogError(
                ex,
                "Redis failure while caching short code {ShortCode}",
                shortCode);
        }
    }

    private bool TryGetMemory(
        string shortCode,
        out ShortLinkCacheEntry? entry)
    {
        return memoryCache.TryGetValue(
            GetMemoryKey(shortCode),
            out entry);
    }

    private void SetMemory(
        string shortCode,
        ShortLinkCacheEntry entry)
    {
        var ttl = TimeSpan.FromSeconds(30);

        if (entry.ExpiresAt.HasValue)
        {
            var remaining =
                entry.ExpiresAt.Value - DateTime.UtcNow;

            if (remaining <= TimeSpan.Zero)
                return;

            if (remaining < ttl)
                ttl = remaining;
        }

        memoryCache.Set(
            GetMemoryKey(shortCode),
            entry,
            ttl);
    }
    private async Task<ShortLinkCacheEntry?> GetFromDatabaseAsync(
    string shortCode,
    CancellationToken cancellationToken)
    {
        // First check L1
        if (TryGetMemory(shortCode, out var memoryEntry))
            return memoryEntry;

        using (await keyedLocker.LockAsync(
                   shortCode,
                   cancellationToken))
        {
            // Double-check L1 after acquiring the lock
            if (TryGetMemory(shortCode, out memoryEntry))
                return memoryEntry;

            var shortLink = await repository.GetByShortCodeAsync(
                shortCode,
                cancellationToken);

            if (shortLink is null)
                return null;

            if (!shortLink.IsActive)
                return null;

            if (shortLink.ExpiresAt.HasValue &&
                shortLink.ExpiresAt.Value <= DateTime.UtcNow)
            {
                return null;
            }

            var entry = new ShortLinkCacheEntry(
                shortLink.OriginalUrl,
                shortLink.RedirectType,
                shortLink.ExpiresAt);

            SetMemory(shortCode, entry);

            // Redis may still be unavailable.
            // Best effort only.
            await TrySetRedisAsync(
                shortCode,
                entry,
                cancellationToken);

            return entry;
        }
    }
    private static string GetMemoryKey(string shortCode)
        => $"short-link:l1:{shortCode}";
}