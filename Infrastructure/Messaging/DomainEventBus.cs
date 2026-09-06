using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using Domain.Abstractions.Events;
using MediatR;
 

namespace Infrastructure.Messaging
{
    public class DomainEventBus (IDomainEventDispatcher dispatcher): IDomainEventBus
    {
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
          where T : IDomainEvent
         
        {
        await   dispatcher.DispatchAsync(message, cancellationToken);
      
        }
 
    }
}
