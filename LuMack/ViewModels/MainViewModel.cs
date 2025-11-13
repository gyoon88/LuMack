using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using LuMack.Models;
using System.Linq;
using System.Windows;
using LuMack.Utils;
using LuMack.Services;

namespace LuMack.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ImageSource? mainImage;
        public ImageSource? MainImage
        {
            get => mainImage;
            set
            {
                SetProperty(ref mainImage, value);
            }
        }

        public event EventHandler? OnImageChanged;

        private FileService fileService;
        private MaskCreationService maskCreationService;

        private string mousePositionText = "X: ---, Y: ---";
        public string MousePositionText
        {
            get => mousePositionText;
            set
            {
                SetProperty(ref mousePositionText, value);
            }
        }

        private string pixelValueText = "R:--- G:--- B:--- (GV:---)";
        public string PixelValueText
        {
            get => pixelValueText;
            set
            {
                SetProperty(ref pixelValueText, value);
            }
        }

        private string zoomLevelText = "Zoom: 100%";
        public string ZoomLevelText
        {
            get => zoomLevelText;
            set
            {
                SetProperty(ref zoomLevelText, value);
            }
        }

        private string statusText = string.Empty;
        public string StatusText
        {
            get => statusText;
            set => SetProperty(ref statusText, value);
        }

        private Mask? selectedMask;
        public Mask? SelectedMask
        {
            get => selectedMask;
            set
            {
                SetProperty(ref selectedMask, value);
            }
        }

        private bool isEditMode;
        public bool IsEditMode
        {
            get => isEditMode;
            set
            {
                SetProperty(ref isEditMode, value);
            }
        }

        public ObservableCollection<Mask> Masks { get; } = new ObservableCollection<Mask>();
        public ObservableCollection<MaskClass> MaskClasses { get; } = new ObservableCollection<MaskClass>();

        private MaskClass? selectedMaskClass;
        public MaskClass? SelectedMaskClass
        {
            get => selectedMaskClass;
            set => SetProperty(ref selectedMaskClass, value);
        }

        public ICommand OpenImageCommand { get; }
        public ICommand LoadRecipeCommand { get; }
        public ICommand CreateMaskFromGVCommand { get; }
        public ICommand SaveMaskCommand { get; }
        public ICommand AddClassCommand { get; }
        public ICommand AssignClassCommand { get; }


        public MainViewModel()
        {
            fileService = new FileService(this);
            maskCreationService = new MaskCreationService();
            OpenImageCommand = new RelayCommand(fileService.OpenImage);
            LoadRecipeCommand = new RelayCommand(fileService.LoadRecipe);
            CreateMaskFromGVCommand = new RelayCommand(CreateMaskFromGV);
            SaveMaskCommand = new RelayCommand(SaveMask);
            AddClassCommand = new RelayCommand(AddClass);
            AssignClassCommand = new RelayCommand(AssignClass, CanAssignClass);

            // Initialize default class labels
            MaskClasses.Add(new MaskClass { Name = "Unclassified", DisplayColor = Colors.Gray });
            MaskClasses.Add(new MaskClass { Name = "Pad", DisplayColor = (Color)Application.Current.Resources["AccentBlueColor"] });
            MaskClasses.Add(new MaskClass { Name = "Line", DisplayColor = (Color)Application.Current.Resources["AccentPurpleColor"] });
            MaskClasses.Add(new MaskClass { Name = "Space", DisplayColor = Colors.LawnGreen });

            SelectedMaskClass = MaskClasses.FirstOrDefault();
        }


        private void AddClass(object? obj)
        {
            // For simplicity, we'll add a new class with a default name and a random color.
            // In a real app, you'd likely open a dialog to get the name and color from the user.
            var random = new Random();
            var color = Color.FromRgb((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
            var newClass = new MaskClass { Name = $"New Class {MaskClasses.Count}", DisplayColor = color };
            MaskClasses.Add(newClass);
            SelectedMaskClass = newClass;
        }

        private bool CanAssignClass(object? obj)
        {
            return SelectedMask != null && SelectedMaskClass != null;
        }

        private void AssignClass(object? obj)
        {
            if (SelectedMask != null && SelectedMaskClass != null)
            {
                SelectedMask.MaskClass = SelectedMaskClass;
            }
        }

        private void CreateMaskFromGV(object? parameter)
        {
            if (parameter is not GVCreationParameters gvParams || MainImage is not BitmapSource bitmap)
            {
                return;
            }

            var defaultClass = MaskClasses.FirstOrDefault(mc => mc.Name == "Unclassified") ?? MaskClasses.First();
            var newMask = maskCreationService.CreateMaskFromGV(bitmap, gvParams, defaultClass);

            if (newMask != null)
            {
                newMask.Name = $"GV Mask {Masks.Count + 1}";
                Masks.Add(newMask);
            }
        }


        private void SaveMask(object? parameter)
        {
            if (MainImage is not BitmapSource sourceBitmap)
            {
                MessageBox.Show("Please load an image first.", "No Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PNG Image (*.png)|*.png",
                Title = "Save Color Mask As"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // 1. Create the final Pbgra32 bitmap, initialized to transparent.
                    var savedBitmap = new WriteableBitmap(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);

                    savedBitmap.Lock();
                    try
                    {
                        // 2. Loop through masks.
                        foreach (var mask in Masks.Where(m => m.IsVisible && m.MaskImage != null && m.MaskClass != null))
                        {
                            // 3. Filter out "Unclassified".
                            if (mask.MaskClass.Name == "Unclassified") continue;

                            var sourceMask = mask.MaskImage;
                            if (sourceMask == null) continue;

                            var displayColor = mask.MaskClass.DisplayColor;
                            int colorInt = (displayColor.A << 24) | (displayColor.R << 16) | (displayColor.G << 8) | displayColor.B;

                            int sourceStride = (sourceMask.PixelWidth * sourceMask.Format.BitsPerPixel + 7) / 8;
                            byte[] sourcePixels = new byte[sourceMask.PixelHeight * sourceStride];
                            sourceMask.CopyPixels(sourcePixels, sourceStride, 0);

                            int finalStride = savedBitmap.BackBufferStride;

                            unsafe
                            {
                                int* pFinalMap = (int*)savedBitmap.BackBuffer;

                                for (int y = 0; y < sourceMask.PixelHeight; y++)
                                {
                                    for (int x = 0; x < sourceMask.PixelWidth; x++)
                                    {
                                        byte alpha = sourcePixels[y * sourceStride + x * 4 + 3];

                                        if (alpha > 0) // If the mask pixel is visible
                                        {
                                            // Write the display color to the final map
                                            *(pFinalMap + y * (finalStride / 4) + x) = colorInt;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        savedBitmap.AddDirtyRect(new Int32Rect(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));
                        savedBitmap.Unlock();
                    }

                    // 4. Save the completed color mask map.
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(savedBitmap));

                    using (var fs = System.IO.File.OpenWrite(saveFileDialog.FileName))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show($"Color mask saved successfully to:\n{saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving mask: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
                  
    }
}


