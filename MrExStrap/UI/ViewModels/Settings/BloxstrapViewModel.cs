using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;

using MrExStrap.Models;
using MrExStrap.UI;

namespace MrExStrap.UI.ViewModels.Settings
{
    public class BloxstrapViewModel : NotifyPropertyChangedViewModel
    {
        public WebEnvironment[] WebEnvironments => Enum.GetValues<WebEnvironment>();

        public bool UpdateCheckingEnabled
        {
            get => App.Settings.Prop.CheckForUpdates;
            set => App.Settings.Prop.CheckForUpdates = value;
        }

        public bool LiveChannelToastEnabled
        {
            get => App.Settings.Prop.ShowLiveChannelToast;
            set => App.Settings.Prop.ShowLiveChannelToast = value;
        }

        public bool PrivacyModeEnabled
        {
            get => App.Settings.Prop.EnablePrivacyMode;
            set => App.Settings.Prop.EnablePrivacyMode = value;
        }

        public bool MultiInstanceEnabled
        {
            get => App.Settings.Prop.MultiInstanceEnabled;
            set => App.Settings.Prop.MultiInstanceEnabled = value;
        }

        public bool WindowTilingEnabled
        {
            get => App.Settings.Prop.WindowTilingEnabled;
            set => App.Settings.Prop.WindowTilingEnabled = value;
        }

        public WindowTilingLayout[] WindowTilingLayouts => Enum.GetValues<WindowTilingLayout>();

        public WindowTilingLayout WindowTilingLayout
        {
            get => App.Settings.Prop.WindowTilingLayout;
            set => App.Settings.Prop.WindowTilingLayout = value;
        }

        public bool DebugModeEnabled
        {
            get => App.Settings.Prop.DebugModeEnabled;
            set
            {
                App.Settings.Prop.DebugModeEnabled = value;
                OnPropertyChanged(nameof(DebugModeEnabled));
                OnPropertyChanged(nameof(DebugModeVisibility));
            }
        }

        public Visibility DebugModeVisibility => App.Settings.Prop.DebugModeEnabled ? Visibility.Visible : Visibility.Collapsed;

        public ICommand RunHealthCheckCommand => new RelayCommand(RunHealthCheck);

        public ICommand OpenLogFolderCommand => new RelayCommand(OpenLogFolder);

        private void RunHealthCheck()
        {
            var dialog = new UI.Elements.Dialogs.HealthCheckDialog();
            dialog.ShowDialog();
        }

        private void OpenLogFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(Paths.Logs) && Directory.Exists(Paths.Logs))
                    Process.Start(new ProcessStartInfo { FileName = Paths.Logs, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BloxstrapViewModel::OpenLogFolder", ex);
            }
        }

        public WebEnvironment WebEnvironment
        {
            get => App.Settings.Prop.WebEnvironment;
            set => App.Settings.Prop.WebEnvironment = value;
        }

        public Visibility WebEnvironmentVisibility => App.Settings.Prop.DeveloperMode ? Visibility.Visible : Visibility.Collapsed;

        public bool ShouldExportConfig { get; set; } = true;

        public bool ShouldExportLogs { get; set; } = true;

        public ICommand ExportDataCommand => new RelayCommand(ExportData);

        public ICommand ClearRobloxCacheCommand => new AsyncRelayCommand(ClearRobloxCacheAsync);

        public ObservableCollection<RobloxInstanceInfo> RunningInstances { get; } = new();

        public bool HasNoRunningInstances => RunningInstances.Count == 0;

        public string RunningInstancesHeader => RunningInstances.Count switch
        {
            0 => "Running Roblox instances (none)",
            1 => "Running Roblox instances (1)",
            _ => $"Running Roblox instances ({RunningInstances.Count})"
        };

        public ICommand RefreshRunningInstancesCommand => new RelayCommand(RefreshRunningInstances);
        public ICommand KillInstanceCommand => new RelayCommand<int>(KillInstance);

        public BloxstrapViewModel()
        {
            RefreshRunningInstances();
        }

        private void RefreshRunningInstances()
        {
            RunningInstances.Clear();

            try
            {
                foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
                {
                    string uptime;
                    long memMb = 0;
                    try
                    {
                        uptime = FormatUptime(DateTime.Now - p.StartTime);
                        memMb = p.WorkingSet64 / 1024 / 1024;
                    }
                    catch
                    {
                        uptime = "?";
                    }

                    string title = "";
                    try
                    {
                        title = GetMainWindowTitle(p.Id);
                    }
                    catch { }

                    RunningInstances.Add(new RobloxInstanceInfo(p.Id, uptime, memMb, title));
                    p.Dispose();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BloxstrapViewModel::RefreshRunningInstances", ex);
            }

            OnPropertyChanged(nameof(HasNoRunningInstances));
            OnPropertyChanged(nameof(RunningInstancesHeader));
        }

        private void KillInstance(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                proc.Kill();
                proc.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("BloxstrapViewModel::KillInstance", ex);
            }
            RefreshRunningInstances();
        }

        private static string FormatUptime(TimeSpan t)
        {
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{(int)t.TotalMinutes}m {t.Seconds}s";
            return $"{(int)t.TotalSeconds}s";
        }

        private static string GetMainWindowTitle(int pid)
        {
            string result = "";
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint winPid);
                if ((int)winPid != pid) return true;
                if (!IsWindowVisible(hWnd)) return true;

                int len = GetWindowTextLength(hWnd);
                if (len == 0) return true;

                var sb = new StringBuilder(len + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                result = title;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private async Task ClearRobloxCacheAsync()
        {
            const string LOG_IDENT = "BloxstrapViewModel::ClearRobloxCacheAsync";

            // Candidate cache locations. We touch the fork's own version/download cache and
            // the default-location Roblox install (when the user also runs vanilla Roblox on
            // the same machine). Settings, FastFlags, and custom themes are intentionally NOT
            // in this list — those are user config, not cache.
            var candidates = new List<string>();

            string? robloxPlayerDefault = Path.Combine(Paths.LocalAppData, "Roblox");
            string? robloxTemp = Path.Combine(Path.GetTempPath(), "Roblox");

            if (!string.IsNullOrEmpty(Paths.Versions) && Directory.Exists(Paths.Versions))
                candidates.Add(Paths.Versions);
            if (!string.IsNullOrEmpty(Paths.Downloads) && Directory.Exists(Paths.Downloads))
                candidates.Add(Paths.Downloads);
            if (Directory.Exists(robloxPlayerDefault))
                candidates.Add(robloxPlayerDefault);
            if (Directory.Exists(robloxTemp))
                candidates.Add(robloxTemp);

            if (candidates.Count == 0)
            {
                Frontend.ShowMessageBox("No Roblox cache folders were found on this machine.",
                    MessageBoxImage.Information);
                return;
            }

            string bulletList = string.Join("\n", candidates.Select(p => "- " + p));
            string prompt =
                "The following folders will be permanently deleted:\n\n" + bulletList +
                "\n\nYour Bloxstrap Mr Exploit edition settings, FastFlags, and custom themes will be kept. " +
                "Roblox and the client cache will redownload on next launch.\n\n" +
                "Continue?";

            var result = Frontend.ShowMessageBox(prompt, MessageBoxImage.Warning,
                MessageBoxButton.YesNo, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes)
                return;

            var failed = new List<string>();

            await Task.Run(() =>
            {
                foreach (var path in candidates)
                {
                    try
                    {
                        Directory.Delete(path, recursive: true);
                        App.Logger.WriteLine(LOG_IDENT, $"Deleted {path}");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException(LOG_IDENT, ex);
                        failed.Add(path);
                    }
                }
            });

            if (failed.Count == 0)
            {
                Frontend.ShowMessageBox($"Cleared {candidates.Count} folder(s) successfully.",
                    MessageBoxImage.Information);
            }
            else
            {
                string failedList = string.Join("\n", failed.Select(p => "- " + p));
                Frontend.ShowMessageBox(
                    $"Cleared {candidates.Count - failed.Count} of {candidates.Count} folder(s). " +
                    "The following were skipped (likely in use — close Roblox and try again):\n\n" +
                    failedList,
                    MessageBoxImage.Warning);
            }
        }

        private void ExportData()
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

            var dialog = new SaveFileDialog 
            { 
                FileName = $"MrExStrap-export-{timestamp}.zip",
                Filter = $"{Strings.FileTypes_ZipArchive}|*.zip" 
            };

            if (dialog.ShowDialog() != true)
                return;

            using var memStream = new MemoryStream();
            using var zipStream = new ZipOutputStream(memStream);

            if (ShouldExportConfig)
            {
                var files = new List<string>()
                {
                    App.Settings.FileLocation,
                    App.State.FileLocation,
                    App.FastFlags.FileLocation
                };

                AddFilesToZipStream(zipStream, files, "Config/");
            }

            if (ShouldExportLogs && Directory.Exists(Paths.Logs))
            {
                var files = Directory.GetFiles(Paths.Logs)
                    .Where(x => !x.Equals(App.Logger.FileLocation, StringComparison.OrdinalIgnoreCase));

                AddFilesToZipStream(zipStream, files, "Logs/");
            }

            zipStream.CloseEntry();
            zipStream.Finish();
            memStream.Position = 0;

            using var outputStream = File.OpenWrite(dialog.FileName);
            memStream.CopyTo(outputStream);

            Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
        }

        private void AddFilesToZipStream(ZipOutputStream zipStream, IEnumerable<string> files, string directory)
        {
            const string LOG_IDENT = "BloxstrapViewModel::AddFilesToZipStream";

            foreach (string file in files)
            {
                if (!File.Exists(file))
                    continue;

                try
                {
                    using FileStream fileStream = File.OpenRead(file);

                    var entry = new ZipEntry(directory + Path.GetFileName(file));
                    entry.DateTime = DateTime.Now;

                    zipStream.PutNextEntry(entry);

                    fileStream.CopyTo(zipStream);
                }
                catch (IOException ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to open '{file}'");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }
        }
    }
}
