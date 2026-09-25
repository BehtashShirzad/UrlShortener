using Application.IntegrationEvents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Abstractions.Contracts
{
    public interface IShortLinkClickProcessor
    {
        Task<bool> ProcessAsync(
            ShortLinkClickedIntegrationEvent @event,
            CancellationToken cancellationToken);
    }
}
