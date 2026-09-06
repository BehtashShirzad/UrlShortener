using Application.Abstractions.Contracts;
 


namespace Infrastructure.Persistence;

internal sealed class UnitOfWork(
    ShortLinkDbContext dbContext)
    : IUnitOfWork
{
    public bool HasActiveTransaction =>
        dbContext.Database.CurrentTransaction is not null;

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ITransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        return new EfTransaction(transaction);
    }
}