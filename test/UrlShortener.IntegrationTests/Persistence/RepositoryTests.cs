using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UrlShortener.IntegrationTests.Support;

namespace UrlShortener.IntegrationTests.Persistence;

public sealed class RepositoryTests(ShortenerFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Migrations_create_the_schema_on_PostgreSQL_17()
    {
        await using var connection = new NpgsqlConnection(Factory.Postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SHOW server_version_num", connection);
        var version = int.Parse((string)(await command.ExecuteScalarAsync())!);
        Assert.InRange(version, 170000, 179999);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        Assert.Contains("20260906191318_init", await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    [Theory]
    [InlineData(RedirectType.Temporary, false)]
    [InlineData(RedirectType.Permanent, true)]
    public async Task Saved_link_round_trips_all_business_fields_in_a_new_scope(RedirectType type, bool limited)
    {
        // Whole seconds avoid PostgreSQL's microsecond timestamp rounding.
        DateTime? expiresAt = limited ? DateTime.UtcNow.Date.AddDays(2) : null;
        long? maxClicks = limited ? 123L : null;
        var original = await Factory.SeedAsync(expiresAt: expiresAt, type: type, maxClicks: maxClicks);

        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();
        var found = await repository.GetByIdAsync(original.Id);

        Assert.NotNull(found);
        Assert.Equal(original.OriginalUrl, found.OriginalUrl);
        Assert.Equal(original.ShortCode, found.ShortCode);
        Assert.Equal(type, found.RedirectType);
        Assert.Equal(expiresAt, found.ExpiresAt);
        Assert.Equal(maxClicks, found.MaxClicks);
        Assert.True(found.IsActive);
        Assert.Empty(found.DomainEvents);
        Assert.True(await repository.ExistsByShortCodeAsync(found.ShortCode));
        Assert.Equal(found.Id, (await repository.GetByShortCodeAsync(found.ShortCode))!.Id);
    }

    [Fact]
    public async Task Missing_and_differently_cased_codes_do_not_match()
    {
        await Factory.SeedAsync("Ab12xyz");
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();

        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await repository.GetByShortCodeAsync("missing"));
        Assert.Null(await repository.GetByShortCodeAsync("ab12xyz"));
        Assert.False(await repository.ExistsByShortCodeAsync("ab12xyz"));
    }

    [Fact]
    public async Task Duplicate_short_code_is_rejected_by_the_database_unique_index()
    {
        await Factory.SeedAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();
        var duplicate = ShortLink.Create("https://other.example", "Ab12xyz", RedirectType.Temporary, null, null);
        await repository.AddAsync(duplicate);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_ShortLinks_ShortCode", postgres.ConstraintName);
        Assert.Single(duplicate.DomainEvents);
    }

    [Theory]
    [InlineData(2049, 7)]
    [InlineData(20, 33)]
    public async Task Database_rejects_values_exceeding_column_limits(int urlLength, int codeLength)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var link = ShortLink.Create(new string('u', urlLength), new string('c', codeLength), RedirectType.Temporary, null, null);
        await scope.ServiceProvider.GetRequiredService<IShortLinkRepository>().AddAsync(link);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation,
            Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [Fact]
    public async Task Remove_deletes_the_persisted_row()
    {
        var link = await Factory.SeedAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IShortLinkRepository>();
            repository.Remove((await repository.GetByIdAsync(link.Id))!);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }
        await using var verification = Factory.Services.CreateAsyncScope();
        Assert.False(await verification.ServiceProvider.GetRequiredService<IShortLinkRepository>().ExistsByShortCodeAsync(link.ShortCode));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transaction_commit_or_rollback_controls_row_visibility(bool commit)
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Assert.False(work.HasActiveTransaction);
            await using (var transaction = await work.BeginTransactionAsync())
            {
                Assert.True(work.HasActiveTransaction);
                await scope.ServiceProvider.GetRequiredService<IShortLinkRepository>().AddAsync(
                    ShortLink.Create("https://example.com", "Tx12345", RedirectType.Temporary, null, null));
                await work.SaveChangesAsync();
                if (commit) await transaction.CommitAsync();
                else await transaction.RollbackAsync();
            }
            Assert.False(work.HasActiveTransaction);
        }

        await using var verification = Factory.Services.CreateAsyncScope();
        var exists = await verification.ServiceProvider.GetRequiredService<IShortLinkRepository>().ExistsByShortCodeAsync("Tx12345");
        Assert.Equal(commit, exists);
    }
}
