using System.Collections.ObjectModel;

namespace MrExStrap.Models.Persistable
{
    public class Settings
    {
        // bloxstrap configuration
        public BootstrapperStyle BootstrapperStyle { get; set; } = BootstrapperStyle.FluentDialog;
        public BootstrapperIcon BootstrapperIcon { get; set; } = BootstrapperIcon.IconBloxstrap;
        public string BootstrapperTitle { get; set; } = App.ProjectName;
        public string BootstrapperIconCustomLocation { get; set; } = "";
        public Theme Theme { get; set; } = Theme.Default;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool DeveloperMode { get; set; } = false;
        public bool CheckForUpdates { get; set; } = true;
        public bool ConfirmLaunches { get; set; } = false;
        public string Locale { get; set; } = "nil";
        public bool UseFastFlagManager { get; set; } = true;
        public bool WPFSoftwareRender { get; set; } = false;
        public bool EnableAnalytics { get; set; } = true;
        public bool BackgroundUpdatesEnabled { get; set; } = false;
        public bool DebugDisableVersionPackageCleanup { get; set; } = false;
        public string? SelectedCustomTheme { get; set; } = null;
        public WebEnvironment WebEnvironment { get; set; } = WebEnvironment.Production;

        // integration configuration
        public bool EnableActivityTracking { get; set; } = true;
        public bool UseDiscordRichPresence { get; set; } = true;
        public bool HideRPCButtons { get; set; } = true;
        public bool ShowAccountOnRichPresence { get; set; } = false;
        public bool ShowServerDetails { get; set; } = false;
        public ObservableCollection<CustomIntegration> CustomIntegrations { get; set; } = new();

        // mod preset configuration
        public bool UseDisableAppPatch { get; set; } = false;

        // version downgrade (MrExStrap fork feature)
        public bool UseCustomVersion { get; set; } = false;
        public string CustomVersionGuid { get; set; } = "";

        // post-launch "Channel: LIVE" toast (MrExStrap fork feature)
        public bool ShowLiveChannelToast { get; set; } = true;

        // privacy mode — truncate RobloxCookies.dat before every launch (MrExStrap fork feature)
        public bool EnablePrivacyMode { get; set; } = false;

        // multi-instance: close ROBLOX_singletonEvent after launch so another Roblox can start (MrExStrap fork feature)
        public bool MultiInstanceEnabled { get; set; } = false;

        // auto-tile Roblox windows in a grid once they're visible (MrExStrap fork feature)
        public bool WindowTilingEnabled { get; set; } = false;
        public WindowTilingLayout WindowTilingLayout { get; set; } = WindowTilingLayout.Auto;

        // user-visible debug mode — reveals the Run health check button (MrExStrap fork feature)
        public bool DebugModeEnabled { get; set; } = false;

        // VIP server picker — pop a WebView2 dialog before player launches and offer a free
        // shared VIP server pulled from rbxservers.xyz. Off by default. (MrExStrap fork feature)
        public bool EnableVipServerPrompt { get; set; } = false;

        // BanAsync tab — trace cleaner + MAC/MachineGuid spoofer. (MrExStrap fork feature)
        public bool BanAsyncPreserveInGameSettings { get; set; } = true;
        public bool BanAsyncPreserveFastFlags { get; set; } = true;
        public bool BanAsyncIncludeStudioFolders { get; set; } = false;
        public bool BanAsyncClearBrowserCookies { get; set; } = false;
        // Off by default in v420.11+: the netsh adapter cycle already releases the old DHCP
        // lease, so the extra ipconfig /release+/renew tends to do nothing useful and can
        // interrupt VPNs, voice chat, or captive-portal sessions. Existing users keep their
        // saved value — only the initial default changed.
        public bool BanAsyncDhcpRefreshAfterSpoof { get; set; } = false;

        // Default on. Spoofed MACs are written to HKLM\SYSTEM\...\NetworkAddress which the
        // Windows driver reads at every load, so the change naturally outlives MrExBloxstrap
        // closing, being uninstalled, or the machine rebooting. Toggle off if you want
        // MrExBloxstrap to clear the registry override on its own exit (the MAC stays applied
        // for the current Windows session and reverts on next reboot).
        public bool BanAsyncPersistent { get; set; } = true;
        public bool BanAsyncAdvancedMode { get; set; } = false;
        public bool BanAsyncOuiMirror { get; set; } = true;
        public bool BanAsyncMachineGuidAcknowledged { get; set; } = false;
        public string BanAsyncOriginalMachineGuid { get; set; } = "";
        public ObservableCollection<string> BanAsyncSpoofedAdapterGuids { get; set; } = new();
    }
}
