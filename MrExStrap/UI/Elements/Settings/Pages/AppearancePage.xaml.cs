using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
{
    public partial class AppearancePage
    {
        public AppearancePage()
        {
            DataContext = new AppearanceViewModel(this);
            InitializeComponent();
        }
    }
}
