using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkFlow360.Application.Common.Exceptions;

namespace WorkFlow360.API.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "Request could not be completed"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Server error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception switch
                {
                    DbUpdateConcurrencyException => "Someone else changed this record at the same time. Refresh and try again.",
                    _ when status == StatusCodes.Status500InternalServerError => "An unexpected error occurred. Please try again later.",
                    _ => exception.Message
                }
            }
        });
    }
}
