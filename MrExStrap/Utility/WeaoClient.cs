using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;

using MrExStrap.Models.APIs;

namespace MrExStrap.Utility
{
    // Thin client for https://weao.xyz/api.
    // Fetches exploit metadata (titles + the Roblox version hash each is currently updated for)
    // so the Downgrading tab can offer a one-click "match my executor's version" flow.
    //
    // Per docs.weao.xyz, the User-Agent header "WEAO-3PService" is required.
    public static class WeaoClient
    {
        private const string EXPLOITS_ENDPOINT = "https://weao.xyz/api/status/exploits";
        private const string USER_AGENT = "WEAO-3PService";

        // Failures here are almost always client-network-side (DNS/ISP/firewall/AV TLS inspection)
        // because the server-side API itself is stable. The caller surfaces the friendly message
        // verbatim, so we keep them concrete and actionable.
        public readonly record struct WeaoResult(IReadOnlyList<WeaoExploit> Exploits, string? Error)
        {
            public bool Success => Error is null;
        }

        public static async Task<WeaoResult> GetWindowsExploitsAsync(CancellationToken token = default)
        {
            const string LOG_IDENT = "WeaoClient::GetWindowsExploitsAsync";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, EXPLOITS_ENDPOINT);
                request.Headers.UserAgent.ParseAdd(USER_AGENT);

                using var response = await App.HttpClient.SendAsync(request, token);

                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    string reason = code switch
                    {
                        403 => "weao.xyz returned 403 (forbidden). Cloudflare or your network may be blocking the request from your IP.",
                        429 => "weao.xyz returned 429 (rate limited). Wait a minute and click Refresh.",
                        503 => "weao.xyz returned 503 (service unavailable). The site may be temporarily down — try again shortly.",
                        >= 500 and < 600 => $"weao.xyz returned {code}. The site is having issues — try again shortly.",
                        _ => $"weao.xyz returned HTTP {code}. Click Refresh to try again."
                    };
                    App.Logger.WriteLine(LOG_IDENT, $"Non-success status {code} from {EXPLOITS_ENDPOINT}");
                    return new WeaoResult(Array.Empty<WeaoExploit>(), reason);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var all = await JsonSerializer.DeserializeAsync<List<WeaoExploit>>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    token);

                if (all is null)
                    return new WeaoResult(Array.Empty<WeaoExploit>(), "weao.xyz returned an empty response.");

                // Only surface Windows exploits that aren't hidden AND have a real hash.
                // Sort by title for a predictable dropdown.
                var filtered = all
                    .Where(e => !e.Hidden
                                && string.Equals(e.Platform, "Windows", StringComparison.OrdinalIgnoreCase)
                                && VersionGuidValidator.IsWellFormed(e.RbxVersion))
                    .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new WeaoResult(filtered, null);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested)
            {
                App.Logger.WriteLine(LOG_IDENT, "Request to weao.xyz timed out (30s).");
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    "Request to weao.xyz timed out. Your connection may be slow or the request is being blocked silently. Click Refresh to retry.");
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Http", ex);
                string reason = ClassifyHttpFailure(ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(), reason);
            }
            catch (JsonException ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Json", ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    "weao.xyz returned data we couldn't parse. The API may have changed — please report this. " +
                    "You can still paste a version hash manually below.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    $"Couldn't load the executor list ({ex.GetType().Name}). You can still paste a version hash manually below.");
            }
        }

        // Map common transport failures to language that points the user at where to look.
        // Almost every "empty dropdown" report so far has been a network-side block, not a code bug.
        private static string ClassifyHttpFailure(HttpRequestException ex)
        {
            var inner = ex.InnerException;
            if (inner is AuthenticationException)
            {
                return "TLS handshake with weao.xyz failed. This usually means antivirus HTTPS inspection is breaking the connection, " +
                       "or Windows is missing TLS 1.2/1.3 updates. Try disabling AV HTTPS scanning or running Windows Update.";
            }
            if (inner is SocketException sock)
            {
                return sock.SocketErrorCode switch
                {
                    SocketError.HostNotFound =>
                        "Couldn't resolve weao.xyz. Your DNS server may be blocking it (some ISPs, school networks, " +
                        "and family-filter DNS like Cloudflare 1.1.1.3 categorize it). Try switching DNS to 1.1.1.1 or 8.8.8.8.",
                    SocketError.ConnectionRefused or SocketError.NetworkUnreachable or SocketError.HostUnreachable =>
                        "Couldn't reach weao.xyz. A firewall or VPN may be blocking outbound HTTPS to that host.",
                    SocketError.TimedOut =>
                        "Connection to weao.xyz timed out. Network is slow or the host is being filtered silently.",
                    _ => $"Network error contacting weao.xyz (socket: {sock.SocketErrorCode}). Check your connection and click Refresh."
                };
            }

            string msg = (inner?.Message ?? ex.Message).Trim();
            if (string.IsNullOrEmpty(msg))
                msg = "unknown error";
            return $"Couldn't reach weao.xyz: {msg}. Your network may be blocking the request.";
        }
    }
}
