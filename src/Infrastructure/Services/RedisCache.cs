using Application.Abstractions;
using Application.Abstractions.Caching;
using Microsoft.EntityFrameworkCore.Storage;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Services;

internal sealed class ShortLinkRedisCache(
    IConnectionMultiplexer connectionMultiplexer)
{
    private readonly StackExchange.Redis.IDatabase _database =
        connectionMultiplexer.GetDatabase();

    public async Task<ShortLinkCacheEntry?> GetAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var task = _database.StringGetAsync(
            GetKey(shortCode));

        var value = await task.WaitAsync(cancellationToken);

        if (!value.HasValue)
            return null;

        try
        {
            return JsonSerializer.Deserialize<ShortLinkCacheEntry>(
                value.ToString());
        }
        catch (JsonException)
        {
            // Corrupted / old cache payload.
            await RemoveAsync(shortCode, cancellationToken);
            return null;
        }
    }

    public async Task SetAsync(
        string shortCode,
        ShortLinkCacheEntry entry,
        CancellationToken cancellationToken = default)
    {
        var ttl = CalculateTtl(entry.ExpiresAt);

        if (ttl <= TimeSpan.Zero)
            return;

        var payload = JsonSerializer.Serialize(entry);

        var task = _database.StringSetAsync(
            key: GetKey(shortCode),
            value: payload,
            expiry: ttl,
            keepTtl: false,
            when: When.Always,
            flags: CommandFlags.None);

        await task.WaitAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        string shortCode,
        CancellationToken cancellationToken = default)
    {
        var task = _database.KeyDeleteAsync(
            GetKey(shortCode));

        await task.WaitAsync(cancellationToken);
    }

    private static TimeSpan CalculateTtl(
        DateTime? expiresAt)
    {
        TimeSpan ttl;

        if (expiresAt.HasValue)
        {
            ttl = expiresAt.Value - DateTime.UtcNow;

            if (ttl <= TimeSpan.Zero)
                return TimeSpan.Zero;
        }
        else
        {
            ttl = TimeSpan.FromHours(6);
        }

        return AddSafeJitter(ttl);
    }

    private static TimeSpan AddSafeJitter(
        TimeSpan ttl)
    {
        // 0-10% reduction.
        // Never causes cache to live beyond business expiration.
        var ratio = Random.Shared.NextDouble() * 0.10;

        var reduction = TimeSpan.FromTicks(
            (long)(ttl.Ticks * ratio));

        return ttl - reduction;
    }

    private static string GetKey(string shortCode)
        => $"short-link:{shortCode}";
}