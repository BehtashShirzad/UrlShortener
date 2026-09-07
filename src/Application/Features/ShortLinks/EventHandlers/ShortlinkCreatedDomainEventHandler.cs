using Application.Abstractions.Events;
using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks.Events;
using MediatR;
using Application.Abstractions.Caching;
using Application.Abstractions;

namespace Application.Features.ShortLinks.EventHandlers;

public sealed class ShortLinkCreatedDomainEventHandler(
    IShortLinkCacheService shortLinkCache)
    : INotificationHandler<
        DomainEventNotification<ShortLinkCreatedDomainEvent>>
{
    public async Task Handle(
        DomainEventNotification<ShortLinkCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        await shortLinkCache.SetAsync(
            domainEvent.ShortCode,
            new  ShortLinkCacheEntry(
            domainEvent.OriginalUrl,
            domainEvent.RedirectType,
            domainEvent.ExpiresAt),
            cancellationToken);
    }
}