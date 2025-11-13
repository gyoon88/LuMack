using LuMack.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace LuMack.Utils
{
    public class MaskLoaderService
    {
        public ObservableCollection<Mask> LoadMasksFromXml(XDocument doc, IEnumerable<MaskClass> availableClasses, int imageWidth, int imageHeight)
        {
            var concurrentBag = new ConcurrentBag<Mask>();
            var unclassifiedClass = availableClasses.FirstOrDefault(c => c.Name == "Unclassified") 
                                    ?? availableClasses.FirstOrDefault() 
                                    ?? new MaskClass { Name = "Default", DisplayColor = Colors.Red };

            doc.Descendants("RecipeType_Mask").AsParallel().ForAll(recipeNode =>
            {
                try
                {
                    var newMask = new Mask();
                    var guid = recipeNode.Element("Guid")?.Value;
                    newMask.Name = !string.IsNullOrEmpty(guid) ? $"Mask {guid.Substring(0, 8)}..." : $"Mask {Guid.NewGuid().ToString().Substring(0, 8)}";
                    newMask.MaskClass = unclassifiedClass;

                    // Create a transparent bitmap for the mask layer
                    var maskBitmap = new WriteableBitmap(imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32, null);

                    // Draw the RLE data onto the bitmap
                    DrawRleDataOnBitmap(maskBitmap, recipeNode.Descendants("Mask"), unclassifiedClass.DisplayColor);

                    maskBitmap.Freeze(); // Crucial for performance and cross-thread safety
                    newMask.MaskImage = maskBitmap;
                    
                    concurrentBag.Add(newMask);
                }
                catch (Exception ex)
                {
                    // Log the exception to the console for debugging
                    Console.WriteLine($"Error parsing mask: {ex.Message}");
                }
            });

            return new ObservableCollection<Mask>(concurrentBag);
        }

        private void DrawRleDataOnBitmap(WriteableBitmap bitmap, IEnumerable<XElement> maskComponents, Color color)
        {
            // Pre-calculate color components for performance
            byte b = color.B;
            byte g = color.G;
            byte r = color.R;
            byte a = color.A; // Assuming full opacity for the mask pixels themselves

            try
            {
                bitmap.Lock();

                foreach (var maskNode in maskComponents)
                {
                    int absTop = (int)maskNode.Element("p_nBoundTop");
                    int absLeft = (int)maskNode.Element("p_nBoundLeft");

                    foreach (var line in maskNode.Descendants("RecipeType_PointLine"))
                    {
                        int relY = (int)line.Element("StartPoint").Element("Y");
                        int relX = (int)line.Element("StartPoint").Element("X");
                        int length = (int)line.Element("Length");

                        int y = absTop + relY;
                        int startX = absLeft + relX;
                        int endX = startX + length;

                        // Boundary checks
                        if (y < 0 || y >= bitmap.PixelHeight) continue;
                        if (startX >= bitmap.PixelWidth || endX <= 0) continue;

                        int clampedStartX = Math.Max(0, startX);
                        int clampedEndX = Math.Min(bitmap.PixelWidth, endX);

                        for (int x = clampedStartX; x < clampedEndX; x++)
                        {
                            unsafe
                            {
                                // Get a pointer to the back buffer
                                IntPtr pBackBuffer = bitmap.BackBuffer;

                                // Find the address of the pixel to draw
                                pBackBuffer += y * bitmap.BackBufferStride;
                                pBackBuffer += x * 4;

                                // Assign the color data
                                *((int*)pBackBuffer) = (a << 24) | (r << 16) | (g << 8) | b;
                            }
                        }
                    }
                }

                // Specify the area of the bitmap that changed
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }
}
