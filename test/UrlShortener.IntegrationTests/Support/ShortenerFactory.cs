using Application.Abstractions.Caching;
using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Repositories;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace UrlShortener.IntegrationTests.Support;

public sealed class ShortenerFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("shortlinks_tests")
        .Build();

    public RedisContainer Redis { get; } = new RedisBuilder("redis:8.6.2").Build();
    public ShortLinkReadCounter Reads { get; } = new();
    public HttpClient Client { get; private set; } = null!;
    public IDatabase RedisDatabase => Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:shortlinks", Postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:redis", Redis.GetConnectionString() + ",abortConnect=false,allowAdmin=true");
        builder.ConfigureServices(services =>
            services.AddDbContext<ShortLinkDbContext>(options => options.AddInterceptors(Reads)));
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var startupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await Task.WhenAll(Postgres.StartAsync(startupTimeout.Token), Redis.StartAsync(startupTimeout.Token));
            Client = CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
            Client.Timeout = TimeSpan.FromSeconds(30);
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
            // Use checked-in migrations, not EnsureCreated or a substitute database provider.
            await db.Database.MigrateAsync(startupTimeout.Token);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task ResetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"ShortLinks\"");
        await RedisDatabase.ExecuteAsync("FLUSHDB");
        ((MemoryCache)Services.GetRequiredService<IMemoryCache>()).Compact(1);
        Reads.Reset();
    }

    public async Task<ShortLink> SeedAsync(string code = "Ab12xyz", DateTime? expiresAt = null,
        RedirectType type = RedirectType.Temporary, long? maxClicks = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var link = ShortLink.Create("https://example.com/path?q=a%20b", code, type, expiresAt, maxClicks);
        await repository.AddAsync(link);
        await work.SaveChangesAsync();
        // A seeded row starts cold so a read must exercise PostgreSQL and cache population.
        await scope.ServiceProvider.GetRequiredService<IShortLinkCacheService>().RemoveAsync(code);
        Reads.Reset();
        return link;
    }

    public override async ValueTask DisposeAsync()
    {
        try { await base.DisposeAsync(); }
        finally
        {
            try { await Redis.DisposeAsync(); }
            finally { await Postgres.DisposeAsync(); }
        }
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
