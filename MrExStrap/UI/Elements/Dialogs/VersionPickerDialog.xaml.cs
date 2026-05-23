using MrExStrap.Models.Persistable;
using MrExStrap.UI.ViewModels.Dialogs;

namespace MrExStrap.UI.Elements.Dialogs
{
    public partial class VersionPickerDialog
    {
        public VersionProfile? PickedProfile { get; private set; }

        public VersionPickerDialog()
        {
            var vm = new VersionPickerViewModel();
            vm.CloseRequested += (_, profile) =>
            {
                PickedProfile = profile;
                Close();
            };
            DataContext = vm;
            InitializeComponent();
        }
    }
}
