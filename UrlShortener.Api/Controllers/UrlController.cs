using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api")]
[EnableRateLimiting("fixed")]
public sealed class UrlController(
    IUrlShorteningService urlShorteningService,
    IAnalyticsService analyticsService) : ControllerBase
{
    [HttpPost("shorten")]
    [ProducesResponseType(typeof(ShortenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ShortenAsync(
        [FromBody] ShortenRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ShortenResponse response = await urlShorteningService.ShortenAsync(request, baseUrl, cancellationToken);
        return Created(response.ShortUrl, response);
    }

    [HttpGet("stats/{code}")]
    [ProducesResponseType(typeof(StatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatsAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var stats = await analyticsService.GetStatsAsync(code, cancellationToken: cancellationToken);
        if (stats is null)
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = $"Short code '{code}' not found.",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(stats);
    }
}
