using Application.Abstractions;
using Application.Abstractions.Caching;
using Domain.Aggregates.ShortLinks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Timeout;
using StackExchange.Redis;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Caching;

// A private fixture keeps deliberate Redis failures away from other tests.
public sealed class RedisCircuitBreakerTests : IAsyncLifetime
{
    private readonly ShortenerFactory _factory = new();
    public Task InitializeAsync() => _factory.InitializeAsync();
    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Slow_Redis_times_out_and_the_read_still_returns_the_database_entry()
    {
        var link = await _factory.SeedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        var pipeline = _factory.Services.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline("redis");
        var paused = await _factory.Redis.ExecAsync(["redis-cli", "CLIENT", "PAUSE", "3000", "ALL"]);
        Assert.Equal(0, paused.ExitCode);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => ReadRedisAsync(pipeline));
        var found = await cache.GetAsync(link.ShortCode);

        Assert.Equal(link.OriginalUrl, found?.OriginalUrl);
        Assert.Equal(1, _factory.Reads.Count);
    }

    [Fact]
    public async Task Redis_outage_opens_the_real_pipeline_preserves_database_fallback_and_recovers()
    {
        await _factory.ResetAsync();
        var link = await _factory.SeedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var cache = scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>();
        var pipeline = _factory.Services.GetRequiredService<ResiliencePipelineProvider<string>>().GetPipeline("redis");

        // Pausing keeps Docker's random host port stable through recovery.
        await _factory.Redis.PauseAsync();
        try
        {
            // Drive the production policy with actual Redis I/O failures.
            // No fabricated Redis exception, mock connection, or alternate policy.
            var opened = false;
            var failures = 0;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var failure = await Record.ExceptionAsync(() => ReadRedisAsync(pipeline));
                if (failure is BrokenCircuitException)
                {
                    opened = true;
                    break;
                }
                Assert.True(failure is RedisException or TimeoutRejectedException,
                    $"Expected a Redis failure or timeout, got {failure?.GetType().Name ?? "success"}.");
                failures++;
            }
            Assert.True(opened, "Redis failures should open the production circuit within the bounded attempts.");
            Assert.InRange(failures, 1, 9);

            var executed = false;
            await Assert.ThrowsAsync<BrokenCircuitException>(() => pipeline.ExecuteAsync(_ =>
            {
                executed = true;
                return ValueTask.CompletedTask;
            }).AsTask());
            Assert.False(executed);

            var found = await cache.GetAsync(link.ShortCode);
            Assert.Equal(link.OriginalUrl, found?.OriginalUrl);
            Assert.Equal(1, _factory.Reads.Count);
            Assert.Equal(found, await cache.GetAsync(link.ShortCode));
            Assert.Equal(1, _factory.Reads.Count);

            var memoryEntry = new ShortLinkCacheEntry("https://memory.example", RedirectType.Temporary, null);
            await cache.SetAsync("Outage1", memoryEntry);
            Assert.Equal(memoryEntry, await cache.GetAsync("Outage1"));
            await cache.RemoveAsync("Outage1");
            Assert.False(_factory.Services.GetRequiredService<IMemoryCache>().TryGetValue("short-link:l1:Outage1", out _));
        }
        finally
        {
            // Restore Redis even when an assertion fails.
            await _factory.Redis.UnpauseAsync();
        }

        // Keep the real 30-second break duration. Poll for a successful half-open
        // probe with a deadline rather than assuming a fixed reconnect delay.
        using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        while (true)
        {
            try { await ReadRedisAsync(pipeline, recoveryTimeout.Token); break; }
            catch (Exception ex) when (ex is BrokenCircuitException or RedisException or TimeoutRejectedException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), recoveryTimeout.Token);
            }
        }

        var recovered = new ShortLinkCacheEntry("https://recovered.example", RedirectType.Permanent, null);
        await cache.SetAsync("Recover", recovered);
        Assert.True(await _factory.RedisDatabase.KeyExistsAsync("short-link:Recover"));
        _factory.Services.GetRequiredService<IMemoryCache>().Remove("short-link:l1:Recover");
        _factory.Reads.Reset();
        Assert.Equal(recovered, await cache.GetAsync("Recover"));
        Assert.Equal(0, _factory.Reads.Count);
    }

    private Task ReadRedisAsync(ResiliencePipeline pipeline, CancellationToken cancellationToken = default)
    {
        return pipeline.ExecuteAsync(async token =>
        {
            await _factory.RedisDatabase.StringGetAsync("short-link:probe").WaitAsync(token);
        }, cancellationToken).AsTask();
    }
}
