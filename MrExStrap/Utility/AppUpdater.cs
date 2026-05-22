using System.Net.Http;

namespace MrExStrap.Utility
{
    // Shared auto-update helpers used by both the Roblox-launch path (Bootstrapper.CheckForUpdates)
    // and the menu-open path (LaunchHandler.LaunchMenu). Keeps the download + relaunch contract
    // in one place so the two callers can't drift apart.
    public static class AppUpdater
    {
        private const string LOG_IDENT = "AppUpdater";

        // Reported as (bytes downloaded so far, total bytes if known else 0).
        public readonly record struct DownloadProgress(long Downloaded, long Total);

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
        // given args ("-upgrade" is prepended automatically). Returns true if the new process
        // was started — the caller MUST exit on true so the new exe can take over.
        // Returns false on any failure (already logged); caller falls through to its normal flow.
        public static async Task<bool> DownloadAndRelaunchAsync(
            GithubRelease release,
            IEnumerable<string> extraArgs,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
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

                // Always truncate. The previous behaviour was to skip when the file existed,
                // which silently ran a partial exe if a previous download was interrupted.
                if (File.Exists(downloadLocation))
                {
                    try { File.Delete(downloadLocation); }
                    catch (Exception ex) { App.Logger.WriteException(LOG_IDENT + "::PreDelete", ex); }
                }

                App.Logger.WriteLine(LOG_IDENT, $"Downloading {release.TagName} from {asset.BrowserDownloadUrl}");

                using var response = await App.HttpClient.GetAsync(
                    asset.BrowserDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? 0;
                progress?.Report(new DownloadProgress(0, total));

                await using var fileStream = new FileStream(downloadLocation, FileMode.Create, FileAccess.Write);
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int lastReportedPercent = -1;

                while (true)
                {
                    int read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0)
                        break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;

                    if (progress != null)
                    {
                        // Throttle: only report when the integer percent changes (or every chunk if
                        // total is unknown). Avoids hammering the UI dispatcher on a fast LAN.
                        if (total > 0)
                        {
                            int pct = (int)(totalRead * 100 / total);
                            if (pct != lastReportedPercent)
                            {
                                progress.Report(new DownloadProgress(totalRead, total));
                                lastReportedPercent = pct;
                            }
                        }
                        else
                        {
                            progress.Report(new DownloadProgress(totalRead, 0));
                        }
                    }
                }

                // Verify the downloaded bytes match the advertised Content-Length. A partial
                // response that ends cleanly (bad proxy, dropped connection that the stream
                // didn't surface as an error) would otherwise hand off a truncated exe and
                // we'd App.Terminate this process before the new one fails to start.
                if (total > 0 && totalRead != total)
                {
                    App.Logger.WriteLine(LOG_IDENT,
                        $"Download size mismatch for {asset.Name}: expected {total}, got {totalRead}. Aborting relaunch.");
                    try { File.Delete(downloadLocation); } catch { /* leave the truncated file, next attempt will overwrite */ }
                    return false;
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
