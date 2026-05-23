namespace MrExStrap.Models.Persistable
{
    // A single saved Roblox version slot. The user can have many of these — one per
    // executor they care about, plus a built-in "Latest LIVE" sentinel — and switches
    // between them with one click on the Versions Manager tab.
    //
    // VersionGuid == "" is special: it means "always use the current LIVE build", so
    // the built-in profile doesn't have to be re-pinned every time Roblox ships a new
    // production hash.
    public class VersionProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";

        // Empty means "always use the current LIVE hash from Roblox CDN". For pinned
        // profiles this is a well-formed "version-<16 hex>" string.
        public string VersionGuid { get; set; } = "";

        // Populated when the profile was created via the WEAO executor dropdown so
        // we can show the title in the tile and (later) periodically check whether
        // the upstream executor has updated to a newer Roblox build.
        public string? ExecutorTitle { get; set; }

        // WEAO CDN logo URL (slug.logo). The Versions Manager tile fetches and caches
        // this through Utility.ExecutorLogoCache on first display.
        public string? ExecutorLogoUrl { get; set; }

        // Locked from edit/delete in the UI. The seed "Latest LIVE" profile sets this
        // so users can't accidentally remove the always-LIVE fallback.
        public bool IsBuiltIn { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
