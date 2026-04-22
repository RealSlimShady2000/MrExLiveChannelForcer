using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

using MrExStrap.RobloxInterfaces;
using MrExStrap.UI.Elements.About;

namespace MrExStrap.UI.ViewModels.Installer
{
    public enum ChannelLockState
    {
        Locked,
        Overridden
    }

    public class LaunchMenuViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        public ICommand LaunchSettingsCommand => new RelayCommand(LaunchSettings);

        public ICommand LaunchRobloxCommand => new RelayCommand(LaunchRoblox);

        public ICommand LaunchRobloxStudioCommand => new RelayCommand(LaunchRobloxStudio);

        public ICommand LaunchAboutCommand => new RelayCommand(LaunchAbout);

        public event EventHandler<NextAction>? CloseWindowRequest;

        // Computed once at construction; reflects the Roblox-side channel registry at the
        // moment the launch menu opens. If Overridden, the bootstrapper will still force
        // LIVE when the user clicks Launch Roblox — the chip just reports current state.
        public ChannelLockState ChannelLockState { get; } = DetectChannelLockState();

        public string ChannelLockText => ChannelLockState switch
        {
            ChannelLockState.Locked => "CHANNEL: LIVE (locked)",
            ChannelLockState.Overridden => "CHANNEL: will be forced to LIVE on launch",
            _ => "CHANNEL: LIVE (locked)"
        };

        public Brush ChannelLockBackground => ChannelLockState switch
        {
            ChannelLockState.Locked => new SolidColorBrush(Color.FromRgb(0x1E, 0x6B, 0x2E)),      // green
            ChannelLockState.Overridden => new SolidColorBrush(Color.FromRgb(0x8A, 0x6B, 0x17)),  // amber
            _ => new SolidColorBrush(Color.FromRgb(0x1E, 0x6B, 0x2E))
        };

        private static ChannelLockState DetectChannelLockState()
        {
            const string LOG_IDENT = "LaunchMenuViewModel::DetectChannelLockState";
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                    $"SOFTWARE\\ROBLOX Corporation\\Environments\\RobloxPlayer\\Channel",
                    writable: false);
                string? value = key?.GetValue("www.roblox.com") as string;

                if (string.IsNullOrEmpty(value)
                    || string.Equals(value, Deployment.DefaultChannel, StringComparison.OrdinalIgnoreCase))
                {
                    return ChannelLockState.Locked;
                }

                return ChannelLockState.Overridden;
            }
            catch (Exception ex)
            {
                // Read errors are non-fatal. The bootstrapper will still force LIVE at launch;
                // the chip just optimistically shows Locked if we can't read the key.
                App.Logger.WriteException(LOG_IDENT, ex);
                return ChannelLockState.Locked;
            }
        }

        private void LaunchSettings() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings);

        private void LaunchRoblox() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRoblox);

        private void LaunchRobloxStudio() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRobloxStudio);

        private void LaunchAbout() => new MainWindow().ShowDialog();
    }
}
