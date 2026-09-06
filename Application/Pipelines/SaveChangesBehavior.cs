using Application.Abstractions.Contracts;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Pipelines
{
    public sealed class SaveChangesBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (request is not IBaseCommand)
                return await next();

            
            if (request is ITransactionalCommand ||
                request is ITransactionalCommand<TResponse>)
            {
                return await next();
            }

            var response = await next();

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return response;
        }
    }
}
