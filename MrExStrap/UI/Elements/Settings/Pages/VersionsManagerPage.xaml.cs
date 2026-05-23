using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
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
