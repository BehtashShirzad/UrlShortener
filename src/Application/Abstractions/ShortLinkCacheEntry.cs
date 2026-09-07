using Domain.Aggregates.ShortLinks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Abstractions
{
    public sealed record ShortLinkCacheEntry(
      string OriginalUrl,
      RedirectType RedirectType,
      DateTime? ExpiresAt);
}
