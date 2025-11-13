using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using LuMack.Models;

namespace LuMack.Services
{
    public class PatchGenerationService
    {
        private record MaskInfo(BitmapSource MaskImage, Color DisplayColor);

        public Task<List<PatchPoint>> GenerateAsync(ImageSource mainImage, ObservableCollection<Mask> masks, int numPoints, int patchSize)
        {
            if (mainImage is not BitmapSource sourceBitmap)
            {
                return Task.FromResult(new List<PatchPoint>());
            }

            var sourceBitmapClone = sourceBitmap.Clone();
            sourceBitmapClone.Freeze();

            var maskInfos = new List<MaskInfo>();
            if (masks != null)
            {
                foreach (var mask in masks.Where(m => m.IsVisible && m.MaskImage != null && m.MaskClass != null))
                {
                    var maskImageClone = mask.MaskImage.Clone();
                    maskImageClone.Freeze();
                    maskInfos.Add(new MaskInfo(maskImageClone, mask.MaskClass.DisplayColor));
                }
            }

            return Task.Run(() =>
            {
                // 1. Create a single composite mask image from all visible masks
                var compositeMask = CreateCompositeMask(sourceBitmapClone, maskInfos);
                compositeMask.Freeze();

                // 2. Generate random coordinates and create patches
                var points = new List<PatchPoint>();
                var random = new Random();
                int imageWidth = sourceBitmapClone.PixelWidth;
                int imageHeight = sourceBitmapClone.PixelHeight;

                for (int i = 0; i < numPoints; i++)
                {
                    if (imageWidth <= patchSize || imageHeight <= patchSize) continue;

                    int x = random.Next(0, imageWidth - patchSize);
                    int y = random.Next(0, imageHeight - patchSize);
                    var coordinate = new Point(x, y);

                    var rect = new Int32Rect(x, y, patchSize, patchSize);

                    // Create image patch
                    var imagePatch = new CroppedBitmap(sourceBitmapClone, rect);
                    imagePatch.Freeze();

                    // Create mask patch from the composite mask
                    var maskPatch = new CroppedBitmap(compositeMask, rect);
                    maskPatch.Freeze();

                    points.Add(new PatchPoint
                    {
                        Coordinate = coordinate,
                        ImagePatch = imagePatch,
                        MaskPatch = maskPatch
                    });
                }

                return points;
            });
        }

        private WriteableBitmap CreateCompositeMask(BitmapSource sourceBitmap, IEnumerable<MaskInfo> masks)
        {
            var compositeBitmap = new WriteableBitmap(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);

            compositeBitmap.Lock();
            try
            {
                foreach (var mask in masks)
                {
                    var sourceMask = mask.MaskImage;
                    if (sourceMask == null) continue;

                    var displayColor = mask.DisplayColor;
                    int colorInt = (displayColor.A << 24) | (displayColor.R << 16) | (displayColor.G << 8) | displayColor.B;

                    int sourceStride = (sourceMask.PixelWidth * sourceMask.Format.BitsPerPixel + 7) / 8;
                    byte[] sourcePixels = new byte[sourceMask.PixelHeight * sourceStride];
                    sourceMask.CopyPixels(sourcePixels, sourceStride, 0);

                    int finalStride = compositeBitmap.BackBufferStride;

                    unsafe
                    {
                        int* pFinalMap = (int*)compositeBitmap.BackBuffer;

                        for (int y = 0; y < sourceMask.PixelHeight; y++)
                        {
                            for (int x = 0; x < sourceMask.PixelWidth; x++)
                            {
                                byte alpha = sourcePixels[y * sourceStride + x * 4 + 3];
                                if (alpha > 0)
                                {
                                    *(pFinalMap + y * (finalStride / 4) + x) = colorInt;
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                compositeBitmap.AddDirtyRect(new Int32Rect(0, 0, sourceBitmap.PixelWidth, sourceBitmap.PixelHeight));
                compositeBitmap.Unlock();
            }
            return compositeBitmap;
        }

        public Task UpdateMaskPatchesAsync(ImageSource mainImage, ObservableCollection<Mask> masks, IEnumerable<PatchPoint> patchPoints, int patchSize)
        {
            if (mainImage is not BitmapSource sourceBitmap)
            {
                return Task.CompletedTask;
            }

            var sourceBitmapClone = sourceBitmap.Clone();
            sourceBitmapClone.Freeze();

            var maskInfos = new List<MaskInfo>();
            if (masks != null)
            {
                foreach (var mask in masks.Where(m => m.IsVisible && m.MaskImage != null && m.MaskClass != null))
                {
                    var maskImageClone = mask.MaskImage.Clone();
                    maskImageClone.Freeze();
                    maskInfos.Add(new MaskInfo(maskImageClone, mask.MaskClass.DisplayColor));
                }
            }

            var pointsToUpdate = patchPoints.ToList();

            return Task.Run(() =>
            {
                var compositeMask = CreateCompositeMask(sourceBitmapClone, maskInfos);
                compositeMask.Freeze();

                foreach (var point in pointsToUpdate)
                {
                    var rect = new Int32Rect((int)point.Coordinate.X, (int)point.Coordinate.Y, patchSize, patchSize);

                    if (rect.X < 0 || rect.Y < 0 || rect.X + rect.Width > compositeMask.PixelWidth || rect.Y + rect.Height > compositeMask.PixelHeight)
                    {
                        continue;
                    }

                    var newMaskPatch = new CroppedBitmap(compositeMask, rect);
                    newMaskPatch.Freeze();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        point.MaskPatch = newMaskPatch;
                    });
                }
            });
        }
    }
}
