using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using MrExStrap.Models;
using MrExStrap.Models.Persistable;
using MrExStrap.Utility.Accounts;

namespace MrExStrap.UI.ViewModels.Settings
{
    // One row in the account list. Wraps a saved RobloxAccount and adds the bulk-launch selection
    // checkbox state without polluting the persisted model.
    public class AccountRow : NotifyPropertyChangedViewModel
    {
        public RobloxAccount Account { get; }

        public AccountRow(RobloxAccount account) => Account = account;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string Id => Account.Id;
        public string DisplayLabel => Account.DisplayLabel;
        public string UsernameLine => string.IsNullOrEmpty(Account.Username) ? "" : "@" + Account.Username;
        public string? AvatarUrl => Account.AvatarUrl;
    }

    public class MultiInstanceViewModel : NotifyPropertyChangedViewModel
    {
        private const string LOG_IDENT = "MultiInstanceViewModel";

        public ObservableCollection<AccountRow> Accounts { get; } = new();

        public ObservableCollection<RobloxInstanceInfo> RunningInstances { get; } = new();

        public MultiInstanceViewModel()
        {
            ReloadAccounts();
            RefreshRunningInstances();
        }

        // ---- Accounts ----

        public string AccountsHeader => Accounts.Count switch
        {
            0 => "Accounts (none yet)",
            1 => "Accounts (1)",
            _ => $"Accounts ({Accounts.Count})"
        };

        public bool HasNoAccounts => Accounts.Count == 0;

        public ICommand AddAccountCommand => new RelayCommand(AddAccount);
        public ICommand RemoveAccountCommand => new RelayCommand<AccountRow>(RemoveAccount);
        public ICommand LaunchAccountCommand => new AsyncRelayCommand<AccountRow>(LaunchAccountAsync);

        private void ReloadAccounts()
        {
            Accounts.Clear();
            foreach (var account in AccountManager.All)
                Accounts.Add(new AccountRow(account));

            OnPropertyChanged(nameof(AccountsHeader));
            OnPropertyChanged(nameof(HasNoAccounts));
        }

        private void AddAccount()
        {
            var dialog = new UI.Elements.Dialogs.AddAccountDialog();
            dialog.ShowDialog();

            if (dialog.CreatedAccount is null)
                return;

            AccountManager.Add(dialog.CreatedAccount);
            ReloadAccounts();
            BulkStatus = $"Added {dialog.CreatedAccount.DisplayLabel}.";
        }

        private void RemoveAccount(AccountRow? row)
        {
            if (row is null)
                return;

            var confirm = Frontend.ShowMessageBox(
                $"Remove {row.DisplayLabel} from MrExBloxstrap?\n\nThis only deletes the saved login on this PC. The Roblox account itself is untouched.",
                MessageBoxImage.Question, MessageBoxButton.YesNo, MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes)
                return;

            AccountManager.Remove(row.Id);
            ReloadAccounts();
        }

        private async Task LaunchAccountAsync(AccountRow? row)
        {
            if (row is null)
                return;

            if (!TryGetPlaceId(out long placeId))
                return;

            BulkStatus = $"Launching {row.DisplayLabel}…";
            bool ok = await AccountLauncher.LaunchAsync(row.Account, placeId, NormalizedJobId);
            BulkStatus = ok
                ? $"Launched {row.DisplayLabel}."
                : $"Couldn't launch {row.DisplayLabel} — the saved login may have expired. Re-add it.";
            RefreshRunningInstances();
        }

        // ---- Bulk launch ----

        public string BulkPlaceId
        {
            get => App.Settings.Prop.LastBulkPlaceId;
            set { App.Settings.Prop.LastBulkPlaceId = (value ?? "").Trim(); OnPropertyChanged(nameof(BulkPlaceId)); }
        }

        public string BulkJobId
        {
            get => App.Settings.Prop.LastBulkJobId;
            set { App.Settings.Prop.LastBulkJobId = (value ?? "").Trim(); OnPropertyChanged(nameof(BulkJobId)); }
        }

        public int BulkLaunchDelaySeconds
        {
            get => App.Settings.Prop.BulkLaunchDelaySeconds;
            set { App.Settings.Prop.BulkLaunchDelaySeconds = Math.Max(2, value); OnPropertyChanged(nameof(BulkLaunchDelaySeconds)); }
        }

        public bool MultiInstanceEnabled
        {
            get => App.Settings.Prop.MultiInstanceEnabled;
            set { App.Settings.Prop.MultiInstanceEnabled = value; OnPropertyChanged(nameof(MultiInstanceEnabled)); }
        }

        private string _bulkStatus = "";
        public string BulkStatus
        {
            get => _bulkStatus;
            set { _bulkStatus = value; OnPropertyChanged(nameof(BulkStatus)); OnPropertyChanged(nameof(HasBulkStatus)); }
        }
        public bool HasBulkStatus => !string.IsNullOrEmpty(_bulkStatus);

        private bool _isLaunching;
        public bool IsLaunching
        {
            get => _isLaunching;
            private set { _isLaunching = value; OnPropertyChanged(nameof(IsLaunching)); OnPropertyChanged(nameof(IsNotLaunching)); }
        }
        public bool IsNotLaunching => !_isLaunching;

        public ICommand LaunchSelectedCommand => new AsyncRelayCommand(LaunchSelectedAsync);
        public ICommand LaunchAllCommand => new AsyncRelayCommand(LaunchAllAsync);

        private async Task LaunchSelectedAsync()
        {
            var selected = Accounts.Where(a => a.IsSelected).Select(a => a.Account).ToList();
            if (selected.Count == 0)
            {
                BulkStatus = "Tick the accounts you want to launch first.";
                return;
            }
            await BulkLaunchAsync(selected);
        }

        private async Task LaunchAllAsync()
        {
            var all = Accounts.Select(a => a.Account).ToList();
            if (all.Count == 0)
            {
                BulkStatus = "Add some accounts first.";
                return;
            }
            await BulkLaunchAsync(all);
        }

        private async Task BulkLaunchAsync(IReadOnlyList<RobloxAccount> accounts)
        {
            if (IsLaunching)
                return;
            if (!TryGetPlaceId(out long placeId))
                return;

            IsLaunching = true;
            try
            {
                var progress = new Progress<string>(s => BulkStatus = s);
                int delay = App.Settings.Prop.BulkLaunchDelaySeconds;
                await AccountLauncher.BulkLaunchAsync(accounts, placeId, NormalizedJobId, delay, progress);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::BulkLaunch", ex);
                BulkStatus = $"Bulk launch failed ({ex.GetType().Name}).";
            }
            finally
            {
                IsLaunching = false;
                RefreshRunningInstances();
            }
        }

        private string? NormalizedJobId => string.IsNullOrWhiteSpace(BulkJobId) ? null : BulkJobId.Trim();

        private bool TryGetPlaceId(out long placeId)
        {
            placeId = 0;
            string raw = (BulkPlaceId ?? "").Trim();
            if (long.TryParse(raw, out placeId) && placeId > 0)
                return true;

            BulkStatus = "Enter a valid Place ID (the number in the game's URL).";
            return false;
        }

        // ---- Running instances (relocated from the Settings page) ----

        public string RunningInstancesHeader => RunningInstances.Count switch
        {
            0 => "Running Roblox instances (none)",
            1 => "Running Roblox instances (1)",
            _ => $"Running Roblox instances ({RunningInstances.Count})"
        };

        public bool HasNoRunningInstances => RunningInstances.Count == 0;

        public ICommand RefreshRunningInstancesCommand => new RelayCommand(RefreshRunningInstances);
        public ICommand KillInstanceCommand => new RelayCommand<int>(KillInstance);

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
                    try { title = GetMainWindowTitle(p.Id); }
                    catch { }

                    RunningInstances.Add(new RobloxInstanceInfo(p.Id, uptime, memMb, title));
                    p.Dispose();
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::RefreshRunningInstances", ex);
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
                App.Logger.WriteException(LOG_IDENT + "::KillInstance", ex);
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
    }
}
