using Application.IntegrationEvents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Abstractions.Contracts
{
    public interface IShortLinkClickPublisher
    {
        Task PublishAsync(
        ShortLinkClickedIntegrationEvent @event,
        CancellationToken cancellationToken = default);
    }
}
