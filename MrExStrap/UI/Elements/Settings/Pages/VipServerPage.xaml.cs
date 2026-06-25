using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class VipServerPage
    {
        public VipServerPage()
        {
            DataContext = new VipServerViewModel();
            InitializeComponent();
        }
    }
}
