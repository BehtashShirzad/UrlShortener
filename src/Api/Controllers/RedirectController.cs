using Application.Features.ShortLinks.Queries;
using Domain.Aggregates.ShortLinks;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("/")]
    public class RedirectController(ISender sender) : ControllerBase
    {
        [HttpGet("{shortenCode}")]
        public async Task<IActionResult> RedirectByShortLink([FromRoute(Name = "shortenCode")] string shortenCode, CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetShortenUrlQuery(shortenCode),
                cancellationToken);
            HttpContext.Response.Headers.CacheControl =
    "no-store, no-cache, must-revalidate";

            HttpContext.Response.Headers.Pragma = "no-cache";
            HttpContext.Response.Headers.Expires = "0";
            return result.RedirectType switch
            {
                RedirectType.Permanent => RedirectPermanent(result.Url),
                RedirectType.Temporary => Redirect(result.Url),
                _ => Redirect(result.Url)
            };
        }
    }
}
