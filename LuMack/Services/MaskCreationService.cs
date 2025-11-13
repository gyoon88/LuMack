using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuMack.Models;

namespace LuMack.Services
{
    public class MaskCreationService
    {
        public Mask? CreateMaskFromGV(BitmapSource bitmap, GVCreationParameters gvParams, MaskClass defaultClass)
        {
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

                if (maskPixels.Count == 0) return null;

                var newMask = new Mask
                {
                    Name = $"GV Mask",
                    MaskClass = defaultClass,
                };

                var maskBitmap = new WriteableBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);
                DrawPointsOnBitmap(maskBitmap, maskPixels, newMask.MaskClass.DisplayColor);
                maskBitmap.Freeze();
                newMask.MaskImage = maskBitmap;

                return newMask;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during GV mask creation: {ex.Message}", "Error");
                return null;
            }
        }

        private void DrawPointsOnBitmap(WriteableBitmap bitmap, List<Point> points, Color color)
        {
            byte b = color.B;
            byte g = color.G;
            byte r = color.R;
            byte a = color.A;

            try
            {
                bitmap.Lock();
                foreach (var p in points)
                {
                    int x = (int)p.X;
                    int y = (int)p.Y;

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
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                bitmap.Unlock();
            }
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
            if (bytesPerPixel == 4) { b = pixels[index]; g = pixels[index + 1]; r = pixels[index + 2]; }
            else if (bytesPerPixel == 3) { b = pixels[index]; g = pixels[index + 1]; r = pixels[index + 2]; }
            else if (bytesPerPixel == 1) { return pixels[index]; }
            else { return 0; }

            return (byte)(0.299 * r + 0.587 * g + 0.114 * b);
        }
    }
}
