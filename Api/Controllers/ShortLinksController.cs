using Application.Dtos;
using Application.Features.ShortLinks.Commands;
using Application.Features.ShortLinks.Commands.CreateShortLink;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("/api/v1/short-links")]
    public class ShortLinksController(ISender sender): ControllerBase
    {
        [HttpPost]
        public ActionResult<CreateShortLinkResponse> CreateShortLink([FromBody] CreateShortLinkRequest request)
        {
            var result = sender.Send(request.Adapt<CreateShortLinkCommand>());
            return Ok(result);
        }
    }
}
