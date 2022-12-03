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
            if (value is CheckStatus.CookieCheckStatus cookieEnum)
            {
                return CheckStatus.GetEnumValue(cookieEnum);
            }
            if (value is CheckStatus.ManualCheckStatus manualCheckEnum)
            {
                return CheckStatus.GetEnumValue(manualCheckEnum);
            }
            if (value is PayoutStatus.PayoutStatusEnum payoutEnum)
            {
                return PayoutStatus.GetEnumValue(payoutEnum);
            }
            else return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}