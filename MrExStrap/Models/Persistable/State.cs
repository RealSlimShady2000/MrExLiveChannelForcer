using System.Collections.Generic;

namespace MrExStrap.Models.Persistable
{
    public class State
    {
        public bool PromptWebView2Install { get; set; } = true;

        public bool ForceReinstall { get; set; } = false;

        public WindowState SettingsWindow { get; set; } = new();

        // Recent hashes the user has verified/pinned (MrExStrap fork feature).
        // Newest first. Capped at 10 via the push helper.
        public List<string> RecentCustomVersionHashes { get; set; } = new();

        // LIVE version the user has already seen. Used to detect when Roblox
        // has shipped a new build since the last open so we can show a banner.
        public string DismissedLiveHash { get; set; } = "";

        // Hash → first-seen UTC time. Each LIVE hash this install observes gets
        // timestamped here the first time we see it. Useful because the CDN's
        // Last-Modified header reflects when the package was uploaded — often many
        // hours before Roblox flips the LIVE pointer to this build.
        public Dictionary<string, DateTime> LiveHashFirstSeenUtc { get; set; } = new();

        // v420.28+: last LIVE hash a "Roblox just shipped X" toast was fired for.
        // Stops the same toast from re-firing every launcher open / tray poll once
        // the user has already seen it. Updated atomically with the toast call.
        public string LastNotifiedLiveHash { get; set; } = "";

        // v420.29.5+: last MrExBloxstrap release tag a "new version available" toast
        // was fired for. Stops the same toast re-firing every launcher open / tray poll.
        // Seeded silently on first observation so we never toast just for noticing the
        // current installed version.
        public string LastNotifiedAppVersion { get; set; } = "";
    }
}
