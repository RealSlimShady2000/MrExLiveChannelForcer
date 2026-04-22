using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
{
    public partial class VersionPage
    {
        public VersionPage()
        {
            DataContext = new VersionViewModel();
            InitializeComponent();
        }
    }
}
