using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;

using MrExStrap.Models.APIs;

namespace MrExStrap.Utility
{
    // Where the executor list actually came from, so the UI can note when the backup was used.
    public enum WeaoSource { None, Weao, Mirror }

    // Client for the WEAO exploit-status API (https://weao.xyz/api), with a transparent failover
    // to the robloxscripts.com mirror of the same data.
    //
    // weao.xyz is frequently unreachable on end-user machines — not because the app is broken, but
    // because the domain is blocked at the network/ISP layer (SNI filtering surfaces as a browser
    // ERR_SSL_PROTOCOL_ERROR / a corrupted TLS frame in .NET) or intercepted by antivirus HTTPS
    // scanning. robloxscripts.com serves the identical data from a DIFFERENT domain, so a
    // weao.xyz-specific block doesn't reach it. We try weao.xyz first (canonical, freshest) and
    // fall back to the mirror on any failure. Mapping + shapes: docs.robloxscripts.com (#weao).
    //
    // Per docs.weao.xyz the User-Agent "WEAO-3PService" is required for weao.xyz. The mirror just
    // needs any non-bot User-Agent (a bare "curl/x" UA is challenged) — App.HttpClient's default
    // "MrExBloxstrap/<version>" already satisfies that, so the mirror request sends no extra header.
    public static class WeaoClient
    {
        private const string EXPLOITS_ENDPOINT = "https://weao.xyz/api/status/exploits";
        private const string MIRROR_EXPLOITS_ENDPOINT = "https://robloxscripts.com/api/v1/weao/status/exploits";
        private const string USER_AGENT = "WEAO-3PService";

        public readonly record struct WeaoResult(IReadOnlyList<WeaoExploit> Exploits, string? Error, WeaoSource Source = WeaoSource.None)
        {
            public bool Success => Error is null;
        }

        // Endpoint descriptors so the two sources can be tried in either order.
        private readonly record struct Endpoint(string Url, string Host, WeaoSource Source, bool SendWeaoUserAgent);
        private static readonly Endpoint WeaoEndpoint = new(EXPLOITS_ENDPOINT, "weao.xyz", WeaoSource.Weao, true);
        private static readonly Endpoint MirrorEndpoint = new(MIRROR_EXPLOITS_ENDPOINT, "robloxscripts.com", WeaoSource.Mirror, false);

        public static async Task<WeaoResult> GetWindowsExploitsAsync(CancellationToken token = default)
        {
            const string LOG_IDENT = "WeaoClient::GetWindowsExploitsAsync";

            // Default order is weao.xyz (canonical, freshest) then the robloxscripts.com mirror. The
            // "prefer robloxscripts.com" setting flips it for users whose network/ISP blocks weao.xyz,
            // so they skip the dead weao.xyz attempt and hit the working mirror first. Either way the
            // other source is the fallback, and both are tried before giving up.
            var order = App.Settings.Prop.PreferRobloxScriptsApi
                ? new[] { MirrorEndpoint, WeaoEndpoint }
                : new[] { WeaoEndpoint, MirrorEndpoint };

            WeaoResult primaryResult = default;

            for (int i = 0; i < order.Length; i++)
            {
                var ep = order[i];
                var result = await FetchExploitsAsync(ep.Url, ep.Host, ep.Source, ep.SendWeaoUserAgent, token);

                if (result.Success)
                {
                    if (i > 0)
                        App.Logger.WriteLine(LOG_IDENT, $"Loaded {result.Exploits.Count} executors from {ep.Host} (fallback).");
                    return result;
                }

                if (i == 0)
                {
                    primaryResult = result;
                    App.Logger.WriteLine(LOG_IDENT, $"{ep.Host} failed ({result.Error}) — falling back to {order[i + 1].Host}.");
                }
                else
                {
                    App.Logger.WriteLine(LOG_IDENT, $"{ep.Host} also failed ({result.Error}).");
                }
            }

            // Both sources are unreachable — almost always a broad block on the user's PC or network
            // (antivirus HTTPS scanning, or an ISP/router SSL filter) rather than anything app-side.
            // Lead with the preferred-source reason and make clear the backup failed too.
            return new WeaoResult(
                Array.Empty<WeaoExploit>(),
                "Couldn't load the executor list from weao.xyz or the robloxscripts.com backup.\n\n" +
                $"{primaryResult.Error}\n\n" +
                "Both sources being down at once usually means something on your PC or network is blocking this kind " +
                "of traffic — antivirus HTTPS/SSL scanning, or an ISP/router-level filter. You can still paste a " +
                "version hash manually below.",
                WeaoSource.None);
        }

        // Fetch + parse + filter one source. Never throws — every failure path returns a WeaoResult
        // carrying a human-readable Error so the caller can decide whether to fall back.
        private static async Task<WeaoResult> FetchExploitsAsync(string url, string host, WeaoSource source, bool sendWeaoUserAgent, CancellationToken token)
        {
            string LOG_IDENT = $"WeaoClient::FetchExploits({host})";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (sendWeaoUserAgent)
                    request.Headers.UserAgent.ParseAdd(USER_AGENT);

                using var response = await App.HttpClient.SendAsync(request, token);

                if (!response.IsSuccessStatusCode)
                {
                    int code = (int)response.StatusCode;
                    string reason = code switch
                    {
                        403 => $"{host} returned 403 (forbidden). Cloudflare or your network may be blocking the request from your IP.",
                        429 => $"{host} returned 429 (rate limited). Wait a minute and click Refresh.",
                        503 => $"{host} returned 503 (service unavailable). It may be temporarily down — try again shortly.",
                        >= 500 and < 600 => $"{host} returned {code}. The site is having issues — try again shortly.",
                        _ => $"{host} returned HTTP {code}. Click Refresh to try again."
                    };
                    App.Logger.WriteLine(LOG_IDENT, $"Non-success status {code} from {url}");
                    return new WeaoResult(Array.Empty<WeaoExploit>(), reason, source);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var all = await JsonSerializer.DeserializeAsync<List<WeaoExploit>>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    token);

                if (all is null)
                    return new WeaoResult(Array.Empty<WeaoExploit>(), $"{host} returned an empty response.", source);

                // Only surface Windows exploits that aren't hidden AND have a real hash.
                // Sort by title for a predictable dropdown.
                var filtered = all
                    .Where(e => !e.Hidden
                                && string.Equals(e.Platform, "Windows", StringComparison.OrdinalIgnoreCase)
                                && VersionGuidValidator.IsWellFormed(e.RbxVersion))
                    .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new WeaoResult(filtered, null, source);
            }
            catch (TaskCanceledException) when (!token.IsCancellationRequested)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Request to {host} timed out (30s).");
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    $"Request to {host} timed out. Your connection may be slow or the request is being blocked silently.", source);
            }
            catch (HttpRequestException ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Http", ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(), ClassifyHttpFailure(ex, host), source);
            }
            catch (JsonException ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Json", ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    $"{host} returned data we couldn't parse. The API may have changed — please report this.", source);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return new WeaoResult(Array.Empty<WeaoExploit>(),
                    $"Couldn't load the executor list from {host} ({ex.GetType().Name}).", source);
            }
        }

        // Map common transport failures to language that points the user at where to look.
        // Almost every "empty dropdown" report so far has been a network-side block, not a code bug.
        private static string ClassifyHttpFailure(HttpRequestException ex, string host)
        {
            var inner = ex.InnerException;
            if (inner is AuthenticationException)
            {
                return $"TLS handshake with {host} failed. This usually means antivirus HTTPS inspection is breaking the connection, " +
                       "or Windows is missing TLS 1.2/1.3 updates. Try disabling AV HTTPS scanning or running Windows Update.";
            }

            // A TLS stream that corrupts mid-flight — IOException like "Cannot determine the frame
            // size or a corrupted frame was received", usually wrapped as "The SSL connection could
            // not be established". A middlebox rewriting the connection: antivirus HTTPS/SSL scanning,
            // a filtering proxy/VPN, or an ISP/router SSL filter on the domain (which shows up in a
            // browser as ERR_SSL_PROTOCOL_ERROR — confirmed even with AV fully off).
            if (IsTlsStreamCorruption(ex))
            {
                return $"The secure connection to {host} was corrupted before it finished (a TLS frame came back malformed). " +
                       "This is usually antivirus HTTPS/SSL scanning, or an ISP/router-level filter blocking the site. " +
                       "Try turning off AV HTTPS scanning, switching DNS to 1.1.1.1 or 8.8.8.8, or a different network.";
            }

            if (inner is SocketException sock)
            {
                return sock.SocketErrorCode switch
                {
                    SocketError.HostNotFound =>
                        $"Couldn't resolve {host}. Your DNS server may be blocking it (some ISPs, school networks, " +
                        "and family-filter DNS like Cloudflare 1.1.1.3 categorize it). Try switching DNS to 1.1.1.1 or 8.8.8.8.",
                    SocketError.ConnectionRefused or SocketError.NetworkUnreachable or SocketError.HostUnreachable =>
                        $"Couldn't reach {host}. A firewall or VPN may be blocking outbound HTTPS to that host.",
                    SocketError.TimedOut =>
                        $"Connection to {host} timed out. Network is slow or the host is being filtered silently.",
                    _ => $"Network error contacting {host} (socket: {sock.SocketErrorCode}). Check your connection and click Refresh."
                };
            }

            string msg = (inner?.Message ?? ex.Message).Trim();
            if (string.IsNullOrEmpty(msg))
                msg = "unknown error";
            return $"Couldn't reach {host}: {msg}.";
        }

        // Walk the inner-exception chain for the signature of a corrupted TLS stream. SslStream
        // surfaces this as an IOException mentioning the TLS "frame" (e.g. "Cannot determine the
        // frame size or a corrupted frame was received"); HttpClient wraps it as "The SSL
        // connection could not be established". A genuine handshake/cert failure is an
        // AuthenticationException instead and is handled before this is called.
        private static bool IsTlsStreamCorruption(Exception ex)
        {
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e is IOException && e.Message.Contains("frame", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (e.Message.Contains("SSL connection could not be established", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
