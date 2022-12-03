﻿using Models.Enums;
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
                if (values[0] is bool isBanned && values[1] is bool showBanned)
                {
                    return !showBanned && isBanned ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            if (values.Length == 3)
            {
                if (values[0] is CheckStatus.CookieCheckStatus status &&
                    values[1] is bool isClosedShow &&
                    values[2] is bool isOtherShow)
                {
                    if (status == CheckStatus.CookieCheckStatus.End || status == CheckStatus.CookieCheckStatus.EndNoValid)
                    {
                        if (!isClosedShow)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else if (!isOtherShow)
                    {
                        return Visibility.Collapsed;
                    }
                    return Visibility.Visible;
                }
            }
            if (values.Length == 4)
            {
                if (values[0] is CheckStatus.ManualCheckStatus status &&
                    values[1] is bool isClosedChecksShow &&
                    values[2] is bool isErrorChecksShow &&
                    values[3] is bool isOtherChecksShow)
                {
                    if (status == CheckStatus.ManualCheckStatus.End || status == CheckStatus.ManualCheckStatus.EndNoValid)
                    {
                        if (!isClosedChecksShow)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else if (status == CheckStatus.ManualCheckStatus.Error || status == CheckStatus.ManualCheckStatus.NoAnyUnique)
                    {
                        if (!isErrorChecksShow)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else if (!isOtherChecksShow)
                    {
                        return Visibility.Collapsed;
                    }
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
    }
}