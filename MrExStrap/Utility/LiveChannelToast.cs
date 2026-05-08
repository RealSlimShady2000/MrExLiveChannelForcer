using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace MrExStrap.Utility
{
    public static class LiveChannelToast
    {
        private const string LOG_IDENT = "LiveChannelToast";

        // Callable from any thread. Dispatches to the WPF UI thread, then shows a transient
        // balloon tip / toast via a short-lived NotifyIcon. If notifications are disabled
        // system-wide, ShowBalloonTip silently no-ops.
        public static void Show()
        {
            if (App.Settings?.Prop?.ShowLiveChannelToast == false)
                return;

            ShowToast(
                title: "Channel: LIVE",
                message: $"Roblox launched on the LIVE channel. Enforced by {App.ProjectName}. You can disable this notification in settings.",
                icon: WinForms.ToolTipIcon.Info);
        }

        // Failure path: the registry write or read-back didn't agree on LIVE. Always shown,
        // even if the success toast is disabled, because the user needs to know the lock
        // promise wasn't kept this launch.
        public static void ShowChannelLockFailed()
        {
            ShowToast(
                title: "Channel lock could not be verified",
                message: "Roblox may have launched on a non-LIVE channel. Antivirus, a Roblox manager app, or another tool may be overwriting the channel registry key. Check the log for details.",
                icon: WinForms.ToolTipIcon.Warning);
        }

        private static void ShowToast(string title, string message, WinForms.ToolTipIcon icon)
        {
            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            try
            {
                dispatcher.InvokeAsync(() => ShowOnUiThread(title, message, icon), DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::ShowToast", ex);
            }
        }

        private static void ShowOnUiThread(string title, string message, WinForms.ToolTipIcon icon)
        {
            WinForms.NotifyIcon? notifyIcon = null;
            try
            {
                notifyIcon = new WinForms.NotifyIcon
                {
                    Icon = Properties.Resources.IconBloxstrap,
                    Visible = true,
                    BalloonTipTitle = title,
                    BalloonTipText = message,
                    BalloonTipIcon = icon
                };
                notifyIcon.ShowBalloonTip(5000);

                // Keep the NotifyIcon alive long enough for the balloon to naturally dismiss,
                // then dispose so we don't leak a tray slot. 10s is generous — most balloons
                // auto-dismiss in 5s on Win10+.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                WinForms.NotifyIcon captured = notifyIcon;
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    try
                    {
                        captured.Visible = false;
                        captured.Dispose();
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT + "::Dispose", ex);
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::ShowOnUiThread", ex);
                try { notifyIcon?.Dispose(); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
