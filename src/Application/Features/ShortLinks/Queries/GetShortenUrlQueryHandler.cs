using Application.Abstractions.Caching;
using Application.Abstractions.Contracts;
using Application.IntegrationEvents;
using Domain.Abstractions;
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Repositories;

namespace Application.Features.ShortLinks.Queries;

public sealed class GetShortenUrlQueryHandler(
    IShortLinkCacheService shortLinkCacheService, IShortLinkClickPublisher shortLinkClickPublisher)
    : IQueryHandler<GetShortenUrlQuery, GetShortenUrlQueryResponse>
{
    public async Task<GetShortenUrlQueryResponse> Handle(
        GetShortenUrlQuery request,
        CancellationToken cancellationToken)
    {
        var shortLink = await shortLinkCacheService.GetAsync(
            request.ShortenCode,
            cancellationToken);

        if (shortLink is null)
            throw new InvalidOperationException("Invalid short link code.");

        await shortLinkClickPublisher.PublishAsync(
                new ShortLinkClickedIntegrationEvent(
                IdGenerator.New(),
                shortLink.Id,
                DateTimeOffset.UtcNow),
                cancellationToken);

        return new GetShortenUrlQueryResponse(
            shortLink.OriginalUrl,
            shortLink.RedirectType);
    }
}