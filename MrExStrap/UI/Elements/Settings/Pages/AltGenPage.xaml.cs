using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for AltGenPage.xaml
    /// </summary>
    public partial class AltGenPage
    {
        public AltGenPage()
        {
            DataContext = new AltGenViewModel();
            InitializeComponent();
        }
    }
}
