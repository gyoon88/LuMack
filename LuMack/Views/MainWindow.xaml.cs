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

            // Center the image
            var viewportWidth = ImageBorder.ActualWidth;
            var viewportHeight = ImageBorder.ActualHeight;
            var contentWidth = MainImage.RenderSize.Width;
            var contentHeight = MainImage.RenderSize.Height;

            ImageTranslateTransform.X = (viewportWidth - contentWidth) / 2;
            ImageTranslateTransform.Y = (viewportHeight - contentHeight) / 2;
            
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
            if (ViewModel?.MainImage == null || !_isInitialised) return;

            var scale = ImageScaleTransform.ScaleX;
            var viewportWidth = ImageBorder.ActualWidth;
            var viewportHeight = ImageBorder.ActualHeight;

            var contentWidth = MainImage.RenderSize.Width * scale;
            var contentHeight = MainImage.RenderSize.Height * scale;

            var minX = viewportWidth - contentWidth;
            var minY = viewportHeight - contentHeight;

            var currentX = ImageTranslateTransform.X;
            var currentY = ImageTranslateTransform.Y;

            var newX = (contentWidth < viewportWidth) ? (viewportWidth - contentWidth) / 2 : Math.Max(minX, Math.Min(0, currentX));
            var newY = (contentHeight < viewportHeight) ? (viewportHeight - contentHeight) / 2 : Math.Max(minY, Math.Min(0, currentY));

            ImageTranslateTransform.X = newX;
            ImageTranslateTransform.Y = newY;
        }

        private void UpdateZoomText()
        {
            if (ViewModel == null) return;
            ViewModel.ZoomLevelText = $"Zoom: {ImageScaleTransform.ScaleX:P0}";
        }

        private void DrawOnMask(Point position)
        {
            if (ViewModel?.SelectedMask == null) return;

            var geometryGroup = ViewModel.SelectedMask.MaskGeometry as GeometryGroup;
            if (geometryGroup == null)
            {
                geometryGroup = new GeometryGroup();
                ViewModel.SelectedMask.MaskGeometry = geometryGroup;
            }

            var brushSize = 5;
            var newRect = new RectangleGeometry(new Rect(position.X - brushSize / 2.0, position.Y - brushSize / 2.0, brushSize, brushSize));
            geometryGroup.Children.Add(newRect);
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