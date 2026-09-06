using MediatR;

namespace Application.Abstractions.Contracts;

public interface IBaseCommand
{
}

public interface ICommand
    : IRequest, IBaseCommand
{
}

public interface ICommand<out TResponse>
    : IRequest<TResponse>, IBaseCommand
{
}

public interface ITransactionalCommand
    : ICommand
{
}

public interface ITransactionalCommand<out TResponse>
    : ICommand<TResponse>
{
}