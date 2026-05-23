using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;

using MrExStrap.Models.Persistable;
using MrExStrap.Utility;

namespace MrExStrap.UI.ViewModels.Settings
{
    // ViewModel for the Versions Manager tab. Holds the tile list (one item per
    // VersionProfile), exposes commands for activating / editing / deleting /
    // adding profiles, and renders the executor logo for each tile via the
    // ExecutorLogoCache.
    public class VersionsManagerViewModel : NotifyPropertyChangedViewModel
    {
        private const string LOG_IDENT = "VersionsManagerViewModel";

        public ObservableCollection<VersionProfileTile> Tiles { get; } = new();

        public ICommand ActivateCommand => new RelayCommand<string>(Activate);
        public ICommand DeleteCommand => new RelayCommand<string>(DeleteProfile);
        public ICommand AddProfileCommand => new RelayCommand(AddProfile);
        public ICommand OpenVersionsFolderCommand => new RelayCommand(OpenVersionsFolder);
        public ICommand RefreshCommand => new RelayCommand(RebuildTiles);

        private string _activeName = "";
        public string ActiveName
        {
            get => _activeName;
            private set { _activeName = value; OnPropertyChanged(nameof(ActiveName)); }
        }

        private string _activeHash = "";
        public string ActiveHash
        {
            get => _activeHash;
            private set { _activeHash = value; OnPropertyChanged(nameof(ActiveHash)); }
        }

        private string _diskUsageText = "";
        public string DiskUsageText
        {
            get => _diskUsageText;
            private set { _diskUsageText = value; OnPropertyChanged(nameof(DiskUsageText)); }
        }

        // Banner: visible when the legacy single-pin is on AND a non-built-in Versions
        // Manager profile is also active. Tells the user the new tab wins.
        public Visibility SinglePinConflictVisibility =>
            App.Settings.Prop.UseCustomVersion
            && App.Settings.Prop.VersionProfiles
                .Any(p => p.Id == App.Settings.Prop.ActiveVersionProfileId && !p.IsBuiltIn && !string.IsNullOrEmpty(p.VersionGuid))
                ? Visibility.Visible : Visibility.Collapsed;

        public VersionsManagerViewModel()
        {
            RebuildTiles();
        }

        private void RebuildTiles()
        {
            Tiles.Clear();
            string activeId = App.Settings.Prop.ActiveVersionProfileId;

            foreach (var profile in App.Settings.Prop.VersionProfiles)
            {
                var tile = new VersionProfileTile(profile, profile.Id == activeId);
                Tiles.Add(tile);
                // Fire-and-forget the logo fetch.
                _ = tile.LoadLogoAsync();
            }

            RefreshActiveSummary();
            RefreshDiskUsage();
            OnPropertyChanged(nameof(SinglePinConflictVisibility));
        }

        private void RefreshActiveSummary()
        {
            var active = App.Settings.Prop.VersionProfiles
                .FirstOrDefault(p => p.Id == App.Settings.Prop.ActiveVersionProfileId);
            if (active == null)
            {
                ActiveName = "(none)";
                ActiveHash = "";
                return;
            }
            ActiveName = active.Name;
            ActiveHash = string.IsNullOrEmpty(active.VersionGuid) ? "(current LIVE)" : active.VersionGuid;
        }

        private void RefreshDiskUsage()
        {
            try
            {
                var guids = App.Settings.Prop.VersionProfiles
                    .Where(p => !string.IsNullOrEmpty(p.VersionGuid))
                    .Select(p => p.VersionGuid);
                long bytes = VersionsDiskUsage.GetTotalUsageBytes(guids);
                int profileCount = App.Settings.Prop.VersionProfiles.Count;
                DiskUsageText = $"Disk usage: {VersionsDiskUsage.FormatBytes(bytes)} across {profileCount} profile{(profileCount == 1 ? "" : "s")}";
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::RefreshDiskUsage", ex);
                DiskUsageText = "Disk usage: (unavailable)";
            }
        }

        private void Activate(string? id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var profile = App.Settings.Prop.VersionProfiles.FirstOrDefault(p => p.Id == id);
            if (profile == null) return;

            App.Settings.Prop.ActiveVersionProfileId = profile.Id;

            // Mirror into the legacy single-pin so any code path that still reads
            // CustomVersionGuid (e.g. log statements, third-party hooks) sees a
            // consistent value. For the built-in LIVE profile we clear the pin.
            if (profile.IsBuiltIn || string.IsNullOrEmpty(profile.VersionGuid))
            {
                App.Settings.Prop.UseCustomVersion = false;
                App.Settings.Prop.CustomVersionGuid = "";
            }
            else
            {
                App.Settings.Prop.UseCustomVersion = true;
                App.Settings.Prop.CustomVersionGuid = profile.VersionGuid;
            }

            App.Settings.Save();
            App.Logger.WriteLine(LOG_IDENT, $"Activated profile '{profile.Name}' ({profile.Id})");

            foreach (var tile in Tiles)
                tile.IsActive = tile.Id == id;

            RefreshActiveSummary();
            OnPropertyChanged(nameof(SinglePinConflictVisibility));
        }

        private void DeleteProfile(string? id)
        {
            if (string.IsNullOrEmpty(id)) return;
            var profile = App.Settings.Prop.VersionProfiles.FirstOrDefault(p => p.Id == id);
            if (profile == null || profile.IsBuiltIn) return;

            var confirm = Frontend.ShowMessageBox(
                $"Delete profile '{profile.Name}'?\n\nThe pinned Roblox install for this profile will be removed on next launch cleanup unless another profile references the same hash.",
                MessageBoxImage.Warning,
                MessageBoxButton.YesNo,
                MessageBoxResult.No);
            if (confirm != MessageBoxResult.Yes) return;

            App.Settings.Prop.VersionProfiles.Remove(profile);

            // If we just deleted the active profile, fall back to the built-in LIVE one.
            if (App.Settings.Prop.ActiveVersionProfileId == id)
            {
                App.Settings.Prop.ActiveVersionProfileId = App.LiveBuiltInProfileId;
                App.Settings.Prop.UseCustomVersion = false;
                App.Settings.Prop.CustomVersionGuid = "";
            }

            App.Settings.Save();
            App.Logger.WriteLine(LOG_IDENT, $"Deleted profile '{profile.Name}' ({id})");
            RebuildTiles();
        }

        private void AddProfile()
        {
            var dialog = new UI.Elements.Dialogs.AddVersionProfileDialog();
            dialog.ShowDialog();
            if (dialog.CreatedProfile == null) return;

            App.Settings.Prop.VersionProfiles.Add(dialog.CreatedProfile);
            App.Settings.Save();
            App.Logger.WriteLine(LOG_IDENT, $"Added profile '{dialog.CreatedProfile.Name}' ({dialog.CreatedProfile.Id})");
            RebuildTiles();
        }

        private void OpenVersionsFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(Paths.Versions))
                {
                    Directory.CreateDirectory(Paths.Versions);
                    Process.Start(new ProcessStartInfo { FileName = Paths.Versions, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT + "::OpenVersionsFolder", ex);
            }
        }
    }

    // One row per profile. Wraps the VersionProfile for binding plus the loaded
    // logo image / placeholder letter.
    public class VersionProfileTile : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Name { get; }
        public string VersionGuid { get; }
        public string DisplayHash { get; }
        public string LetterPlaceholder { get; }
        public bool IsBuiltIn { get; }
        public bool CanDelete => !IsBuiltIn;
        public string? LogoUrl { get; }
        public string DiskUsageText { get; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(nameof(IsActive)); }
        }

        private ImageSource? _logo;
        public ImageSource? Logo
        {
            get => _logo;
            private set { _logo = value; OnPropertyChanged(nameof(Logo)); OnPropertyChanged(nameof(HasLogo)); OnPropertyChanged(nameof(NoLogo)); }
        }
        public bool HasLogo => _logo != null;
        public bool NoLogo => _logo == null;

        public VersionProfileTile(VersionProfile profile, bool isActive)
        {
            Id = profile.Id;
            Name = profile.Name;
            VersionGuid = profile.VersionGuid;
            DisplayHash = string.IsNullOrEmpty(profile.VersionGuid) ? "(current LIVE)" : profile.VersionGuid;
            LetterPlaceholder = string.IsNullOrEmpty(profile.Name) ? "?" : profile.Name.Substring(0, 1).ToUpperInvariant();
            IsBuiltIn = profile.IsBuiltIn;
            LogoUrl = profile.ExecutorLogoUrl;
            _isActive = isActive;

            long bytes = string.IsNullOrEmpty(profile.VersionGuid) ? 0 : VersionsDiskUsage.GetUsageBytes(profile.VersionGuid);
            DiskUsageText = bytes > 0 ? VersionsDiskUsage.FormatBytes(bytes) : "";
        }

        public async Task LoadLogoAsync()
        {
            if (string.IsNullOrWhiteSpace(LogoUrl)) return;
            try
            {
                string? path = await ExecutorLogoCache.GetLogoAsync(LogoUrl);
                if (string.IsNullOrEmpty(path)) return;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher == null) return;

                dispatcher.Invoke(() =>
                {
                    try
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.UriSource = new Uri(path);
                        img.EndInit();
                        img.Freeze();
                        Logo = img;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteException("VersionProfileTile::LoadLogoAsync::Bitmap", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                App.Logger.WriteException("VersionProfileTile::LoadLogoAsync", ex);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
