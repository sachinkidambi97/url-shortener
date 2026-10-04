namespace UrlShortener.Api.Exceptions;

/// <summary>
/// Thrown when a user attempts to access or modify a resource they do not own.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }

    public ForbiddenAccessException(string message, Exception innerException) : base(message, innerException) { }
}
