using Domain.Abstractions.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;



namespace Application.Abstractions.Contracts
{
public interface IDomainEventBus
{
    Task PublishAsync<T>(
        T domainEvent,
        CancellationToken cancellationToken = default)
        where T : IDomainEvent;
}


}