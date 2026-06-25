using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class VersionsManagerPage
    {
        public VersionsManagerPage()
        {
            DataContext = new VersionsManagerViewModel();
            InitializeComponent();
        }
    }
}
