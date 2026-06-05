namespace MrExStrap.Utility.Accounts
{
    // Launch technique adapted from robloxmanager by sasha / centerepic (MIT) —
    // https://gitlab.com/centerepic/robloxmanager. Each launch mints a fresh one-time ticket from
    // the stored cookie (see RobloxAuth) and re-enters our OWN bootstrapper through the
    // roblox-player: protocol, so every alt still gets the LIVE-channel lock, FastFlags, mods,
    // privacy mode, the singleton-close and window tiling.
    public static class AccountLauncher
    {
        private const string LOG_IDENT = "AccountLauncher";

        // Launch one account into a place (and optionally a specific server job). Returns true if a
        // launch process was spawned.
        public static async Task<bool> LaunchAsync(RobloxAccount account, long placeId, string? jobId)
        {
            string? cookie = SecureStore.Unprotect(account.EncryptedCookieB64);
            if (string.IsNullOrEmpty(cookie))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not decrypt the cookie for {account.DisplayLabel}; skipping.");
                return false;
            }

            string? ticket = await RobloxAuth.GetAuthTicketAsync(cookie);
            if (string.IsNullOrEmpty(ticket))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Could not get a launch ticket for {account.DisplayLabel} (cookie expired?).");
                return false;
            }

            string uri = BuildLaunchUri(ticket, placeId, jobId);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Paths.Application,
                    Arguments = uri,
                    UseShellExecute = false,
                    WorkingDirectory = Paths.Base
                });
                App.Logger.WriteLine(LOG_IDENT, $"Launched {account.DisplayLabel} into place {placeId}{(string.IsNullOrEmpty(jobId) ? "" : $" job {jobId}")}.");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        // Sequential bulk launch with a per-account delay. The delay is REQUIRED, not cosmetic:
        //   - the log file is named to the second, so two launches in the same second collide and
        //     the later MrExBloxstrap self-terminates as a "duplicate launch";
        //   - each Roblox client's ROBLOX_singletonEvent is closed ~4s after it starts, and the
        //     next client can't start as a separate instance until that happens;
        //   - it also throttles the auth-ticket calls for rate-limited IPs.
        // Minimum is clamped to 2s. Also ensures multi-instance is enabled (otherwise alts can't
        // coexist). Returns how many launched.
        public static async Task<int> BulkLaunchAsync(IReadOnlyList<RobloxAccount> accounts, long placeId, string? jobId, int delaySeconds, IProgress<string>? progress = null)
        {
            if (!App.Settings.Prop.MultiInstanceEnabled)
            {
                App.Settings.Prop.MultiInstanceEnabled = true;
                App.Settings.Save();
                App.Logger.WriteLine(LOG_IDENT, "Enabled multi-instance for bulk launch.");
            }

            delaySeconds = Math.Max(2, delaySeconds);
            int launched = 0;

            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                progress?.Report($"Launching {account.DisplayLabel} ({i + 1}/{accounts.Count})…");

                if (await LaunchAsync(account, placeId, jobId))
                    launched++;

                if (i < accounts.Count - 1)
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }

            progress?.Report($"Done — launched {launched} of {accounts.Count}.");
            App.Logger.WriteLine(LOG_IDENT, $"Bulk launch finished: {launched}/{accounts.Count}.");
            return launched;
        }

        private static string BuildLaunchUri(string ticket, long placeId, string? jobId)
        {
            long launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // The placelauncherurl is percent-encoded so its inner &/= don't break the +-delimited
            // outer protocol string. RequestGame joins any/new server; RequestGameJob targets a
            // specific server so a group of alts lands together.
            string placeLauncher = string.IsNullOrEmpty(jobId)
                ? $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId=0&placeId={placeId}&isPlayTogetherGame=false"
                : $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId=0&placeId={placeId}&gameId={jobId}&isPlayTogetherGame=false";

            string encoded = Uri.EscapeDataString(placeLauncher);
            long tracker = Random.Shared.NextInt64(10_000_000, 200_000_000);

            return $"roblox-player:1+launchmode:play+gameinfo:{ticket}+launchtime:{launchTime}"
                 + $"+placelauncherurl:{encoded}+browsertrackerid:{tracker}"
                 + "+robloxLocale:en_us+gameLocale:en_us+channel:";
        }
    }
}
