using Application.Dtos;
using Application.Features.ShortLinks.Commands;
using Application.Features.ShortLinks.Commands.CreateShortLink;
using Application.Features.ShortLinks.Queries;
using Domain.Aggregates.ShortLinks;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("/api/v1/short-links")]
    public class ShortLinksController(ISender sender) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<CreateShortLinkResponse>> CreateShortLink([FromBody] CreateShortLinkRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.Adapt<CreateShortLinkCommand>(), cancellationToken);
            return Ok(result);
        }

       
    }
}
