using Application.Abstractions.Contracts;
using Domain.Abstractions.Aggregates;
using Domain.Abstractions.Entities;
using Domain.Aggregates.ShortLinks;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class ShortLinkDbContext(
    DbContextOptions<ShortLinkDbContext> options,
    IDomainEventBus domainEventBus,
    ICurrentUser currentUser)
    : DbContext(options)
{
    public DbSet<ShortLink> ShortLinks => Set<ShortLink>();

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();

        var aggregates = ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = aggregates
            .SelectMany(x => x.DomainEvents)
            .ToList();

        foreach (var domainEvent in domainEvents)
        {
            await domainEventBus.PublishAsync(
                domainEvent,
                cancellationToken);
        }

        var result = await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearEvents();
        }

        return result;
    }

    private void ApplyAuditInformation()
    {
        var userId = Guid.TryParse(
            currentUser.UserId,
            out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;

        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatorId = userId;
                    entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifierId = userId;
                    entry.Entity.ModifiedAt = now;

                    entry.Property(nameof(Entity.CreatorId))
                        .IsModified = false;

                    entry.Property(nameof(Entity.CreatedAt))
                        .IsModified = false;

                    break;
            }
        }
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ShortLinkDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}