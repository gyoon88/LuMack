using System.Windows.Media;
using LuMack.Common;

namespace LuMack.Models
{
    public class Mask : BindableBase
    {
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

        private string classLabel = "Unclassified";
        public string ClassLabel
        {
            get => classLabel;
            set => SetProperty(ref classLabel, value);
        }

        private bool isVisible = true;
        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        private Color displayColor = Colors.Red;
        public Color DisplayColor
        {
            get => displayColor;
            set => SetProperty(ref displayColor, value);
        }

        private Geometry? maskGeometry;
        public Geometry? MaskGeometry
        {
            get => maskGeometry;
            set => SetProperty(ref maskGeometry, value);
        }
    }
}

