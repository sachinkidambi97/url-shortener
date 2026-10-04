using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using UrlShortener.Api.Middleware;

namespace UrlShortener.Tests.Unit;

public sealed class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock = new();

    private ExceptionHandlingMiddleware CreateSut(RequestDelegate next)
        => new ExceptionHandlingMiddleware(next, _loggerMock.Object);

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        bool nextCalled = false;
        var sut = CreateSut(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400()
    {
        var sut = CreateSut(_ => throw new ArgumentException("bad input"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404()
    {
        var sut = CreateSut(_ => throw new KeyNotFoundException("not found"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_InvalidOperationException_Returns409()
    {
        var sut = CreateSut(_ => throw new InvalidOperationException("conflict"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500()
    {
        var sut = CreateSut(_ => throw new Exception("unexpected"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        context.Response.ContentType.Should().Contain("application/json");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_ResponseBodyDoesNotContainStackTrace()
    {
        var sut = CreateSut(_ => throw new Exception("oops"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().NotContain("StackTrace", because: "stack traces should not be leaked in responses");
        body.Should().Contain("An unexpected error occurred", because: "generic message should be shown");
    }

    [Fact]
    public async Task InvokeAsync_Exception_LogsError()
    {
        var sut = CreateSut(_ => throw new Exception("logged error"));
        var context = CreateHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test/path";

        await sut.InvokeAsync(context);

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_ResponseBodyContainsMessage()
    {
        var sut = CreateSut(_ => throw new ArgumentException("the specific argument error message"));
        var context = CreateHttpContext();

        await sut.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("the specific argument error message");
    }
}
