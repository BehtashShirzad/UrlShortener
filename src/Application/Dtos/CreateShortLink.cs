
using Domain.Aggregates.ShortLinks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Dtos
{
    public record CreateShortLinkRequest(string originalUrl, string shortCode, bool isActive, RedirectType redirectType, DateTime? expiresAt, long maxClicks);
    public class CreateShortLinkResponse
    {
    }
}
