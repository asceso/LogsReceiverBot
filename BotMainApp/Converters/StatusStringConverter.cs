using Models.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class StatusStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CheckStatus.ManualCheckStatus enumStatus)
            {
                return CheckStatus.GetEnumValue(enumStatus);
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}