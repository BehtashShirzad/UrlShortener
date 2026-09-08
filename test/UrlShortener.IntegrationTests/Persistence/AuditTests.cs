using System.Security.Claims;
using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Repositories;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Persistence;

public sealed class AuditTests(ShortenerFactory factory) : IntegrationTest(factory)
{
    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("62e126e9-7488-47b8-8c79-010911e0c9a0")]
    public async Task Insert_uses_the_current_subject_or_empty_guid_and_sets_creation_time(string? subject)
    {
        var accessor = Factory.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = ContextFor(subject);
        try
        {
            var before = DateTime.UtcNow.AddSeconds(-1);
            var link = await Factory.SeedAsync();
            await using var scope = Factory.Services.CreateAsyncScope();
            var saved = await scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>().ShortLinks.SingleAsync();

            Assert.Equal(link.Id, saved.Id);
            Assert.Equal(Guid.TryParse(subject, out var expected) ? expected : Guid.Empty, saved.CreatorId);
            Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow);
            Assert.Equal(DateTimeKind.Utc, saved.CreatedAt.Kind);
            Assert.Equal(Guid.Empty, saved.ModifierId);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Update_sets_modifier_audit_and_preserves_the_original_creator_audit()
    {
        var creatorId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();
        var accessor = Factory.Services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = ContextFor(creatorId.ToString());
        try
        {
            var link = await Factory.SeedAsync();
            DateTime createdAt;
            accessor.HttpContext = ContextFor(modifierId.ToString());
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();
                var tracked = (await repository.GetByIdAsync(link.Id))!;
                createdAt = tracked.CreatedAt;
                tracked.CreatorId = modifierId;
                tracked.CreatedAt = DateTime.UtcNow.AddDays(-10);
                repository.Update(tracked);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            }

            await using var verification = Factory.Services.CreateAsyncScope();
            var saved = await verification.ServiceProvider.GetRequiredService<ShortLinkDbContext>().ShortLinks.SingleAsync();
            Assert.Equal(creatorId, saved.CreatorId);
            Assert.Equal(createdAt, saved.CreatedAt);
            Assert.Equal(modifierId, saved.ModifierId);
            Assert.InRange(saved.ModifiedAt, createdAt, DateTime.UtcNow);
        }
        finally { accessor.HttpContext = null; }
    }

    private static DefaultHttpContext ContextFor(string? subject)
    {
        var context = new DefaultHttpContext();
        if (subject is not null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "test"));
        return context;
    }
}
