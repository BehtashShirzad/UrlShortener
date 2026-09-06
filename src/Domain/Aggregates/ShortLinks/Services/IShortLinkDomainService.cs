using Domain.Aggregates.ShortLinks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Aggregates.ShortLinks.Services
{
    public interface IShortLinkDomainService
    {
        public Task<ShortLink> CreateShortLink(string originalUrl,     RedirectType redirectType, DateTime? expiresAt, long? maxClicks);
    }
}
