using Extensions;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b_value && parameter is string s_arg)
            {
                if (!s_arg.IsNullOrEmpty() && s_arg == "revert")
                {
                    return b_value ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return b_value ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}