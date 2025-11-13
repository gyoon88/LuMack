using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using LuMack.Models;

namespace LuMack.Services
{
    public class PatchSavingService
    {
        public Task SaveAsync(IEnumerable<PatchPoint> patchesToSave, string folderPath)
        {
            return Task.Run(() =>
            {
                foreach (var patch in patchesToSave)
                {
                    string baseFileName = $"x{(int)patch.Coordinate.X}-y{(int)patch.Coordinate.Y}";

                    // Save Image Patch
                    if (patch.ImagePatch != null)
                    {
                        string imagePath = Path.Combine(folderPath, $"{baseFileName}_image.png");
                        SaveBitmap(patch.ImagePatch, imagePath);
                    }

                    // Save Mask Patch
                    if (patch.MaskPatch != null)
                    {
                        string maskPath = Path.Combine(folderPath, $"{baseFileName}_mask.png");
                        SaveBitmap(patch.MaskPatch, maskPath);
                    }
                }
            });
        }

        private void SaveBitmap(BitmapSource bitmap, string filePath)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }
    }
}
