using Models.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace BotMainApp.Converters
{
    public class EnumStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CheckStatus.ManualCheckStatus manualCheckEnum)
            {
                if (parameter is string s_param)
                {
                    List<int> targetStatuses = new();
                    if (s_param.Contains(' '))
                    {
                        string[] s_params = s_param.Split(' ');
                        foreach (string p_param in s_params)
                        {
                            if (int.TryParse(p_param, out int statusValue))
                            {
                                targetStatuses.Add(statusValue);
                            }
                        }
                    }
                    return targetStatuses.Any(ts => ts == (int)manualCheckEnum) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            if (value is PayoutStatus.PayoutStatusEnum payoutEnum)
            {
                if (parameter is string s_param && int.TryParse(s_param, out int payoutValue))
                {
                    return (int)payoutEnum == payoutValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
    }
}