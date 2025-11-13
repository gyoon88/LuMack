using System.Windows.Media.Imaging;
using LuMack.Common;

namespace LuMack.Models
{
    public class PatchPoint : BindableBase
    {
        private System.Windows.Point coordinate;
        public System.Windows.Point Coordinate
        {
            get => coordinate;
            set => SetProperty(ref coordinate, value);
        }

        private BitmapSource? imagePatch;
        public BitmapSource? ImagePatch
        {
            get => imagePatch;
            set => SetProperty(ref imagePatch, value);
        }

        private BitmapSource? maskPatch;
        public BitmapSource? MaskPatch
        {
            get => maskPatch;
            set => SetProperty(ref maskPatch, value);
        }

        private bool isSelectedToSave = true; // Default to selected
        public bool IsSelectedToSave
        {
            get => isSelectedToSave;
            set => SetProperty(ref isSelectedToSave, value);
        }

        public string DisplayName => $"X: {Coordinate.X}, Y: {Coordinate.Y}";
    }
}
