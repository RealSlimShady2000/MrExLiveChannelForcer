using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using MrExStrap.UI.Utility;
using MrExStrap.UI.ViewModels.Settings;

namespace MrExStrap.UI.Elements.Settings.Pages
{
    /// <summary>
    /// Interaction logic for BloxstrapPage.xaml
    /// </summary>
    public partial class BloxstrapPage
    {
        public BloxstrapPage()
        {
            DataContext = new BloxstrapViewModel();
            InitializeComponent();
        }

        private void ComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
            => ComboBoxScrollFix.HandleWheel(sender, e);
    }
}
