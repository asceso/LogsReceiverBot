using Models.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class ViewTypeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ViewsPayload.ViewTypes vt && parameter is string)
            {
                return Enum.GetName(typeof(ViewsPayload.ViewTypes), vt) == parameter.ToString();
            }
            else
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}