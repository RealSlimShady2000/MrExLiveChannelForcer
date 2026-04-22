using System.Net.Http;
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

        public static async Task<IReadOnlyList<WeaoExploit>> GetWindowsExploitsAsync(CancellationToken token = default)
        {
            const string LOG_IDENT = "WeaoClient::GetWindowsExploitsAsync";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, EXPLOITS_ENDPOINT);
                request.Headers.UserAgent.ParseAdd(USER_AGENT);

                using var response = await App.HttpClient.SendAsync(request, token);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var all = await JsonSerializer.DeserializeAsync<List<WeaoExploit>>(
                    stream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    token);

                if (all is null)
                    return Array.Empty<WeaoExploit>();

                // Only surface Windows exploits that aren't hidden AND have a real hash.
                // Sort by title for a predictable dropdown.
                return all
                    .Where(e => !e.Hidden
                                && string.Equals(e.Platform, "Windows", StringComparison.OrdinalIgnoreCase)
                                && VersionGuidValidator.IsWellFormed(e.RbxVersion))
                    .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return Array.Empty<WeaoExploit>();
            }
        }
    }
}
