using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class MultiBooleanToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2)
            {
                if (values[1] is bool showBanned)
                {
                    if (values[0] is bool isBanned)
                    {
                        return !showBanned && isBanned ? Visibility.Collapsed : Visibility.Visible;
                    }
                }
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
    }
}