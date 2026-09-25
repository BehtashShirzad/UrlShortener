
using Domain.Aggregates.ShortLinks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Dtos
{
    public record CreateShortLinkRequest(string originalUrl, RedirectType redirectType, DateTime? expiresAt);
    public class CreateShortLinkResponse
    {
    }
}
