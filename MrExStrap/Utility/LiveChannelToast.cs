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

            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher == null)
                return;

            try
            {
                dispatcher.InvokeAsync(ShowOnUiThread, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::Show", ex);
            }
        }

        private static void ShowOnUiThread()
        {
            WinForms.NotifyIcon? icon = null;
            try
            {
                icon = new WinForms.NotifyIcon
                {
                    Icon = Properties.Resources.IconBloxstrap,
                    Visible = true,
                    BalloonTipTitle = "Channel: LIVE",
                    BalloonTipText = $"Roblox launched on the LIVE channel. Enforced by {App.ProjectName}.",
                    BalloonTipIcon = WinForms.ToolTipIcon.Info
                };
                icon.ShowBalloonTip(5000);

                // Keep the NotifyIcon alive long enough for the balloon to naturally dismiss,
                // then dispose so we don't leak a tray slot. 10s is generous — most balloons
                // auto-dismiss in 5s on Win10+.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                WinForms.NotifyIcon captured = icon;
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
                try { icon?.Dispose(); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
