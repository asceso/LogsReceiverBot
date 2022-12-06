using Models.Enums;
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
                bool isClosedShow, isErrorShow, isOtherShow;
                if (values[0] is CheckStatus.ManualCheckStatus status &&
                    values[1] is bool bv1_manual_check &&
                    values[2] is bool bv2_manual_check &&
                    values[3] is bool bv3_manual_check)
                {
                    isClosedShow = bv1_manual_check;
                    isErrorShow = bv2_manual_check;
                    isOtherShow = bv3_manual_check;

                    if (status == CheckStatus.ManualCheckStatus.End || status == CheckStatus.ManualCheckStatus.EndNoValid)
                    {
                        if (!isClosedShow)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else if (status == CheckStatus.ManualCheckStatus.Error || status == CheckStatus.ManualCheckStatus.NoAnyUnique)
                    {
                        if (!isErrorShow)
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
                if (values[0] is PayoutStatus.PayoutStatusEnum payoutStatus &&
                    values[1] is bool bv1_payout &&
                    values[2] is bool bv2_payout &&
                    values[3] is bool bv3_payout)
                {
                    isClosedShow = bv1_payout;
                    isErrorShow = bv2_payout;
                    isOtherShow = bv3_payout;

                    if (payoutStatus == PayoutStatus.PayoutStatusEnum.Completed)
                    {
                        if (!isClosedShow)
                        {
                            return Visibility.Collapsed;
                        }
                    }
                    else if (payoutStatus == PayoutStatus.PayoutStatusEnum.Denied)
                    {
                        if (!isErrorShow)
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
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
    }
}