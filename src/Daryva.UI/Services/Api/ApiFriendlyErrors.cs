using System.Net;

namespace Daryva.Services.Api;

/// <summary>
/// Maps API and network failures to user-facing messages for the UI.
/// Use in ViewModels when catching exceptions from API calls.
/// </summary>
public static class ApiFriendlyErrors
{
    /// <summary>
    /// Gets a short, user-friendly message for an exception from an API call.
    /// </summary>
    public static string GetMessage(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.InnerException is TaskCanceledException)
                return "The request took too long. Please try again.";
            if (httpEx.Message.Contains("No connection could be made", StringComparison.OrdinalIgnoreCase) ||
                httpEx.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase))
                return "Cannot reach the server. Check your connection and that the API is running.";
            if (httpEx.Message.Contains("Name or service not known", StringComparison.OrdinalIgnoreCase) ||
                httpEx.Message.Contains("Could not resolve host", StringComparison.OrdinalIgnoreCase))
                return "Server address could not be found. Check your API URL.";
            return "A network error occurred. Please try again.";
        }

        if (ex is TaskCanceledException)
            return "The request was cancelled or timed out.";

        return ex.Message;
    }

    /// <summary>
    /// Gets a user-friendly message for an HTTP status code (e.g. from a failed response).
    /// </summary>
    public static string GetMessageForStatus(HttpStatusCode status)
    {
        return status switch
        {
            HttpStatusCode.Unauthorized => "You have been signed out. Please sign in again.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "The item was not found.",
            HttpStatusCode.BadRequest => "The request was invalid. Please check and try again.",
            HttpStatusCode.ServiceUnavailable => "The service is temporarily unavailable. Please try again later.",
            HttpStatusCode.GatewayTimeout => "The request took too long. Please try again.",
            >= HttpStatusCode.InternalServerError => "Something went wrong on the server. Please try again later.",
            _ => "The request failed. Please try again."
        };
    }
}
