using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace LuMack.Converters
{
    public class ObjectEqualityToBooleanConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return false;
            }
            
            if (values.Any(v => v == System.Windows.DependencyProperty.UnsetValue))
            {
                return false;
            }

            // Check if all values are equal to the first one.
            return values.All(v => Equals(values[0], v));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
