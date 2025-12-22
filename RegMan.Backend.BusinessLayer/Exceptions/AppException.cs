using System;

namespace RegMan.Backend.BusinessLayer.Exceptions;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }
    public object? Errors { get; }

    protected AppException(string message, int statusCode, string? errorCode = null, object? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Errors = errors;
    }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "Not found", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 404, errorCode, errors) { }
}

public sealed class BadRequestException : AppException
{
    public BadRequestException(string message = "Bad request", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 400, errorCode, errors) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message = "Conflict", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 409, errorCode, errors) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 403, errorCode, errors) { }
}

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 401, errorCode, errors) { }
}

public sealed class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message = "Too many requests", string? errorCode = null, object? errors = null)
        : base(message, statusCode: 429, errorCode, errors) { }
}
