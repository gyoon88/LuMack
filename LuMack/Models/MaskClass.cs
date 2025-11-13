using System.Windows.Media;
using LuMack.Common;

namespace LuMack.Models
{
    public class MaskClass : BindableBase
    {
        private string name = "New Class";
        public string Name
        {
            get => name;
            set => SetProperty(ref name, value);
        }

        private Color displayColor = Colors.Green;
        public Color DisplayColor
        {
            get => displayColor;
            set => SetProperty(ref displayColor, value);
        }
    }
}
