using System.Diagnostics;
using System.Net;

using App01.Shared.Application.Exceptions;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;


namespace App01.Shared.Application.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, _logger);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, ILogger<ExceptionHandlingMiddleware> logger)
    {
        string errorSourceClass = "N/A";
        try
        {
            var stackTrace = new StackTrace(exception, true);
            var frame = stackTrace.GetFrame(0);
            if (frame != null) errorSourceClass = frame.GetMethod()?.DeclaringType?.FullName ?? "N/A";
        }
        catch
        {
            // Ignoruj błędy podczas analizy StackTrace (może być niedostępny)
        }

        // Handle FluentValidation exceptions
        if (exception is ValidationException validationException)
        {
            logger.LogWarning(
                "Validation error occurred in {ErrorSource}: {Errors}",
                errorSourceClass,
                string.Join(", ", validationException.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            var problemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            };

            return context.Response.WriteAsJsonAsync(problemDetails);
        }

        // Handle ForbiddenException
        if (exception is ForbiddenException forbiddenException)
        {
            logger.LogWarning(
                "Forbidden access attempt in {ErrorSource}: {Message}",
                errorSourceClass,
                forbiddenException.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = forbiddenException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3"
            };

            return context.Response.WriteAsJsonAsync(problemDetails);
        }

        // Handle NotFoundException
        if (exception is NotFoundException notFoundException)
        {
            logger.LogWarning(
                "Resource not found in {ErrorSource}: {Message}",
                errorSourceClass,
                notFoundException.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = notFoundException.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            };

            return context.Response.WriteAsJsonAsync(problemDetails);
        }

        // Handle UnauthorizedAccessException
        if (exception is UnauthorizedAccessException unauthorizedException)
        {
            logger.LogWarning(
                "Unauthorized access attempt in {ErrorSource}: {Message}",
                errorSourceClass,
                unauthorizedException.Message);

            // Bez tego nagłówka przeglądarka nie pokaże natywnego okna logowania –
            // 401 obsługuje UI przekierowaniem na stronę /login.
            context.Response.Headers.Remove("WWW-Authenticate");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = unauthorizedException.Message,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            };

            return context.Response.WriteAsJsonAsync(problemDetails);
        }

        logger.LogError(
           exception,
           "An unhandled exception has occurred in {ErrorSource}: {Message}",
           errorSourceClass,
           exception.Message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var serverErrorDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Detail = $"{exception.Message} {exception.InnerException?.Message} {exception.InnerException?.InnerException?.Message}".Trim(),
            Instance = errorSourceClass,
        };

        return context.Response.WriteAsJsonAsync(serverErrorDetails);
    }
}