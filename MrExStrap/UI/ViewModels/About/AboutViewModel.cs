using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using MrExStrap.UI;

namespace MrExStrap.UI.ViewModels.About
{
    public class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        public BuildMetadataAttribute BuildMetadata => App.BuildMetadata;

        public string BuildTimestamp => BuildMetadata.Timestamp.ToFriendlyString();
        public string BuildCommitHashUrl => $"https://github.com/{App.ProjectRepository}/commit/{BuildMetadata.CommitHash}";

        public Visibility BuildInformationVisibility => App.IsProductionBuild ? Visibility.Collapsed : Visibility.Visible;
        public Visibility BuildCommitVisibility => App.IsActionBuild ? Visibility.Visible : Visibility.Collapsed;

        public bool IsPortableMode => App.IsPortableMode;
        public Visibility PortableModeVisibility => App.IsPortableMode ? Visibility.Visible : Visibility.Collapsed;

        public ICommand CopyDiagnosticInfoCommand => new AsyncRelayCommand(CopyDiagnosticInfoAsync);

        private async Task CopyDiagnosticInfoAsync()
        {
            const string LOG_IDENT = "AboutViewModel::CopyDiagnosticInfo";
            try
            {
                string blob = await MrExStrap.Utility.Diagnostics.BuildAsync();
                Clipboard.SetDataObject(blob, true);
                Frontend.ShowMessageBox("Diagnostic info copied to clipboard. Paste it into your support message.",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOG_IDENT, ex);
                Frontend.ShowMessageBox("Couldn't build or copy diagnostic info. Check the log file.",
                    MessageBoxImage.Warning);
            }
        }
    }
}
