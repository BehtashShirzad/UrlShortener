using Application.Abstractions.Contracts;
using MediatR;

public sealed class TransactionBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalCommand &&
            request is not ITransactionalCommand<TResponse>)
        {
            return await next();
        }

        if (unitOfWork.HasActiveTransaction)
            return await next();

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var response = await next();

            await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}