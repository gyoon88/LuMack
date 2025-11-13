using System.Windows.Media;
using LuMack.Common;
using System.Windows.Media.Imaging;
using System.Media;

namespace LuMack.Models
{
    public class Mask : BindableBase
    {
        public Guid Id { get; } = Guid.NewGuid();

        private string name = "New Mask";
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        private string rleData = string.Empty;
        public string RleData
        {
            get => rleData;
            set => SetProperty(ref rleData, value);
        }

        private MaskClass? maskClass;
        public MaskClass? MaskClass
        {
            get => maskClass;
            set => SetProperty(ref maskClass, value);
        }

        private bool isVisible = true;
        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        private BitmapSource? maskImage;
        public BitmapSource? MaskImage
        {
            get => maskImage;
            set => SetProperty(ref maskImage, value);
        }
    }
}

