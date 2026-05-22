using System.Net.Http;

namespace MrExStrap.Utility
{
    // Shared auto-update helpers used by both the Roblox-launch path (Bootstrapper.CheckForUpdates)
    // and the menu-open path (LaunchHandler.LaunchMenu). Keeps the download + relaunch contract
    // in one place so the two callers can't drift apart.
    public static class AppUpdater
    {
        private const string LOG_IDENT = "AppUpdater";

        // Common gate. Returns false if the auto-update path is disabled for this session:
        //   - the user turned off CheckForUpdates,
        //   - we're already mid-upgrade (-upgrade flag passed),
        //   - we're in portable mode (the user updates by re-downloading the portable folder),
        //   - or there's another MrExBloxstrap instance running (likely a background watcher).
        public static bool IsAutoUpdateEligible()
        {
            if (!App.Settings.Prop.CheckForUpdates)
                return false;
            if (App.LaunchSettings.UpgradeFlag.Active)
                return false;
            if (App.IsPortableMode)
                return false;
            if (Process.GetProcessesByName(App.ProjectName).Length > 1)
                return false;
            return true;
        }

        // Pick the installer exe from a release's assets. Built explicitly because GitHub returns
        // assets in upload order, and historically the portable zip used to sit ahead of the exe —
        // grabbing Assets[0] would download the wrong file.
        public static GithubReleaseAsset? PickExeAsset(GithubRelease release) =>
            release.Assets?.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        // Downloads the .exe asset of the release to Paths.TempUpdates and starts it with the
        // given args (a "-upgrade" is prepended automatically). Returns true if the new process
        // was started — the caller MUST exit on true so the new exe can take over.
        // Returns false on any failure (already logged); caller falls through to its normal flow.
        public static async Task<bool> DownloadAndRelaunchAsync(GithubRelease release, IEnumerable<string> extraArgs)
        {
            try
            {
                var asset = PickExeAsset(release);
                if (asset is null)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"No .exe asset on release {release.TagName} — cannot auto-update.");
                    return false;
                }

                string downloadLocation = Path.Combine(Paths.TempUpdates, asset.Name);
                Directory.CreateDirectory(Paths.TempUpdates);

                App.Logger.WriteLine(LOG_IDENT, $"Downloading {release.TagName} from {asset.BrowserDownloadUrl}");

                if (!File.Exists(downloadLocation))
                {
                    using var response = await App.HttpClient.GetAsync(asset.BrowserDownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fileStream = new FileStream(downloadLocation, FileMode.OpenOrCreate, FileAccess.Write);
                    await response.Content.CopyToAsync(fileStream);
                }

                App.Logger.WriteLine(LOG_IDENT, $"Starting {release.TagName} from {downloadLocation}");

                var psi = new ProcessStartInfo { FileName = downloadLocation };
                psi.ArgumentList.Add("-upgrade");
                foreach (var arg in extraArgs)
                    psi.ArgumentList.Add(arg);

                // Persist settings before we hand off so the new process sees the latest state.
                App.Settings.Save();

                // Singleton lock so the new exe waits if anything else is mid-update.
                new InterProcessLock("AutoUpdater");

                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::DownloadAndRelaunchAsync", ex);
                return false;
            }
        }
    }
}
