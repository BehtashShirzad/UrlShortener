using Application.Abstractions.Contracts;
using Application.Features.ShortLinks.Commands.CreateShortLink;
using Application.Features.ShortLinks.Queries;
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Services;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Application;

public sealed class ApplicationFlowTests(ShortenerFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Domain_service_generates_a_code_checks_PostgreSQL_and_leaves_persistence_to_the_caller()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IShortLinkDomainService>();
        var expiresAt = DateTime.UtcNow.Date.AddDays(2);

        var link = await service.CreateShortLink("https://example.com", RedirectType.Permanent, expiresAt, 10);

        Assert.Matches("^[a-zA-Z0-9]{7}$", link.ShortCode);
        Assert.Equal("https://example.com", link.OriginalUrl);
        Assert.Equal(RedirectType.Permanent, link.RedirectType);
        Assert.Equal(expiresAt, link.ExpiresAt);
        Assert.Equal(10, link.MaxClicks);
        Assert.Single(link.DomainEvents);
        Assert.Equal(1, Factory.Reads.Count);
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(await db.ShortLinks.AnyAsync());
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{link.ShortCode}"));
    }

    [Fact]
    public async Task Mediator_create_saves_and_dispatches_the_creation_event_only_once()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var response = await sender.Send(new CreateShortLinkCommand("https://example.com", RedirectType.Temporary, null, 50));
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var tracked = Assert.Single(db.ChangeTracker.Entries<ShortLink>()).Entity;

        Assert.Equal(response.ShortenCode, tracked.ShortCode);
        Assert.Empty(tracked.DomainEvents);
        Assert.True(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{tracked.ShortCode}"));

        // If SaveChanges wrongly replays the event, it would recreate this key.
        await Factory.RedisDatabase.KeyDeleteAsync($"short-link:{tracked.ShortCode}");
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        Assert.False(await Factory.RedisDatabase.KeyExistsAsync($"short-link:{tracked.ShortCode}"));

        await using var verification = Factory.Services.CreateAsyncScope();
        Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<ShortLinkDbContext>().ShortLinks.CountAsync());
    }

    [Fact]
    public async Task Query_returns_url_and_redirect_type_from_the_real_cache_service()
    {
        var link = await Factory.SeedAsync(type: RedirectType.Permanent);
        await using var scope = Factory.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetShortenUrlQuery(link.ShortCode));

        Assert.Equal(link.OriginalUrl, result.Url);
        Assert.Equal(RedirectType.Permanent, result.RedirectType);
        Assert.Null(scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>().Database.CurrentTransaction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_expired_query_raises_the_current_application_error(bool expired)
    {
        if (expired) await Factory.SeedAsync(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        await using var scope = Factory.Services.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetShortenUrlQuery("Ab12xyz")));

        Assert.Equal("Invalid short link code.", exception.Message);
    }

    [Fact]
    public void Application_registers_one_transaction_behavior_per_request()
    {
        using var scope = Factory.Services.CreateScope();
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<CreateShortLinkCommand, CreateShortLinkCommandResponse>>();

        Assert.Single(behaviors.OfType<TransactionBehavior<CreateShortLinkCommand, CreateShortLinkCommandResponse>>());
    }
}
