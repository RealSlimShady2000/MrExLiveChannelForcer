using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
{
    public partial class BanAsyncPage
    {
        public BanAsyncPage()
        {
            DataContext = new BanAsyncViewModel();
            InitializeComponent();
        }
    }
}
