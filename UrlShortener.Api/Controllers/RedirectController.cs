using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[EnableRateLimiting("fixed")]
public sealed class RedirectController(IRedirectService redirectService) : ControllerBase
{
    [HttpGet("{code}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public async Task<IActionResult> RedirectAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? Request.Headers["X-Forwarded-For"].FirstOrDefault();
        string? userAgent = Request.Headers.UserAgent.ToString();
        string? referrer = Request.Headers.Referer.ToString();

        var result = await redirectService.GetOriginalUrlAsync(
            code,
            ipAddress,
            string.IsNullOrEmpty(userAgent) ? null : userAgent,
            string.IsNullOrEmpty(referrer) ? null : referrer,
            cancellationToken);

        if (result.IsExpired)
        {
            return StatusCode(StatusCodes.Status410Gone, new ProblemDetails
            {
                Title = "Gone",
                Detail = $"Short code '{code}' has expired.",
                Status = StatusCodes.Status410Gone
            });
        }

        if (result.OriginalUrl is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = $"Short code '{code}' not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Redirect(result.OriginalUrl);
    }
}
