using Domain.Exceptions;

namespace Api.Middleware;

/// <summary>Maps exceptions to the workspace error contract: the right status code and a <c>{ message }</c> body.</summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var (status, message) = Map(ex);
            if (status >= 500) _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new { message });
        }
    }

    public static (int Status, string Message) Map(Exception ex) => ex switch
    {
        KeyNotFoundException e => (StatusCodes.Status404NotFound, e.Message),
        DomainConflictException e => (StatusCodes.Status409Conflict, e.Message),
        DomainValidationException e => (StatusCodes.Status400BadRequest, e.Message),
        ArgumentException e => (StatusCodes.Status400BadRequest, e.Message),
        BadHttpRequestException e => (e.StatusCode, e.Message),
        _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
    };
}
