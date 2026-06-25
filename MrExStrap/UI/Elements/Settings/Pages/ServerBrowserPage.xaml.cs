using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class ServerBrowserPage
    {
        public ServerBrowserPage()
        {
            DataContext = new ServerBrowserViewModel();
            InitializeComponent();
        }
    }
}
