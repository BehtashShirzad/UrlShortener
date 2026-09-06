using Application.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.ShortLinks.Queries
{
    public sealed record GetShortenUrlQuery(string ShortenCode):IQuery<GetShortenUrlQueryResponse>;
    
}
