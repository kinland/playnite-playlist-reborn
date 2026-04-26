using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Playnite.SDK.Models;

namespace Playlist
{
    /// <summary>
    /// Shows 1-based rank from the game's index in the playlist. Do not use
    /// <see cref="System.Windows.Controls.ItemsControl.AlternationIndex"/> for row numbers — it wraps incorrectly
    /// when items move (especially to the top) with large <c>AlternationCount</c>.
    /// </summary>
    public class GamePlaylistRankConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return string.Empty;
            }

            if (!(values[0] is Game game) || !(values[1] is IList<Game> list))
            {
                return string.Empty;
            }

            // values[2] is PlaylistGamesRevision — only used so WPF re-runs this when order changes.

            int index = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], game))
                {
                    index = i;
                    break;
                }
            }

            return index >= 0 ? (index + 1).ToString(culture) : string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
