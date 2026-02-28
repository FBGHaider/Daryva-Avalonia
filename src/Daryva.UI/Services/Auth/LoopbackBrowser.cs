using System.Diagnostics;
using System.Net;
using Duende.IdentityModel.OidcClient.Browser;

namespace Daryva.Services.Auth;

/// <summary>
/// Opens the system default browser and receives the OIDC callback on a loopback HTTP listener.
/// Must use the same port as configured in OidcClientOptions.RedirectUri (see LoopbackPort).
/// </summary>
public sealed class LoopbackBrowser : IBrowser
{
    /// <summary>
    /// Port used for loopback redirect. Must match RedirectUri in OidcClientOptions.
    /// </summary>
    public const int LoopbackPort = 58432;

    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{LoopbackPort}/");
        listener.Start();

        try
        {
            OpenBrowser(options.StartUrl);

            var context = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            var query = context.Request.Url?.Query ?? string.Empty;
            var queryTrimmed = query.TrimStart('?');
            var hasError = queryTrimmed.Contains("error=", StringComparison.OrdinalIgnoreCase);
            string responseHtml;
            if (hasError)
            {
                var (error, desc) = GetQueryValue(queryTrimmed, "error", "error_description");
                var descEscaped = string.IsNullOrEmpty(desc) ? "" : desc.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
                responseHtml = $"<html><body><p>Sign-in failed.</p><p><b>{error}</b></p><p>{descEscaped}</p><p>You can close this window and fix the issue in Clerk (see Scopes).</p></body></html>";
            }
            else
            {
                responseHtml = "<html><body>Sign-in complete. You can close this window.</body></html>";
            }
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html";
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            context.Response.OutputStream.Close();

            if (string.IsNullOrEmpty(query))
            {
                return new BrowserResult
                {
                    ResultType = BrowserResultType.HttpError,
                    Error = "No callback query string."
                };
            }

            if (hasError)
            {
                var (err, errDesc) = GetQueryValue(queryTrimmed, "error", "error_description");
                return new BrowserResult
                {
                    ResultType = BrowserResultType.HttpError,
                    Error = string.IsNullOrWhiteSpace(errDesc) ? err : $"{err}: {errDesc}",
                    Response = queryTrimmed
                };
            }

            return new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = queryTrimmed
            };
        }
        catch (OperationCanceledException)
        {
            return new BrowserResult { ResultType = BrowserResultType.UserCancel, Error = "Canceled." };
        }
        catch (Exception ex)
        {
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
        }
    }

    private static (string value1, string value2) GetQueryValue(string query, string key1, string key2)
    {
        string v1 = "", v2 = "";
        foreach (var part in query.Split('&'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0) continue;
            var k = Uri.UnescapeDataString(part[..eq].Trim());
            var v = Uri.UnescapeDataString(part[(eq + 1)..].Trim());
            if (string.Equals(k, key1, StringComparison.OrdinalIgnoreCase)) v1 = v;
            if (string.Equals(k, key2, StringComparison.OrdinalIgnoreCase)) v2 = v;
        }
        return (v1, v2);
    }

    private static void OpenBrowser(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            // URL is passed as a single quoted argument; do not escape & (that was appending ^ to
            // param values, e.g. client_id=qR4eijSepugdOw7g^ and breaking Clerk).
            Process.Start(new ProcessStartInfo("cmd", $"/c start \"\" \"{url}\"") { CreateNoWindow = true });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", url);
        }
        else
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { CreateNoWindow = true });
        }
    }
}
