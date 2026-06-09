using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
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
