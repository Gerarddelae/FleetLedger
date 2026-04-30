using FleetLedger.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FleetLedger.Api.Middleware;

public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, problemDetails) = exception switch
        {
            LicenseNumberAlreadyExistsException ex => (409, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Conflict",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.3.5",
                Status = 409,
                Extensions = new Dictionary<string, object?>
                {
                    ["licenseNumber"] = ex.LicenseNumber,
                    ["errorCode"] = "LICENSE_NUMBER_ALREADY_EXISTS"
                }
            }),

            DepotNameAlreadyExistsException ex => (409, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Conflict",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.3.5",
                Status = 409,
                Extensions = new Dictionary<string, object?>
                {
                    ["name"] = ex.Name,
                    ["errorCode"] = "DEPOT_NAME_ALREADY_EXISTS"
                }
            }),

            DriverNotFoundException ex => (404, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Not Found",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Status = 404,
                Extensions = new Dictionary<string, object?>
                {
                    ["driverId"] = ex.DriverId,
                    ["errorCode"] = "DRIVER_NOT_FOUND"
                }
            }),

            DepotNotFoundException ex => (404, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Not Found",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Status = 404,
                Extensions = new Dictionary<string, object?>
                {
                    ["depotId"] = ex.DepotId,
                    ["errorCode"] = "DEPOT_NOT_FOUND"
                }
            }),

            DriverInactiveException ex => (422, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Unprocessable Entity",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.24",
                Status = 422,
                Extensions = new Dictionary<string, object?>
                {
                    ["driverId"] = ex.DriverId,
                    ["errorCode"] = "DRIVER_INACTIVE"
                }
            }),

            DepotInactiveException ex => (422, new ProblemDetails
            {
                Detail = ex.Message,
                Title = "Unprocessable Entity",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.24",
                Status = 422,
                Extensions = new Dictionary<string, object?>
                {
                    ["depotId"] = ex.DepotId,
                    ["errorCode"] = "DEPOT_INACTIVE"
                }
            }),

            _ => (500, new ProblemDetails
            {
                Detail = "An unexpected error occurred.",
                Title = "Internal Server Error",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Status = 500
            })
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}

public static class DomainExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseDomainExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<DomainExceptionMiddleware>();
    }
}