using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for MultiInstancePage.xaml
    /// </summary>
    public partial class MultiInstancePage
    {
        public MultiInstancePage()
        {
            DataContext = new MultiInstanceViewModel();
            InitializeComponent();
        }
    }
}
