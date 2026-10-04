using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api")]
[EnableRateLimiting("fixed")]
public sealed class UrlController(
    IUrlShorteningService urlShorteningService,
    IAnalyticsService analyticsService,
    IUserRepository userRepository) : ControllerBase
{
    [HttpPost("shorten")]
    [Authorize]
    [ProducesResponseType(typeof(ShortenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ShortenAsync(
        [FromBody] ShortenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ShortenResponse response = await urlShorteningService.ShortenAsync(request, baseUrl, userId, cancellationToken);
        return Created(response.ShortUrl, response);
    }

    [HttpPost("shorten/bulk")]
    [Authorize]
    [ProducesResponseType(typeof(BulkShortenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BulkShortenAsync(
        [FromBody] BulkShortenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        BulkShortenResponse response = await urlShorteningService.BulkShortenAsync(request, baseUrl, userId, cancellationToken);
        return Ok(response);
    }

    [HttpGet("stats/{code}")]
    [Authorize]
    [ProducesResponseType(typeof(StatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
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

    [HttpGet("urls")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<UrlListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyUrlsAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var urls = await userRepository.GetUrlsByUserIdAsync(userId.Value, cancellationToken);

        var items = urls.Select(u => new UrlListItem
        {
            ShortCode = u.ShortCode,
            ShortUrl = $"{baseUrl.TrimEnd('/')}/{u.ShortCode}",
            OriginalUrl = u.OriginalUrl,
            CreatedAt = u.CreatedAt,
            ExpiresAt = u.ExpiresAt
        }).ToList();

        return Ok(items);
    }

    [HttpPut("urls/{code}")]
    [Authorize]
    [ProducesResponseType(typeof(ShortenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUrlAsync(
        [FromRoute] string code,
        [FromBody] UpdateUrlRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await urlShorteningService.UpdateUrlAsync(code, request.Url, userId.Value, baseUrl, cancellationToken);
        return Ok(response);
    }

    [HttpDelete("urls/{code}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUrlAsync(
        [FromRoute] string code,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromClaims();
        if (userId is null)
            return Unauthorized();

        await urlShorteningService.DeleteUrlAsync(code, userId.Value, cancellationToken);
        return NoContent();
    }

    private int? GetUserIdFromClaims()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (sub is not null && int.TryParse(sub, out var id))
            return id;

        return null;
    }
}
