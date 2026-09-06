using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.ShortLinks.Queries
{
    public class GetShortenUrlQueryHandler (IShortLinkRepository shortLinkRepository): IQueryHandler<GetShortenUrlQuery, GetShortenUrlQueryResponse>
    {
        public async Task<GetShortenUrlQueryResponse> Handle(GetShortenUrlQuery request, CancellationToken cancellationToken)
        {
            var shortenLink =await shortLinkRepository.GetByShortCodeAsync(request.ShortenCode);
            if (shortenLink is null)
                throw new InvalidOperationException("Invalid Shortlink Code");

            return new GetShortenUrlQueryResponse(shortenLink!.OriginalUrl,shortenLink.RedirectType);
        }
    }
}
