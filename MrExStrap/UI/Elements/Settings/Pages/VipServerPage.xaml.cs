using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
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
