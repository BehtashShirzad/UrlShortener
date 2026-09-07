using Application.Abstractions.Caching;
using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks.Repositories;

namespace Application.Features.ShortLinks.Queries;

public sealed class GetShortenUrlQueryHandler(
    IShortLinkCacheService shortLinkCacheService)
    : IQueryHandler<GetShortenUrlQuery, GetShortenUrlQueryResponse>
{
    public async Task<GetShortenUrlQueryResponse> Handle(
        GetShortenUrlQuery request,
        CancellationToken cancellationToken)
    {
        var originalUrl = await shortLinkCacheService.GetAsync(
            request.ShortenCode,
            cancellationToken);

        if (originalUrl is null)
            throw new InvalidOperationException("Invalid short link code.");
 

        return new GetShortenUrlQueryResponse(
            originalUrl.OriginalUrl,
            originalUrl.RedirectType);
    }
}