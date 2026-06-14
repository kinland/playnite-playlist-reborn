using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Playlist
{
    /// <summary>
    /// True when a submenu status id matches the right-clicked game's completion status (no-op selection).
    /// </summary>
    public class CompletionStatusIsCurrentConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return false;
            }

            if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
            {
                return false;
            }

            if (!TryGetGuid(values[0], out Guid statusId) || !TryGetGuid(values[1], out Guid gameStatusId))
            {
                return false;
            }

            return statusId == gameStatusId;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return Array.ConvertAll(targetTypes, _ => (object)DependencyProperty.UnsetValue);
        }

        private static bool TryGetGuid(object value, out Guid guid)
        {
            if (value is Guid direct)
            {
                guid = direct;
                return true;
            }

            Guid? nullable = value as Guid?;
            if (nullable.HasValue)
            {
                guid = nullable.Value;
                return true;
            }

            guid = default;
            return false;
        }
    }
}
