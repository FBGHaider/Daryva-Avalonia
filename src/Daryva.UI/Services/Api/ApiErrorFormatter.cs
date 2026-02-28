using System.Net;
using System.Text.Json;

namespace Daryva.Services.Api;

/// <summary>
/// Parses API error response bodies (JSON or plain text) into a user-friendly message.
/// </summary>
public static class ApiErrorFormatter
{
    /// <summary>
    /// Builds a message like "Failed to {action}: {detail from server}" using
    /// JSON properties "detail", "error", "inner" when present.
    /// </summary>
    public static string Format(string action, HttpStatusCode statusCode, string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return $"Failed to {action}. Server returned {(int)statusCode} {statusCode} with no details. Check API logs.";

        var trimmed = responseBody.Trim();
        if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) || (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                var inner = root.TryGetProperty("inner", out var i) ? i.GetString() : null;
                var msg = !string.IsNullOrWhiteSpace(detail) ? detail! : (!string.IsNullOrWhiteSpace(error) ? error! : responseBody);
                if (!string.IsNullOrWhiteSpace(inner))
                    msg += " " + inner;
                return $"Failed to {action}: {msg}";
            }
            catch
            {
                // not JSON or parse failed
            }
        }

        return $"Failed to {action}: {responseBody}";
    }
}
