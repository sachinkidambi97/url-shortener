using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace UrlShortener.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            string correlationId = context.Items["X-Correlation-Id"] as string ?? "unknown";

            logger.LogError(ex, "Unhandled exception for request {Method} {Path} (CorrelationId: {CorrelationId})",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        (int statusCode, string title, string detail) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            HttpRequestException { StatusCode: HttpStatusCode.Gone } => (StatusCodes.Status410Gone, "Gone", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
