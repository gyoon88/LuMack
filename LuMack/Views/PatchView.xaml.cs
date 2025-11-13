using System.Windows;
using LuMack.ViewModels;

namespace LuMack.Views
{
    public partial class PatchView : Window
    {
        public PatchView()
        {
            InitializeComponent();
            Closed += PatchView_Closed;
        }

        private void PatchView_Closed(object? sender, System.EventArgs e)
        {
            if (DataContext is PatchViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}
