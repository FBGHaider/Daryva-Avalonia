using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Daryva.Api.Security;

/// <summary>
/// Catches unhandled exceptions and returns 500 with a JSON body containing error details
/// so API clients can show a useful message instead of "InternalServerError -".
/// </summary>
public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;

    public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            if (context.Response.HasStarted)
                throw;

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var body = new
            {
                error = "An error occurred while processing your request.",
                detail = ex.Message,
                inner = ex.InnerException?.Message
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
