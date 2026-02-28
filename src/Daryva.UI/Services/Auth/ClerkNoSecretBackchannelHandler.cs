using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Daryva.Services.Auth;

/// <summary>
/// For token endpoint only: strips client authentication so Clerk receives auth method "none" (public client / PKCE).
/// Leaves userinfo and other requests unchanged (userinfo needs Authorization: Bearer token).
/// </summary>
public sealed class ClerkNoSecretBackchannelHandler : DelegatingHandler
{
    public ClerkNoSecretBackchannelHandler(HttpMessageHandler? innerHandler = null)
        : base(innerHandler ?? new SocketsHttpHandler())
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isTokenEndpoint = request.RequestUri?.AbsoluteUri.Contains("/oauth/token", StringComparison.OrdinalIgnoreCase) == true;
        var modified = isTokenEndpoint ? await CloneAndStripClientAuthAsync(request).ConfigureAwait(false) : request;
        try
        {
            return await base.SendAsync(modified, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (modified != request)
                modified.Dispose();
        }
    }

    private static async Task<HttpRequestMessage> CloneAndStripClientAuthAsync(HttpRequestMessage request)
    {
        var modified = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                continue;
            foreach (var value in header.Value)
                modified.Headers.TryAddWithoutValidation(header.Key, value);
        }

        if (request.Content != null)
        {
            var contentType = request.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                var form = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                var filtered = string.Join("&", form.Split('&')
                    .Where(part => !part.StartsWith("client_secret=", StringComparison.OrdinalIgnoreCase)));
                modified.Content = new StringContent(filtered, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
            }
            else
            {
                modified.Content = request.Content;
            }
        }

        modified.Version = request.Version;
        return modified;
    }
}
