using LuMack.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace LuMack.Utils
{

    public class FileService
    {
        MainViewModel mainVM;
        public FileService(MainViewModel mainViewModel)
        {
            mainVM = mainViewModel;

        }
        public void OpenImage(object sender)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp)|*.png;*.jpeg;*.jpg;*.bmp|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    mainVM.MainImage = bitmap;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error loading image: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
        public async void LoadRecipe(object sender)
        {
            if (mainVM.MainImage is not BitmapSource bitmap)
            {
                System.Windows.MessageBox.Show("Please open an image before loading a recipe.", "Image Not Found", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "XML Recipe Files (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Open Mask Recipe File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                
                // --- Get data from UI objects BEFORE the background task ---
                int imageWidth = bitmap.PixelWidth;
                int imageHeight = bitmap.PixelHeight;
                var maskClasses = mainVM.MaskClasses.ToList(); // Create a thread-safe copy

                mainVM.StatusText = "Loading recipe...";
                try
                {
                    var loadedMasks = await Task.Run(() =>
                    {
                        XDocument loadedDoc = XDocument.Load(filePath);
                        var maskService = new MaskLoaderService();
                        // --- Use the local copies in the background task ---
                        return maskService.LoadMasksFromXml(loadedDoc, maskClasses, imageWidth, imageHeight);
                    });

                    // Update the UI on the UI thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainVM.Masks.Clear();
                        foreach (var mask in loadedMasks)
                        {
                            mask.IsVisible = false;
                            mainVM.Masks.Add(mask);
                        }

                        string sizeInfo = "N/A";
                        var firstMask = loadedMasks.FirstOrDefault();
                        if (firstMask?.MaskImage != null)
                        {
                            sizeInfo = $"{firstMask.MaskImage.PixelWidth}x{firstMask.MaskImage.PixelHeight}";
                        }

                        System.Windows.MessageBox.Show($"{loadedMasks.Count} masks loaded successfully.\nFirst mask size: {sizeInfo}", "Recipe Loaded", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"An error occurred while reading the file:\n{ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    mainVM.StatusText = "Ready";
                }
            }
        }

    }
}
