using Application.Abstractions;
using Application.Abstractions.Caching;
using Domain.Aggregates.ShortLinks;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Caching;

public sealed class ShortLinkCacheTests(ShortenerFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Cold_read_loads_PostgreSQL_and_populates_both_caches()
    {
        var link = await Factory.SeedAsync();
        await using var scope = Factory.Services.CreateAsyncScope();

        var entry = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync(link.ShortCode);

        Assert.Equal(new ShortLinkCacheEntry(link.OriginalUrl, link.RedirectType, link.ExpiresAt), entry);
        Assert.Equal(1, Factory.Reads.Count);
        var json = await Factory.RedisDatabase.StringGetAsync($"short-link:{link.ShortCode}");
        Assert.Equal(entry, JsonSerializer.Deserialize<ShortLinkCacheEntry>(json.ToString()));
        Assert.Equal(entry, Factory.Services.GetRequiredService<IMemoryCache>().Get<ShortLinkCacheEntry>($"short-link:l1:{link.ShortCode}"));
    }

    [Fact]
    public async Task Redis_hit_returns_the_entry_without_reading_PostgreSQL()
    {
        var entry = new ShortLinkCacheEntry("https://redis.example", RedirectType.Permanent, null);
        await Factory.RedisDatabase.StringSetAsync("short-link:Redis01", JsonSerializer.Serialize(entry), TimeSpan.FromMinutes(1));
        await using var scope = Factory.Services.CreateAsyncScope();

        var found = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync("Redis01");

        Assert.Equal(entry, found);
        Assert.Equal(0, Factory.Reads.Count);
    }

    [Fact]
    public async Task Redis_miss_uses_memory_without_reading_PostgreSQL()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        var entry = new ShortLinkCacheEntry("https://memory.example", RedirectType.Temporary, null);
        await cache.SetAsync("Memory1", entry);
        await Factory.RedisDatabase.KeyDeleteAsync("short-link:Memory1");

        Assert.Equal(entry, await cache.GetAsync("Memory1"));
        Assert.Equal(0, Factory.Reads.Count);
    }

    [Fact]
    public async Task Redis_is_checked_before_memory()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        await cache.SetAsync("Order01", new ShortLinkCacheEntry("https://memory.example", RedirectType.Temporary, null));
        var redisEntry = new ShortLinkCacheEntry("https://redis.example", RedirectType.Permanent, null);
        await Factory.RedisDatabase.StringSetAsync("short-link:Order01", JsonSerializer.Serialize(redisEntry), TimeSpan.FromMinutes(1));

        Assert.Equal(redisEntry, await cache.GetAsync("Order01"));
        Assert.Equal(0, Factory.Reads.Count);
    }

    [Theory]
    [InlineData("{invalid json")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Unreadable_Redis_payload_falls_back_to_PostgreSQL_and_is_replaced(string payload)
    {
        var link = await Factory.SeedAsync();
        await Factory.RedisDatabase.StringSetAsync($"short-link:{link.ShortCode}", payload);
        await using var scope = Factory.Services.CreateAsyncScope();

        var entry = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync(link.ShortCode);

        Assert.NotNull(entry);
        Assert.Equal(link.OriginalUrl, entry.OriginalUrl);
        Assert.Equal(1, Factory.Reads.Count);
        var stored = await Factory.RedisDatabase.StringGetAsync($"short-link:{link.ShortCode}");
        Assert.Equal(entry, JsonSerializer.Deserialize<ShortLinkCacheEntry>(stored.ToString()));
    }

    [Fact]
    public async Task Corrupted_entry_without_a_database_row_is_removed()
    {
        await Factory.RedisDatabase.StringSetAsync("short-link:Missing", "{bad");
        await using var scope = Factory.Services.CreateAsyncScope();

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync("Missing"));
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync("short-link:Missing"));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("inactive")]
    public async Task Unavailable_link_returns_null_and_is_not_cached(string reason)
    {
        if (reason != "missing")
            await Factory.SeedAsync(expiresAt: reason == "expired" ? DateTime.UtcNow.AddMinutes(-1) : null);
        await using var scope = Factory.Services.CreateAsyncScope();
        if (reason == "inactive")
        {
            // No public deactivation operation exists; seed the persisted state directly.
            await scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>().ShortLinks
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsActive, false));
        }

        var entry = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync("Ab12xyz");

        Assert.Null(entry);
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync("short-link:Ab12xyz"));
        Assert.False(Factory.Services.GetRequiredService<IMemoryCache>().TryGetValue("short-link:l1:Ab12xyz", out _));
    }

    [Fact]
    public async Task Concurrent_cold_reads_for_one_code_issue_one_database_lookup()
    {
        var link = await Factory.SeedAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, 24).Select(async _ =>
        {
            // Each simulated request owns its DbContext, as it does in ASP.NET Core.
            await using var scope = Factory.Services.CreateAsyncScope();
            var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
            await start.Task;
            return await cache.GetAsync(link.ShortCode);
        }).ToArray();

        start.SetResult();
        var entries = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.All(entries, entry => Assert.Equal(link.OriginalUrl, entry?.OriginalUrl));
        Assert.Equal(1, Factory.Reads.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Redis_TTL_has_downward_jitter_and_never_exceeds_business_expiration(bool expiring)
    {
        var lifetime = expiring ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(6);
        DateTime? expiresAt = expiring ? DateTime.UtcNow.Add(lifetime) : null;
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().SetAsync("Ttl1234",
            new ShortLinkCacheEntry("https://example.com", RedirectType.Temporary, expiresAt));

        var ttl = await Factory.RedisDatabase.KeyTimeToLiveAsync("short-link:Ttl1234");

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, lifetime * 0.90 - TimeSpan.FromSeconds(5), lifetime);
    }

    [Fact]
    public async Task Already_expired_entry_is_not_written_to_either_cache()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().SetAsync("Expired",
            new ShortLinkCacheEntry("https://example.com", RedirectType.Temporary, DateTime.UtcNow.AddMinutes(-1)));

        Assert.False(await Factory.RedisDatabase.KeyExistsAsync("short-link:Expired"));
        Assert.False(Factory.Services.GetRequiredService<IMemoryCache>().TryGetValue("short-link:l1:Expired", out _));
    }

    [Fact]
    public async Task Memory_and_Redis_stop_serving_a_link_after_its_business_expiration()
    {
        var expiresAt = DateTime.UtcNow.AddSeconds(2);
        var link = await Factory.SeedAsync(expiresAt: expiresAt);
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        Assert.NotNull(await cache.GetAsync(link.ShortCode));

        var remaining = expiresAt - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining + TimeSpan.FromMilliseconds(100));

        Assert.Null(await cache.GetAsync(link.ShortCode));
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{link.ShortCode}"));
        Assert.False(Factory.Services.GetRequiredService<IMemoryCache>().TryGetValue($"short-link:l1:{link.ShortCode}", out _));
    }

    [Fact]
    public async Task Expired_Redis_payload_with_a_remaining_TTL_is_rejected_and_evicted()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(-1);
        var link = await Factory.SeedAsync(expiresAt: expiresAt);
        var staleEntry = new ShortLinkCacheEntry(link.OriginalUrl, link.RedirectType, expiresAt);
        await Factory.RedisDatabase.StringSetAsync($"short-link:{link.ShortCode}",
            JsonSerializer.Serialize(staleEntry), TimeSpan.FromMinutes(10));
        await using var scope = Factory.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync(link.ShortCode);

        Assert.Null(result);
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{link.ShortCode}"));
        Assert.Equal(1, Factory.Reads.Count);
    }

    [Fact]
    public async Task Redis_wrong_type_error_falls_back_to_PostgreSQL_and_replaces_the_bad_value()
    {
        var link = await Factory.SeedAsync();
        await Factory.RedisDatabase.ListRightPushAsync($"short-link:{link.ShortCode}", "wrong-type");
        await using var scope = Factory.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync(link.ShortCode);

        Assert.Equal(link.OriginalUrl, result?.OriginalUrl);
        Assert.Equal(1, Factory.Reads.Count);
        var repaired = await Factory.RedisDatabase.StringGetAsync($"short-link:{link.ShortCode}");
        Assert.Equal(result, JsonSerializer.Deserialize<ShortLinkCacheEntry>(repaired.ToString()));
    }

    [Fact]
    public async Task Remove_evicts_both_caches_and_the_next_read_reloads_PostgreSQL()
    {
        var link = await Factory.SeedAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        await cache.GetAsync(link.ShortCode);

        await cache.RemoveAsync(link.ShortCode);
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{link.ShortCode}"));
        Assert.False(Factory.Services.GetRequiredService<IMemoryCache>().TryGetValue($"short-link:l1:{link.ShortCode}", out _));
        Factory.Reads.Reset();

        Assert.NotNull(await cache.GetAsync(link.ShortCode));
        Assert.Equal(1, Factory.Reads.Count);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_instead_of_becoming_a_database_fallback()
    {
        await Factory.SeedAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().GetAsync("Ab12xyz", cancellation.Token));

        Assert.Equal(0, Factory.Reads.Count);
    }
}
