using Application.Abstractions.Contracts;
using Microsoft.EntityFrameworkCore.Storage;
 


namespace Infrastructure.Persistence;

internal sealed class EfTransaction(
    IDbContextTransaction transaction)
    : ITransaction
{
    public Task CommitAsync(
        CancellationToken cancellationToken = default)
    {
        return transaction.CommitAsync(cancellationToken);
    }

    public Task RollbackAsync(
        CancellationToken cancellationToken = default)
    {
        return transaction.RollbackAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return transaction.DisposeAsync();
    }
}