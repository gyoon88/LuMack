using System.Collections.ObjectModel;
using LuMack.Common;
using LuMack.Models;

namespace LuMack.ViewModels
{
    public class PatchViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;

        public ObservableCollection<PatchPoint> PatchPoints { get; } = new ObservableCollection<PatchPoint>();

        private PatchPoint? _selectedPatchPoint;
        public PatchPoint? SelectedPatchPoint
        {
            get => _selectedPatchPoint;
            set => SetProperty(ref _selectedPatchPoint, value);
        }

        public PatchViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
        }
    }
}
