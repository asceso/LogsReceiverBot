using Models.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class ViewTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ViewsPayload.ViewTypes vt && parameter is string)
            {
                return Enum.GetName(typeof(ViewsPayload.ViewTypes), vt) == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return Visibility.Hidden;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}