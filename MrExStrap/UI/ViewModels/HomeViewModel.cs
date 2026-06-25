namespace ExploitStrap.UI.ViewModels
{
    // Backs the Home dashboard (UI\Elements\Settings\Pages\HomePage). Read-only snapshot taken
    // when the settings window opens — the launch action itself reuses MainWindowViewModel's
    // SaveAndLaunchCommand via a RelativeSource binding in the page XAML.
    public class HomeViewModel : NotifyPropertyChangedViewModel
    {
        public string Version => $"v{App.Version}";

        public string ChannelStatus => "LIVE · locked";

        public string ActiveProfileName
        {
            get
            {
                string id = App.Settings.Prop.ActiveVersionProfileId ?? "";
                var profile = App.Settings.Prop.VersionProfiles.FirstOrDefault(x => x.Id == id);
                return string.IsNullOrWhiteSpace(profile?.Name) ? "Latest LIVE" : profile!.Name;
            }
        }

        public string? ExecutorTitle => App.GetActiveExecutorTitle();

        public bool HasExecutor => !string.IsNullOrEmpty(ExecutorTitle);
    }
}
