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
            var responseHtml = "<html><body>Sign-in complete. You can close this window.</body></html>";
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

            return new BrowserResult
            {
                ResultType = BrowserResultType.Success,
                Response = query.TrimStart('?')
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

    private static void OpenBrowser(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            url = url.Replace("&", "^&");
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
