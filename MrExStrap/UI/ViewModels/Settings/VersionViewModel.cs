using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;

using MrExStrap.Models.APIs;
using MrExStrap.Utility;

namespace MrExStrap.UI.ViewModels.Settings
{
    public class VersionViewModel : NotifyPropertyChangedViewModel
    {
        private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xA8, 0x4A));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xA3, 0x22));
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0x3B, 0x3B));

        private string _statusMessage = "";
        private Brush _statusColor = NeutralBrush;
        private string _liveHash = "";
        private string _liveVersion = "";
        private string _liveReleasedText = "";
        private bool _isFetchingLive;
        private bool _isVerifying;
        private bool _isLoadingExploits;
        private WeaoExploit? _selectedExploit;

        public bool UseCustomVersion
        {
            get => App.Settings.Prop.UseCustomVersion;
            set
            {
                App.Settings.Prop.UseCustomVersion = value;
                OnPropertyChanged(nameof(UseCustomVersion));
                RefreshStatusFromCurrentInput();
            }
        }

        public string CustomVersionGuid
        {
            get => App.Settings.Prop.CustomVersionGuid;
            set
            {
                App.Settings.Prop.CustomVersionGuid = (value ?? "").Trim();
                OnPropertyChanged(nameof(CustomVersionGuid));
                RefreshStatusFromCurrentInput();
            }
        }

        public string LiveHash
        {
            get => _liveHash;
            private set { _liveHash = value; OnPropertyChanged(nameof(LiveHash)); OnPropertyChanged(nameof(HasLiveHash)); }
        }

        public string LiveVersion
        {
            get => _liveVersion;
            private set { _liveVersion = value; OnPropertyChanged(nameof(LiveVersion)); OnPropertyChanged(nameof(HasLiveMeta)); }
        }

        public string LiveReleasedText
        {
            get => _liveReleasedText;
            private set { _liveReleasedText = value; OnPropertyChanged(nameof(LiveReleasedText)); OnPropertyChanged(nameof(HasLiveMeta)); }
        }

        public bool HasLiveHash => VersionGuidValidator.IsWellFormed(_liveHash);

        public bool HasLiveMeta => !string.IsNullOrEmpty(_liveVersion) || !string.IsNullOrEmpty(_liveReleasedText);

        public bool IsFetchingLive
        {
            get => _isFetchingLive;
            private set { _isFetchingLive = value; OnPropertyChanged(nameof(IsFetchingLive)); OnPropertyChanged(nameof(IsNotFetchingLive)); }
        }

        public bool IsNotFetchingLive => !_isFetchingLive;

        public bool IsVerifying
        {
            get => _isVerifying;
            private set { _isVerifying = value; OnPropertyChanged(nameof(IsVerifying)); OnPropertyChanged(nameof(IsNotVerifying)); }
        }

        public bool IsNotVerifying => !_isVerifying;

        public ObservableCollection<WeaoExploit> Exploits { get; } = new();

        public WeaoExploit? SelectedExploit
        {
            get => _selectedExploit;
            set
            {
                _selectedExploit = value;
                OnPropertyChanged(nameof(SelectedExploit));

                if (value != null && VersionGuidValidator.IsWellFormed(value.RbxVersion))
                {
                    CustomVersionGuid = value.RbxVersion;
                    UseCustomVersion = true;

                    string upToDate = value.UpdateStatus ? "up-to-date" : "OUT OF DATE";
                    string detection = value.Detected ? "DETECTED" : "undetected";
                    string cost = value.Free ? "free" : "paid";
                    SetStatus(
                        $"Matched {value.Title} v{value.Version} ({cost}, {upToDate}, {detection}). Pinned to {value.RbxVersion}.",
                        value.UpdateStatus ? OkBrush : WarnBrush);
                }
            }
        }

        public bool IsLoadingExploits
        {
            get => _isLoadingExploits;
            private set { _isLoadingExploits = value; OnPropertyChanged(nameof(IsLoadingExploits)); OnPropertyChanged(nameof(IsNotLoadingExploits)); }
        }

        public bool IsNotLoadingExploits => !_isLoadingExploits;

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            private set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public ICommand FetchLiveCommand { get; }
        public ICommand PinLiveCommand { get; }
        public ICommand CopyLiveHashCommand { get; }
        public ICommand VerifyCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand RefreshExploitsCommand { get; }

        public VersionViewModel()
        {
            FetchLiveCommand = new AsyncRelayCommand(FetchLiveAsync);
            PinLiveCommand = new RelayCommand(PinLive);
            CopyLiveHashCommand = new RelayCommand(CopyLiveHash);
            VerifyCommand = new AsyncRelayCommand(VerifyAsync);
            ResetCommand = new RelayCommand(Reset);
            RefreshExploitsCommand = new AsyncRelayCommand(LoadExploitsAsync);

            RefreshStatusFromCurrentInput();

            // Fire-and-forget: auto-detect LIVE hash + populate exploit list on page open.
            _ = FetchLiveAsync();
            _ = LoadExploitsAsync();
        }

        private async Task FetchLiveAsync()
        {
            if (IsFetchingLive)
                return;

            IsFetchingLive = true;
            try
            {
                var info = await RobloxDeploymentClient.GetCurrentLiveAsync();
                if (info is null)
                {
                    LiveHash = "";
                    LiveVersion = "";
                    LiveReleasedText = "";
                    SetStatus("Couldn't reach Roblox to fetch the current LIVE version. Check your connection and click Refresh.", WarnBrush);
                    return;
                }

                LiveHash = info.Hash;
                LiveVersion = string.IsNullOrEmpty(info.Version) ? "" : $"Roblox v{info.Version}";
                LiveReleasedText = FormatReleased(info.LastModifiedUtc);

                if (!UseCustomVersion)
                    SetStatus($"Current LIVE version: {LiveHash}. Click \"Pin this version\" or pick an executor below to lock it in.", NeutralBrush);
                else
                    RefreshStatusFromCurrentInput();
            }
            catch (Exception ex)
            {
                // Belt-and-suspenders: the Utility layer catches everything today, but don't let
                // the spinner hang forever if that ever regresses.
                App.Logger.WriteException("VersionViewModel::FetchLiveAsync", ex);
                LiveHash = "";
                LiveVersion = "";
                LiveReleasedText = "";
                SetStatus("Something went wrong fetching the LIVE version. Click Refresh to try again.", ErrorBrush);
            }
            finally
            {
                IsFetchingLive = false;
            }
        }

        private async Task LoadExploitsAsync()
        {
            if (IsLoadingExploits)
                return;

            IsLoadingExploits = true;
            try
            {
                var list = await WeaoClient.GetWindowsExploitsAsync();
                Exploits.Clear();
                foreach (var e in list)
                    Exploits.Add(e);
            }
            catch (Exception ex)
            {
                // WeaoClient already returns an empty list on its own failures; this only catches
                // a surprise regression. Don't surface a status — the dropdown being empty is
                // already the visible signal, and we don't want to stomp on FetchLive's message.
                App.Logger.WriteException("VersionViewModel::LoadExploitsAsync", ex);
                Exploits.Clear();
            }
            finally
            {
                IsLoadingExploits = false;
            }
        }

        private void PinLive()
        {
            if (!HasLiveHash)
                return;

            CustomVersionGuid = LiveHash;
            UseCustomVersion = true;
            SetStatus($"Pinned to current LIVE: {LiveHash}.", OkBrush);
        }

        private void CopyLiveHash()
        {
            if (!HasLiveHash)
                return;

            try
            {
                // SetDataObject with copy=true flushes to the clipboard so the value survives
                // after this process exits. Retrying on COMException covers the case where
                // another app has the clipboard open for an instant.
                Clipboard.SetDataObject(LiveHash, true);
                SetStatus($"Copied {LiveHash} to clipboard.", OkBrush);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VersionViewModel::CopyLiveHash", ex);
                SetStatus("Couldn't copy — clipboard was busy. Try again.", WarnBrush);
            }
        }

        private async Task VerifyAsync()
        {
            if (IsVerifying)
                return;

            if (string.IsNullOrWhiteSpace(CustomVersionGuid))
            {
                SetStatus("Enter a version hash first.", WarnBrush);
                return;
            }

            if (!VersionGuidValidator.IsWellFormed(CustomVersionGuid))
            {
                SetStatus("Hash format is invalid (expected version-xxxxxxxxxxxxxxxx).", ErrorBrush);
                return;
            }

            IsVerifying = true;
            try
            {
                SetStatus($"Checking Roblox CDN for {CustomVersionGuid}...", NeutralBrush);
                var details = await RobloxDeploymentClient.InspectAsync(CustomVersionGuid);

                if (details.NetworkError)
                {
                    SetStatus("Couldn't reach the Roblox CDN to verify. Check your connection and try again.", WarnBrush);
                    return;
                }

                if (!details.Exists)
                {
                    SetStatus("Not found on CDN. This build may have been purged by Roblox, or the hash is wrong.", ErrorBrush);
                    return;
                }

                string summary = $"Verified: {details.Hash} exists on CDN";
                if (details.PackageCount > 0)
                    summary += $" ({details.PackageCount} packages, {FormatBytes(details.TotalCompressedBytes)} to download)";
                if (details.LastModifiedUtc.HasValue)
                    summary += $". {FormatReleased(details.LastModifiedUtc)}";
                else
                    summary += ".";
                SetStatus(summary, OkBrush);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VersionViewModel::VerifyAsync", ex);
                SetStatus("Something went wrong verifying that hash. Check the log file for details.", ErrorBrush);
            }
            finally
            {
                IsVerifying = false;
            }
        }

        private void Reset()
        {
            SelectedExploit = null;
            UseCustomVersion = false;
            CustomVersionGuid = "";
        }

        private void RefreshStatusFromCurrentInput()
        {
            if (!UseCustomVersion)
            {
                if (HasLiveHash)
                    SetStatus($"Latest LIVE build ({LiveHash}) will be used on next launch.", NeutralBrush);
                else
                    SetStatus("Latest LIVE build will be used on next launch.", NeutralBrush);
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomVersionGuid))
            {
                SetStatus("Paste a version hash, pick an executor, or click \"Pin this version\".", NeutralBrush);
                return;
            }

            if (!VersionGuidValidator.IsWellFormed(CustomVersionGuid))
            {
                SetStatus("Hash format is invalid (expected version-xxxxxxxxxxxxxxxx).", ErrorBrush);
                return;
            }

            SetStatus($"Pinned to {CustomVersionGuid}. Click Verify to confirm it exists on CDN.", WarnBrush);
        }

        private void SetStatus(string text, Brush color)
        {
            StatusMessage = text;
            StatusColor = color;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "unknown size";
            string[] units = { "B", "KB", "MB", "GB" };
            double n = bytes;
            int u = 0;
            while (n >= 1024 && u < units.Length - 1) { n /= 1024; u++; }
            return $"{n:0.#} {units[u]}";
        }

        private static string FormatReleased(DateTime? lastModifiedUtc)
        {
            if (!lastModifiedUtc.HasValue) return "";

            DateTime utc = lastModifiedUtc.Value;
            DateTime local = utc.ToLocalTime();
            TimeSpan age = DateTime.UtcNow - utc;

            string relative;
            if (age.TotalSeconds < 0) relative = "in the future?";
            else if (age.TotalMinutes < 1) relative = "just now";
            else if (age.TotalMinutes < 60) relative = $"{(int)age.TotalMinutes} min ago";
            else if (age.TotalHours < 24) relative = $"{(int)age.TotalHours}h ago";
            else if (age.TotalDays < 30) relative = $"{(int)age.TotalDays}d ago";
            else if (age.TotalDays < 365) relative = $"~{(int)(age.TotalDays / 30)}mo ago";
            else relative = $"~{(int)(age.TotalDays / 365)}y ago";

            return $"Released {local:yyyy-MM-dd HH:mm} UTC{local:zzz} ({relative})";
        }
    }
}
