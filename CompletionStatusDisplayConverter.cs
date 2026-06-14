using Playnite.SDK.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Playlist
{
    public class CompletionStatusDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CompletionStatus completionStatus)
            {
                return CompletionStatusLocalization.LocalizeDisplayName(completionStatus?.Name);
            }

            return CompletionStatusLocalization.LocalizeDisplayName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
