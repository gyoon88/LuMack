using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using LuMack.Models;
using System.Linq;
using System.Windows;

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
        public ObservableCollection<string> ClassLabels { get; } = new ObservableCollection<string>();

        public ICommand OpenImageCommand { get; }
        public ICommand LoadRecipeCommand { get; }
        public ICommand CreateMaskFromGVCommand { get; }
        public ICommand SaveMaskCommand { get; }

        private readonly List<Color> _maskColors = new List<Color>();
        private int _colorIndex = 0;

        public MainViewModel()
        {
            OpenImageCommand = new RelayCommand(OpenImage);
            LoadRecipeCommand = new RelayCommand(LoadRecipe);
            CreateMaskFromGVCommand = new RelayCommand(CreateMaskFromGV);
            SaveMaskCommand = new RelayCommand(SaveMask);

            // Initialize default class labels
            ClassLabels.Add("Unclassified");
            ClassLabels.Add("Pad");
            ClassLabels.Add("Line");
            ClassLabels.Add("Space");

            // Load theme colors for masks
            _maskColors.Add((Color)Application.Current.Resources["AccentBlueColor"]);
            _maskColors.Add((Color)Application.Current.Resources["AccentPurpleColor"]);
            _maskColors.Add(Colors.LawnGreen);
            _maskColors.Add(Colors.OrangeRed);
            _maskColors.Add(Colors.Gold);
        }

        private void OpenImage(object? parameter)
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
                    MainImage = bitmap;
                    OnImageChanged?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error loading image: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void LoadRecipe(object? parameter)
        {
            // This is a placeholder for loading a real recipe file.
            // For now, we'll use a hardcoded RLE string to create a test mask.

            // This dummy RLE represents a 10x10 square at position (100, 100)
            // on an image with a width of 300.
            // RLE format: run1,value1,run2,value2,...
            // Value 0 = transparent, Value 1 = opaque.
            // Each line in the mask is 10 pixels wide.
            // (100 pixels transparent space) + (10 pixels opaque) + (190 pixels transparent space) = 300 total width
            string dummyRle = string.Join(",", Enumerable.Repeat("100,0,10,1,190,0", 10));
            int imageWidth = 300; // Assume image width is 300 for this example
            int startX = 0;
            int startY = 100;

            var newMask = new Mask
            {
                Name = $"Test Mask {Masks.Count + 1}",
                RleData = dummyRle,
                DisplayColor = _maskColors[_colorIndex++ % _maskColors.Count],
                MaskGeometry = ParseRleToGeometry(dummyRle, imageWidth, startX, startY)
            };

            Masks.Add(newMask);
        }

        private void CreateMaskFromGV(object? parameter)
        {
            if (parameter is not Models.GVCreationParameters gvParams || MainImage is not BitmapSource bitmap)
            {
                return;
            }

            try
            {
                int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
                byte[] pixels = new byte[bitmap.PixelHeight * stride];
                bitmap.CopyPixels(pixels, stride, 0);

                int pointX = (int)(gvParams.ClickPoint.X * (bitmap.PixelWidth / gvParams.ImageActualWidth));
                int pointY = (int)(gvParams.ClickPoint.Y * (bitmap.PixelHeight / gvParams.ImageActualHeight));

                byte startGV = GetPixelGrayValue(pixels, pointX, pointY, bitmap.PixelWidth, bitmap.Format);
                
                int tolerance = 10; 

                var maskPixels = FloodFill(pixels, pointX, pointY, bitmap.PixelWidth, bitmap.PixelHeight, startGV, tolerance, bitmap.Format);

                if (maskPixels.Count == 0) return;

                var newMask = new Mask
                {
                    Name = $"GV Mask {Masks.Count + 1}",
                    DisplayColor = _maskColors[_colorIndex++ % _maskColors.Count],
                    MaskGeometry = PointsToGeometry(maskPixels)
                };

                Masks.Add(newMask);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during GV mask creation: {ex.Message}", "Error");
            }
        }

        private Geometry PointsToGeometry(List<Point> points)
        {
            var geometryGroup = new GeometryGroup();
            foreach (var p in points)
            {
                geometryGroup.Children.Add(new RectangleGeometry(new Rect(p.X, p.Y, 1, 1)));
            }
            return geometryGroup;
        }

        private List<Point> FloodFill(byte[] pixels, int startX, int startY, int width, int height, byte startGV, int tolerance, PixelFormat format)
        {
            var points = new List<Point>();
            var q = new Queue<Point>();
            var visited = new bool[width * height];
            
            q.Enqueue(new Point(startX, startY));
            visited[startY * width + startX] = true;

            int minGV = startGV - tolerance;
            int maxGV = startGV + tolerance;

            while (q.Count > 0)
            {
                var p = q.Dequeue();
                points.Add(p);

                int x = (int)p.X;
                int y = (int)p.Y;

                // Check neighbors
                int[] dx = { 0, 0, 1, -1 };
                int[] dy = { 1, -1, 0, 0 };

                for (int i = 0; i < 4; i++)
                {
                    int nextX = x + dx[i];
                    int nextY = y + dy[i];

                    if (nextX >= 0 && nextX < width && nextY >= 0 && nextY < height)
                    {
                        int index = nextY * width + nextX;
                        if (!visited[index])
                        {
                            visited[index] = true;
                            byte neighborGV = GetPixelGrayValue(pixels, nextX, nextY, width, format);
                            if (neighborGV >= minGV && neighborGV <= maxGV)
                            {
                                q.Enqueue(new Point(nextX, nextY));
                            }
                        }
                    }
                }
            }
            
            return points;
        }

        private byte GetPixelGrayValue(byte[] pixels, int x, int y, int width, PixelFormat format)
        {
            int bytesPerPixel = (format.BitsPerPixel + 7) / 8;
            int stride = width * bytesPerPixel;
            int index = y * stride + x * bytesPerPixel;

            if (index + bytesPerPixel > pixels.Length) return 0;

            byte r, g, b;
            if (bytesPerPixel == 4) // BGRA32
            {
                b = pixels[index];
                g = pixels[index + 1];
                r = pixels[index + 2];
            }
            else if (bytesPerPixel == 3) // BGR24
            {
                b = pixels[index];
                g = pixels[index + 1];
                r = pixels[index + 2];
            }
            else if (bytesPerPixel == 1) // Gray8
            {
                return pixels[index];
            }
            else // Other formats not handled, return 0
            {
                return 0;
            }

            // Luminance formula
            return (byte)(0.299 * r + 0.587 * g + 0.114 * b);
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
                Title = "Save Mask As"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Create a bitmap to draw the mask labels onto.
                    // The format is 8-bit grayscale, where each pixel value is the class index.
                    var renderTarget = new RenderTargetBitmap(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight, sourceBitmap.DpiX, sourceBitmap.DpiY, PixelFormats.Gray8);

                    var drawingVisual = new DrawingVisual();
                    using (var drawingContext = drawingVisual.RenderOpen())
                    {
                        // Draw a black background (class index 0)
                        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));

                        // Draw each visible mask with its class index as the color
                        foreach (var mask in Masks.Where(m => m.IsVisible && m.MaskGeometry != null))
                        {
                            int classIndex = ClassLabels.IndexOf(mask.ClassLabel);
                            if (classIndex < 0) classIndex = 0; // Default to 0 if not found

                            // We use a grayscale color where the R, G, and B values are the class index.
                            var color = (byte)classIndex;
                            var brush = new SolidColorBrush(Color.FromRgb(color, color, color));
                            
                            drawingContext.DrawGeometry(brush, null, mask.MaskGeometry);
                        }
                    }

                    renderTarget.Render(drawingVisual);

                    // Encode and save the bitmap
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(renderTarget));

                    using (var fs = System.IO.File.OpenWrite(saveFileDialog.FileName))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show($"Mask saved successfully to:\n{saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving mask: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private Geometry ParseRleToGeometry(string rle, int imageWidth, int startX, int startY)
        {
            var geometryGroup = new GeometryGroup();
            var runs = rle.Split(',').Select(int.Parse).ToList();

            int currentX = startX;
            int currentY = startY;

            for (int i = 0; i < runs.Count; i += 2)
            {
                int runLength = runs[i];
                int value = runs[i + 1];

                if (value == 1) // Opaque run
                {
                    int x = currentX;
                    int y = currentY;
                    int length = runLength;

                    while (length > 0)
                    {
                        int remainingInLine = imageWidth - x;
                        int rectWidth = Math.Min(length, remainingInLine);
                        
                        geometryGroup.Children.Add(new RectangleGeometry(new Rect(x, y, rectWidth, 1)));
                        
                        length -= rectWidth;
                        x += rectWidth;
                        if (x >= imageWidth)
                        {
                            x = 0;
                            y++;
                        }
                    }
                }

                currentX += runLength;
                while (currentX >= imageWidth)
                {
                    currentX -= imageWidth;
                    currentY++;
                }
            }

            return geometryGroup;
        }
    }
}

