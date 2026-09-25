using Application.Abstractions.Contracts;
using Application.Features.ShortLinks.Commands.CreateShortLink;
using Docker.DotNet.Models;
using Domain.Aggregates.ShortLinks;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Api
{
    public class ProcessClickTests(ShortenerFactory factory) : IntegrationTest(factory)
    {

        [Fact]
        public async Task Redirect_publishes_click_message_to_Redis_stream()
        {
       
            var expiresAt = DateTime.UtcNow.Date.AddDays(2);
            const string originalUrl = "https://example.com/path?q=a%20b&source=test#section";
            var createResponse = await Factory.Client.PostAsJsonAsync("/api/v1/short-links", new
            {
                originalUrl,
                 
                redirectType = RedirectType.Temporary,
                expiresAt
            });

            var created =
                await createResponse.Content
                    .ReadFromJsonAsync<CreateShortLinkCommandResponse>();

            Assert.NotNull(created);

            // Act
            var redirect = await Factory.Client.GetAsync(
                "/" + created.ShortenCode);

            // Assert
            Assert.Equal(
                HttpStatusCode.Redirect,
                redirect.StatusCode);

            var entries =
                await Factory.RedisDatabase.StreamRangeAsync(
                    "short-link-clicks");

            Assert.Single(entries);

            var entry = entries[0];

            var values = entry.Values.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString());

            Assert.True(
                Guid.TryParse(values["eventId"], out _));

            await using var scope =
                Factory.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ShortLinkDbContext>();

            var shortLink = await db.ShortLinks
                .AsNoTracking()
                .SingleAsync();

            Assert.Equal(
                shortLink.Id.ToString(),
                values["shortLinkId"]);
        }
    }
}
