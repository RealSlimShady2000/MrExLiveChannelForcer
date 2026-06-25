using ExploitStrap.UI.ViewModels;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    public partial class HomePage
    {
        public HomePage()
        {
            DataContext = new HomeViewModel();
            InitializeComponent();
        }
    }
}
