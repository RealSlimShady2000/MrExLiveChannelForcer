using System.Windows.Input;

using MrExStrap.UI.Utility;
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

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => ComboBoxScrollFix.HandleWheel(sender, e);
    }
}
