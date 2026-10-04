using FluentAssertions;
using Microsoft.AspNetCore.Http;
using UrlShortener.Api.Middleware;

namespace UrlShortener.Tests.Unit;

public sealed class CorrelationIdMiddlewareTests
{
    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task InvokeAsync_NoCorrelationIdInRequest_GeneratesNewCorrelationId()
    {
        var sut = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.Headers.Should().ContainKey("X-Correlation-Id");
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        correlationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdInRequest_UsesExistingCorrelationId()
    {
        var sut = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();
        const string existingId = "my-correlation-id-123";
        context.Request.Headers["X-Correlation-Id"] = existingId;

        await sut.InvokeAsync(context);

        var responseCorrelationId = context.Response.Headers["X-Correlation-Id"].ToString();
        responseCorrelationId.Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_CorrelationIdStoredInContextItems()
    {
        string? capturedId = null;
        var sut = new CorrelationIdMiddleware(ctx =>
        {
            capturedId = ctx.Items["X-Correlation-Id"]?.ToString();
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();
        const string existingId = "test-correlation-456";
        context.Request.Headers["X-Correlation-Id"] = existingId;

        await sut.InvokeAsync(context);

        capturedId.Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_GeneratedCorrelationId_IsValidGuid()
    {
        var sut = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        // Generated IDs are GUID without hyphens (format "N")
        correlationId.Should().HaveLength(32, because: "GUID format N produces 32 hex chars");
        correlationId.Should().MatchRegex("^[0-9a-f]{32}$", because: "format N uses lowercase hex");
    }

    [Fact]
    public async Task InvokeAsync_NextMiddlewareIsCalled()
    {
        bool nextCalled = false;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }
}
