using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using UrlShortener.Api.Health;
using UrlShortener.Api.Middleware;
using Microsoft.Extensions.Logging;
using Moq;

namespace UrlShortener.Tests.Unit;

/// <summary>
/// Tests for reliability features: health check writer, exception middleware, correlation ID (Phase 6).
/// </summary>
public sealed class ResilienceTests
{
    [Fact]
    public async Task HealthCheckResponseWriter_HealthyReport_WritesHealthyJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null),
                ["redis"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null)
            },
            TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.GetProperty("status").GetString().Should().Be("healthy");
        json.RootElement.GetProperty("checks").GetProperty("db").GetString().Should().Be("healthy");
        json.RootElement.GetProperty("checks").GetProperty("redis").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task HealthCheckResponseWriter_UnhealthyReport_WritesUnhealthyJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["db"] = new HealthReportEntry(HealthStatus.Unhealthy, "Connection refused", TimeSpan.Zero, null, null),
                ["redis"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.Zero, null, null)
            },
            HealthStatus.Unhealthy,
            TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var json = JsonDocument.Parse(body);

        json.RootElement.GetProperty("status").GetString().Should().Be("unhealthy");
        json.RootElement.GetProperty("checks").GetProperty("db").GetString().Should().Be("unhealthy");
        json.RootElement.GetProperty("checks").GetProperty("redis").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task HealthCheckResponseWriter_ContentType_IsApplicationJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(),
            TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteResponse(context, report);

        context.Response.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_ArgumentException_Returns400()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = _ => throw new ArgumentException("Bad input.");

        var middleware = new ExceptionHandlingMiddleware(next, logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_InvalidOperationException_Returns409()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = _ => throw new InvalidOperationException("Conflict.");

        var middleware = new ExceptionHandlingMiddleware(next, logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ExceptionHandlingMiddleware_UnhandledException_Returns500WithGenericMessage()
    {
        var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        RequestDelegate next = _ => throw new Exception("Internal details that should not leak.");

        var middleware = new ExceptionHandlingMiddleware(next, logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("An unexpected error occurred.");
        body.Should().NotContain("Internal details that should not leak.");
    }

    [Fact]
    public async Task CorrelationIdMiddleware_NoHeader_GeneratesNewCorrelationId()
    {
        string? capturedCorrelationId = null;
        RequestDelegate next = ctx =>
        {
            capturedCorrelationId = ctx.Items["X-Correlation-Id"] as string;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        capturedCorrelationId.Should().NotBeNullOrEmpty();
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(capturedCorrelationId);
    }

    [Fact]
    public async Task CorrelationIdMiddleware_ExistingHeader_UsesProvidedCorrelationId()
    {
        string? capturedCorrelationId = null;
        RequestDelegate next = ctx =>
        {
            capturedCorrelationId = ctx.Items["X-Correlation-Id"] as string;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "test-correlation-id-123";

        await middleware.InvokeAsync(context);

        capturedCorrelationId.Should().Be("test-correlation-id-123");
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("test-correlation-id-123");
    }
}
