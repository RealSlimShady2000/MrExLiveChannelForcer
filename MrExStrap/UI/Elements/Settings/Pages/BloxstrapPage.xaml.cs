using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using ExploitStrap.UI.Utility;
using ExploitStrap.UI.ViewModels.Settings;

namespace ExploitStrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for ExploitStrapPage.xaml
    /// </summary>
    public partial class ExploitStrapPage
    {
        public ExploitStrapPage()
        {
            DataContext = new ExploitStrapViewModel();
            InitializeComponent();
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => ComboBoxScrollFix.HandleWheel(sender, e);
    }
}
