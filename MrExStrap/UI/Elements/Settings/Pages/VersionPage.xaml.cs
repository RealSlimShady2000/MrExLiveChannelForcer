using System.Windows.Input;

using ExploitStrap.UI.Utility;
using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
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
