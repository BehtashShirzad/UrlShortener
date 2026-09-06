using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks;


namespace Application.Features.ShortLinks.Commands.CreateShortLink
{
    public sealed record CreateShortLinkCommand(
     string OriginalUrl,
     RedirectType RedirectType, DateTime? ExpiresAt,long MaxClicks)
     
     : ICommand<CreateShortLinkCommandResponse>;
}
