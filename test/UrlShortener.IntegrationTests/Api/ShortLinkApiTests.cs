using System.Net;
using System.Net.Http.Json;
using System.Text;
using Application.Abstractions;
using Application.Features.ShortLinks.Commands.CreateShortLink;
using Domain.Aggregates.ShortLinks;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Api;

public sealed class ShortLinkApiTests(ShortenerFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData(RedirectType.Temporary, HttpStatusCode.Redirect)]
    [InlineData(RedirectType.Permanent, HttpStatusCode.MovedPermanently)]
    public async Task Create_persists_input_populates_Redis_and_returns_a_working_redirect(RedirectType type, HttpStatusCode status)
    {
        var expiresAt = DateTime.UtcNow.Date.AddDays(2);
        const string originalUrl = "https://example.com/path?q=a%20b&source=test#section";
        var response = await Factory.Client.PostAsJsonAsync("/api/v1/short-links", new
        {
            originalUrl, shortCode = "ignored", isActive = false, redirectType = type, expiresAt, maxClicks = 123
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateShortLinkCommandResponse>();
        Assert.NotNull(created);
        Assert.Matches("^[a-zA-Z0-9]{7}$", created.ShortenCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var row = await db.ShortLinks.AsNoTracking().SingleAsync();
        Assert.Equal(created.ShortenCode, row.ShortCode);
        Assert.Equal(originalUrl, row.OriginalUrl);
        Assert.Equal(type, row.RedirectType);
        Assert.Equal(expiresAt, row.ExpiresAt);
        Assert.Equal(123, row.MaxClicks);
        Assert.True(row.IsActive);
        Assert.NotEqual(default, row.CreatedAt);
        Assert.Equal(Guid.Empty, row.CreatorId);

        var json = await Factory.RedisDatabase.StringGetAsync($"short-link:{created.ShortenCode}");
        Assert.True(json.HasValue);
        var cached = JsonSerializer.Deserialize<ShortLinkCacheEntry>(json.ToString());
        Assert.Equal(new ShortLinkCacheEntry(originalUrl, type, expiresAt), cached);

        Factory.Reads.Reset();
        var redirect = await Factory.Client.GetAsync('/' + created.ShortenCode);
        Assert.Equal(status, redirect.StatusCode);
        Assert.Equal(originalUrl, redirect.Headers.Location!.OriginalString);
        Assert.Equal(0, Factory.Reads.Count);
    }

    [Theory]
    [InlineData("{bad json")]
    [InlineData("null")]
    [InlineData("{\"originalUrl\":null,\"shortCode\":\"abc\",\"redirectType\":302}")]
    public async Task Invalid_request_body_returns_bad_request_without_writing_a_row(string json)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await Factory.Client.PostAsync("/api/v1/short-links", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>().ShortLinks.AnyAsync());
    }
}
