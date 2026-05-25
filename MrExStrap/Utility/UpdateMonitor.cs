using MrExStrap.RobloxInterfaces;
using WinForms = System.Windows.Forms;

namespace MrExStrap.Utility
{
    // v420.28: opt-in background checks that fire Windows balloon-tip toasts
    // when (a) Roblox's LIVE channel hash moves to a new build or (b) any
    // executor with a tracked profile updates on WEAO.
    //
    // Run-points:
    //   - LaunchMenu open (fire-and-forget so the menu doesn't block on it).
    //   - Tray launcher's 30-minute timer when the tray feature is enabled.
    //
    // Both checks are bounded by a 3-second cancellation token so a slow CDN
    // or WEAO outage never blocks the launcher or wedges the tray timer.
    // Failures are logged but never surface to the user — toast spam from
    // monitoring telemetry would be worse than the missed update.
    public static class UpdateMonitor
    {
        private const string LOG_IDENT = "UpdateMonitor";
        private static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(3);

        // Runs the LIVE and executor checks in parallel. Individual failures
        // don't suppress the other check.
        public static async Task CheckAllAsync()
        {
            var liveTask = CheckLiveAsync();
            var exTask = CheckExecutorsAsync();
            await Task.WhenAll(liveTask, exTask);
        }

        public static async Task CheckLiveAsync()
        {
            if (!App.Settings.Prop.NotifyOnLiveChange)
                return;

            using var cts = new CancellationTokenSource(DefaultBudget);
            try
            {
                var info = await Deployment.GetInfo();
                string currentHash = info.VersionGuid;
                if (string.IsNullOrEmpty(currentHash))
                    return;

                string last = App.State.Prop.LastNotifiedLiveHash ?? "";
                if (string.Equals(currentHash, last, StringComparison.OrdinalIgnoreCase))
                    return;

                // First-ever notification: don't pop a toast — just record the
                // current hash silently. The toast is for *changes*, and on a
                // brand-new install we don't know whether the current hash is
                // brand new or has been live for days.
                if (string.IsNullOrEmpty(last))
                {
                    App.State.Prop.LastNotifiedLiveHash = currentHash;
                    App.State.Save();
                    App.Logger.WriteLine(LOG_IDENT, $"Seeded LastNotifiedLiveHash with {currentHash} (no toast on first observation).");
                    return;
                }

                App.Logger.WriteLine(LOG_IDENT, $"LIVE hash changed: {last} -> {currentHash}. Firing toast.");
                LiveChannelToast.ShowToast(
                    title: "Roblox just shipped a new build",
                    message: $"LIVE is now {currentHash}. Your executor may need to update — check the Versions Manager.",
                    icon: WinForms.ToolTipIcon.Info);

                App.State.Prop.LastNotifiedLiveHash = currentHash;
                App.State.Save();
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"LIVE check exceeded {DefaultBudget.TotalSeconds:F1}s budget.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::CheckLiveAsync", ex);
            }
        }

        public static async Task CheckExecutorsAsync()
        {
            if (!App.Settings.Prop.NotifyOnExecutorUpdate)
                return;

            var trackedProfiles = App.Settings.Prop.VersionProfiles
                .Where(p => !string.IsNullOrWhiteSpace(p.ExecutorRefreshKey))
                .ToArray();
            if (trackedProfiles.Length == 0)
                return;

            using var cts = new CancellationTokenSource(DefaultBudget);
            try
            {
                var result = await WeaoClient.GetWindowsExploitsAsync(cts.Token);
                if (!result.Success || result.Exploits.Count == 0)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Executor check skipped: {result.Error ?? "empty list"}");
                    return;
                }

                bool any = false;
                foreach (var profile in trackedProfiles)
                {
                    var match = result.Exploits.FirstOrDefault(e =>
                        string.Equals(e.Title, profile.ExecutorRefreshKey, StringComparison.OrdinalIgnoreCase));
                    if (match is null)
                        continue;
                    if (!VersionGuidValidator.IsWellFormed(match.RbxVersion))
                        continue;

                    string last = profile.LastNotifiedExecutorHash ?? "";

                    // First-ever notification for this profile: seed silently.
                    if (string.IsNullOrEmpty(last))
                    {
                        profile.LastNotifiedExecutorHash = match.RbxVersion;
                        any = true;
                        continue;
                    }

                    if (string.Equals(last, match.RbxVersion, StringComparison.OrdinalIgnoreCase))
                        continue;

                    App.Logger.WriteLine(LOG_IDENT, $"Executor '{match.Title}' updated: {last} -> {match.RbxVersion}. Firing toast.");
                    LiveChannelToast.ShowToast(
                        title: $"{match.Title} just updated",
                        message: $"Now on {match.RbxVersion}. MrExBloxstrap applies the new version on the profile's next launch.",
                        icon: WinForms.ToolTipIcon.Info);
                    profile.LastNotifiedExecutorHash = match.RbxVersion;
                    any = true;
                }

                if (any)
                    App.Settings.Save();
            }
            catch (OperationCanceledException)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Executor check exceeded {DefaultBudget.TotalSeconds:F1}s budget.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::CheckExecutorsAsync", ex);
            }
        }
    }
}
