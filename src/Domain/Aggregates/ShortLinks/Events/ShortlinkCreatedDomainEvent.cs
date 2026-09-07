using Domain.Abstractions;
using Domain.Abstractions.Events;

namespace Domain.Aggregates.ShortLinks.Events;

public sealed class ShortLinkCreatedDomainEvent : IDomainEvent
{
    public ShortLinkCreatedDomainEvent(
        Guid shortLinkId,
        string shortCode,
        string originalUrl, RedirectType redirectType,
        DateTime? expiresAt)
    {
        Id = IdGenerator.New();
        OccurredOn = DateTime.UtcNow;

        ShortLinkId = shortLinkId;
        ShortCode = shortCode;
        OriginalUrl = originalUrl;
        ExpiresAt = expiresAt;
        RedirectType = redirectType;
    }

    public Guid Id { get; }

    public DateTime OccurredOn { get; }

    public Guid ShortLinkId { get; }

    public string ShortCode { get; }

    public string OriginalUrl { get; }
    public RedirectType RedirectType { get; }

    public DateTime? ExpiresAt { get; }
}