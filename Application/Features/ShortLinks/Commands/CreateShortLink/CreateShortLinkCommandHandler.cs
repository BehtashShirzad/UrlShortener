using Application.Abstractions.Contracts;
 
using Domain.Aggregates.ShortLinks;
using Domain.Aggregates.ShortLinks.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Features.ShortLinks.Commands.CreateShortLink
{
    public sealed class CreateShortLinkCommandHandler (IShortLinkDomainService shortLinkDomainService) : ICommandHandler<CreateShortLinkCommand, CreateShortLinkCommandResponse>
    {
        
        public async Task<CreateShortLinkCommandResponse> Handle(CreateShortLinkCommand request, CancellationToken cancellationToken)
        {
           var result = await shortLinkDomainService.CreateShortLink(request.OriginalUrl,  request.RedirectType,request.ExpiresAt, request.MaxClicks);
            return new CreateShortLinkCommandResponse(result.ShortCode);
        }
    }
}
