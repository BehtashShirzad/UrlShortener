using Domain.Aggregates.ShortLinks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.ShortLinks.Queries
{
    public sealed record GetShortenUrlQueryResponse(string Url,RedirectType RedirectType);
    
}
