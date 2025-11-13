using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using LuMack.Common;
using LuMack.Models;
using LuMack.Services;

namespace LuMack.ViewModels
{
    public class PatchViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainViewModel;
        private readonly PatchGenerationService _patchGenerationService;
        private readonly PatchSavingService _patchSavingService;

        public ObservableCollection<PatchPoint> PatchPoints { get; } = new ObservableCollection<PatchPoint>();

        private PatchPoint? _selectedPatchPoint;
        public PatchPoint? SelectedPatchPoint
        {
            get => _selectedPatchPoint;
            set => SetProperty(ref _selectedPatchPoint, value);
        }

        private int _selectedPatchCount;
        public int SelectedPatchCount
        {
            get => _selectedPatchCount;
            set => SetProperty(ref _selectedPatchCount, value);
        }

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public ICommand GeneratePatchesCommand { get; }
        public ICommand SaveSelectedPatchesCommand { get; }

        public PatchViewModel(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _patchGenerationService = new PatchGenerationService();
            _patchSavingService = new PatchSavingService();

            GeneratePatchesCommand = new RelayCommand(GeneratePatches);
            SaveSelectedPatchesCommand = new RelayCommand(SaveSelectedPatches, CanSaveSelectedPatches);

            _mainViewModel.Masks.CollectionChanged += OnMasksCollectionChanged;
            foreach (var mask in _mainViewModel.Masks)
            {
                mask.PropertyChanged += OnMaskPropertyChanged;
            }
        }

        private void OnMasksCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Mask item in e.NewItems.OfType<Mask>())
                {
                    item.PropertyChanged += OnMaskPropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (Mask item in e.OldItems.OfType<Mask>())
                {
                    item.PropertyChanged -= OnMaskPropertyChanged;
                }
            }
    
            GeneratePatches(null);
        }

        private void OnMaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Mask.IsVisible))
            {
                UpdateMaskPatches();
            }
        }

        private async void UpdateMaskPatches()
        {
            if (_mainViewModel.MainImage == null || !PatchPoints.Any())
            {
                return;
            }

            StatusText = "Updating mask patches...";
            await _patchGenerationService.UpdateMaskPatchesAsync(_mainViewModel.MainImage, _mainViewModel.Masks, PatchPoints, 256);
            StatusText = "Mask patches updated.";
        }

        private async void GeneratePatches(object? parameter)
        {
            if (_mainViewModel.MainImage == null)
            {
                MessageBox.Show("Please load an image in the main window first.", "No Image");
                return;
            }

            StatusText = "Generating patches...";
            // Unsubscribe from old items
            foreach (var point in PatchPoints)
            {
                point.PropertyChanged -= OnPatchPointPropertyChanged;
            }
            PatchPoints.Clear();

            var generatedPoints = await _patchGenerationService.GenerateAsync(_mainViewModel.MainImage, _mainViewModel.Masks, 1000, 256);

            foreach (var point in generatedPoints)
            {
                point.PropertyChanged += OnPatchPointPropertyChanged;
                PatchPoints.Add(point);
            }

            UpdateSelectedCount();
            StatusText = $"Generated {PatchPoints.Count} patches.";
        }

        private async void SaveSelectedPatches(object? parameter)
        {
            var selectedPatches = PatchPoints.Where(p => p.IsSelectedToSave).ToList();
            if (selectedPatches.Count == 0)
            {
                MessageBox.Show("No patches selected to save.", "Nothing to Save");
                return;
            }

            // Use SaveFileDialog as a workaround to select a folder
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select a Folder for Saving Patches",
                Filter = "Folder|*.this.folder.does.not.exist",
                FileName = "select_folder"
            };

            if (dialog.ShowDialog() == true)
            {
                string? folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (string.IsNullOrEmpty(folderPath))
                {
                    MessageBox.Show("Could not determine the folder path.", "Invalid Path");
                    return;
                }

                StatusText = $"Saving {selectedPatches.Count} patches...";
                await _patchSavingService.SaveAsync(selectedPatches, folderPath);
                StatusText = $"Successfully saved {selectedPatches.Count} patches to {folderPath}";
            }
        }

        private bool CanSaveSelectedPatches(object? parameter)
        {
            return SelectedPatchCount > 0;
        }

        private void OnPatchPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PatchPoint.IsSelectedToSave))
            {
                UpdateSelectedCount();
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedPatchCount = PatchPoints.Count(p => p.IsSelectedToSave);
        }

        public void Cleanup()
        {
            _mainViewModel.Masks.CollectionChanged -= OnMasksCollectionChanged;
            foreach (var mask in _mainViewModel.Masks)
            {
                mask.PropertyChanged -= OnMaskPropertyChanged;
            }
            foreach (var point in PatchPoints)
            {
                point.PropertyChanged -= OnPatchPointPropertyChanged;
            }
        }
    }
}
