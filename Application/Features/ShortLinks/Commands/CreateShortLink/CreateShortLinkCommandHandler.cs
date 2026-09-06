using Application.Abstractions.Contracts;
using Domain.Aggregates.ShortLinks.Repositories;
using Domain.Aggregates.ShortLinks.Services;

namespace Application.Features.ShortLinks.Commands.CreateShortLink;

public sealed class CreateShortLinkCommandHandler(
    IShortLinkDomainService shortLinkDomainService,
    IShortLinkRepository shortLinkRepository)
    : ICommandHandler<CreateShortLinkCommand, CreateShortLinkCommandResponse>
{
    public async Task<CreateShortLinkCommandResponse> Handle(
        CreateShortLinkCommand request,
        CancellationToken cancellationToken)
    {
        var shortLink = await shortLinkDomainService.CreateShortLink(
            request.OriginalUrl,
            request.RedirectType,
            request.ExpiresAt,
            request.MaxClicks);

        await shortLinkRepository.AddAsync(
            shortLink,
            cancellationToken);

        return new CreateShortLinkCommandResponse(
            shortLink.ShortCode);
    }
}