using System.Net;
using FluentAssertions;

namespace UrlShortener.Tests.Integration;

public sealed class RateLimitingTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly RateLimitTestWebApplicationFactory _factory;

    public RateLimitingTests(RateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RateLimiter_After100Requests_Returns429OnNext()
    {
        using var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        const int limit = 100;
        var tasks = new List<Task<HttpResponseMessage>>(limit);

        for (int i = 0; i < limit; i++)
        {
            tasks.Add(client.GetAsync($"/zzz_nonexistent_{i}"));
        }

        var responses = await Task.WhenAll(tasks);

        var nonLimitedCount = responses.Count(r => r.StatusCode != HttpStatusCode.TooManyRequests);
        nonLimitedCount.Should().BeGreaterThan(0, "at least some requests should pass the rate limit");

        var extraResponse = await client.GetAsync("/zzz_extra_request");
        extraResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
