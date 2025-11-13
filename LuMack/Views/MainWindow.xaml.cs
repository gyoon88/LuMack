using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuMack.ViewModels;

namespace LuMack.Views
{
    public partial class MainWindow : Window
    {
        private Point _panStartPoint;
        private Point _startOffset;
        private bool _isPanning;
        private bool _isInitialised = false;
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.OnImageChanged -= OnImageChanged;
            }
            if (e.NewValue is MainViewModel newVm)
            {
                newVm.OnImageChanged += OnImageChanged;
                UpdateZoomText(); // Initial update
            }
        }

        private void OnImageChanged(object? sender, EventArgs e)
        {
            // When a new image is loaded, we need to re-initialise its position.
            _isInitialised = false;
        }

        private void MainImage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // This event fires when the image is first rendered or its size changes.
            // We use this to perform the initial centering of the image.
            if (!_isInitialised && e.NewSize.Width > 0 && e.NewSize.Height > 0)
            {
                ResetTransform();
                _isInitialised = true;
            }
        }

        private void ResetTransform()
        {
            ImageScaleTransform.ScaleX = 1;
            ImageScaleTransform.ScaleY = 1;
            ImageTranslateTransform.X = 0;
            ImageTranslateTransform.Y = 0;
            
            ApplyConstraints();
            UpdateZoomText();
        }

        private void MainImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ViewModel?.MainImage == null || !_isInitialised) return;

            var scaleFactor = e.Delta > 0 ? 1.1 : 1 / 1.1;
            var mousePostion = e.GetPosition(ImageBorder);

            var currentScale = ImageScaleTransform.ScaleX;
            var newScale = currentScale * scaleFactor;

            // Limit zoom
            if (newScale < 0.1) newScale = 0.1;
            if (newScale > 100) newScale = 100;

            ImageScaleTransform.ScaleX = newScale;
            ImageScaleTransform.ScaleY = newScale;

            var newX = mousePostion.X - (mousePostion.X - ImageTranslateTransform.X) * scaleFactor;
            var newY = mousePostion.Y - (mousePostion.Y - ImageTranslateTransform.Y) * scaleFactor;

            ImageTranslateTransform.X = newX;
            ImageTranslateTransform.Y = newY;

            ApplyConstraints();
            UpdateZoomText();
        }

        private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.MainImage == null || !_isInitialised) return;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var transformedPoint = e.GetPosition(MainImage);
                var parameters = new Models.GVCreationParameters
                {
                    ClickPoint = transformedPoint,
                    ImageActualWidth = MainImage.ActualWidth,
                    ImageActualHeight = MainImage.ActualHeight
                };
                ViewModel.CreateMaskFromGVCommand.Execute(parameters);
            }
            else
            {
                if (ViewModel.IsEditMode)
                {
                    DrawOnMask(e.GetPosition(MainImage));
                }
                else
                {
                    _panStartPoint = e.GetPosition(ImageBorder);
                    _startOffset = new Point(ImageTranslateTransform.X, ImageTranslateTransform.Y);
                    _isPanning = true;
                    Mouse.Capture(ImageBorder);
                }
            }
        }

        private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            Mouse.Capture(null);
        }

        private void MainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPos = e.GetPosition(ImageBorder);
                Vector delta = currentPos - _panStartPoint;
                ImageTranslateTransform.X = _startOffset.X + delta.X;
                ImageTranslateTransform.Y = _startOffset.Y + delta.Y;
                ApplyConstraints();
            }
            
            if (_isInitialised)
            {
                UpdateStatusBar(e.GetPosition(MainImage), MainImage.ActualWidth, MainImage.ActualHeight);
            }
        }
        
        private void MainImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel?.MainImage == null || !_isInitialised) return;
            ResetTransform();
        }

        private void ImageBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyConstraints();
        }

        private void ApplyConstraints()
        {
            if (ViewModel?.MainImage is not BitmapSource source || !_isInitialised) return;

            var scale = ImageScaleTransform.ScaleX;
            var viewportWidth = ImageBorder.ActualWidth;
            var viewportHeight = ImageBorder.ActualHeight;

            // Determine the size of the image at scale=1 (when it's fitting the viewport)
            var sourceAspect = source.PixelWidth / (double)source.PixelHeight;
            var viewportAspect = viewportWidth / viewportHeight;

            double widthAtScale1, heightAtScale1;
            if (sourceAspect > viewportAspect)
            {
                widthAtScale1 = viewportWidth;
                heightAtScale1 = viewportWidth / sourceAspect;
            }
            else
            {
                heightAtScale1 = viewportHeight;
                widthAtScale1 = viewportHeight * sourceAspect;
            }

            var contentWidth = widthAtScale1 * scale;
            var contentHeight = heightAtScale1 * scale;

            var minX = viewportWidth - contentWidth;
            var minY = viewportHeight - contentHeight;

            var currentX = ImageTranslateTransform.X;
            var currentY = ImageTranslateTransform.Y;

            // Clamp the current translation
            var newX = Math.Max(minX, Math.Min(0, currentX));
            var newY = Math.Max(minY, Math.Min(0, currentY));

            // If content is smaller than viewport, override clamp and center it
            if (contentWidth < viewportWidth)
            {
                newX = (viewportWidth - contentWidth) / 2;
            }
            if (contentHeight < viewportHeight)
            {
                newY = (viewportHeight - contentHeight) / 2;
            }

            ImageTranslateTransform.X = newX;
            ImageTranslateTransform.Y = newY;
        }

        private void UpdateZoomText()
        {
            if (ViewModel == null) return;
            ViewModel.ZoomLevelText = $"Zoom: {ImageScaleTransform.ScaleX:P0}";
        }

        private void ClassRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton radioButton) return;
            if (ViewModel?.SelectedMask == null) return;
            if (radioButton.DataContext is not Models.MaskClass selectedClass) return;

            // Avoid redundant updates
            if (ViewModel.SelectedMask.MaskClass != selectedClass)
            {
                ViewModel.SelectedMask.MaskClass = selectedClass;
            }
        }

        private void DrawOnMask(Point position)
        {
            if (ViewModel?.SelectedMask?.MaskImage is not WriteableBitmap bitmap) return;

            var brushSize = 10; // A reasonable default brush size
            var color = ViewModel.SelectedMask.MaskClass?.DisplayColor ?? Colors.Red;

            // Pre-calculate color components for performance
            byte b = color.B;
            byte g = color.G;
            byte r = color.R;
            byte a = color.A;

            try
            {
                bitmap.Lock();

                int startX = (int)position.X - brushSize / 2;
                int startY = (int)position.Y - brushSize / 2;

                for (int i = 0; i < brushSize; i++)
                {
                    for (int j = 0; j < brushSize; j++)
                    {
                        int x = startX + i;
                        int y = startY + j;

                        if (x >= 0 && x < bitmap.PixelWidth && y >= 0 && y < bitmap.PixelHeight)
                        {
                            unsafe
                            {
                                IntPtr pBackBuffer = bitmap.BackBuffer;
                                pBackBuffer += y * bitmap.BackBufferStride;
                                pBackBuffer += x * 4;
                                *((int*)pBackBuffer) = (a << 24) | (r << 16) | (g << 8) | b;
                            }
                        }
                    }
                }

                // Specify the area of the bitmap that changed
                bitmap.AddDirtyRect(new Int32Rect(startX, startY, brushSize, brushSize));
            }
            finally
            {
                bitmap.Unlock();
            }
        }

        private void UpdateStatusBar(Point mousePos, double imageControlActualWidth, double imageControlActualHeight)
        {
            if (ViewModel != null)
            {
                if (mousePos.X >= 0 && mousePos.Y >= 0 &&
                    mousePos.X < imageControlActualWidth && mousePos.Y < imageControlActualHeight &&
                    ViewModel.MainImage is BitmapSource bitmap)
                {
                    double pixelX = mousePos.X * (bitmap.PixelWidth / imageControlActualWidth);
                    double pixelY = mousePos.Y * (bitmap.PixelHeight / imageControlActualHeight);

                    ViewModel.MousePositionText = $"X: {pixelX:F0}, Y: {pixelY:F0}";

                    if (pixelX >= 0 && pixelY >= 0 && pixelX < bitmap.PixelWidth && pixelY < bitmap.PixelHeight)
                    {
                        try
                        {
                            var croppedBitmap = new CroppedBitmap(bitmap, new Int32Rect((int)pixelX, (int)pixelY, 1, 1));
                            var pixels = new byte[4];
                            croppedBitmap.CopyPixels(pixels, 4, 0);

                            byte b = pixels[0];
                            byte g = pixels[1];
                            byte r = pixels[2];
                            
                            double grayscale = 0.299 * r + 0.587 * g + 0.114 * b;
                            ViewModel.PixelValueText = $"R:{r} G:{g} B:{b} (GV:{grayscale:F0})";
                        }
                        catch
                        {
                            ViewModel.PixelValueText = "R:--- G:--- B:--- (GV:---)";
                        }
                    }
                }
                else
                {
                    ViewModel.MousePositionText = "X: ---, Y: ---";
                    ViewModel.PixelValueText = "R:--- G:--- B:--- (GV:---)";
                }
            }
        }
    }
}