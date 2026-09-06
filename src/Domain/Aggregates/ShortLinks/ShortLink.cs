using Domain.Abstractions.Aggregates;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Domain.Aggregates.ShortLinks
{
    public class ShortLink : AggregateRoot<Guid>
    {
        private ShortLink()
        {

        }
        private ShortLink(string originalUrl,string shortCode,  RedirectType redirectType, DateTime? expiresAt,long? maxClicks)
        {
            OriginalUrl= originalUrl;
            ShortCode= shortCode;
            ExpiresAt= expiresAt;
            IsActive=true;
            MaxClicks = maxClicks;
            RedirectType = redirectType;
        }
        public string OriginalUrl { get; private set; }
        public string ShortCode { get; private set; }
        public DateTime? ExpiresAt {get;private set;}
        public bool IsActive {  get; private set; }
        public long? MaxClicks { get;private set;  }
        public RedirectType RedirectType { get;private set;  }

        public static ShortLink Create(string originalUrl, string shortCode, RedirectType redirectType, DateTime? expiresAt, long? maxClicks)
        {
            return new ShortLink(originalUrl, shortCode,  redirectType, expiresAt, maxClicks);
        }
        

    }
}
