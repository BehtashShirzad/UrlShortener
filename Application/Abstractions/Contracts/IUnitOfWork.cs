namespace Application.Abstractions.Contracts;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<ITransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);

    bool HasActiveTransaction { get; }
}