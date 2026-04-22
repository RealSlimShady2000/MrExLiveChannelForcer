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
    }
}
