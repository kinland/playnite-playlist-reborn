using System;
using System.Collections.Generic;

namespace Playlist
{
    /// <summary>
    /// Shared column visibility read/write used by the header context menu and Extensions quick settings.
    /// </summary>
    internal static class PlaylistColumnVisibilitySettings
    {
        private sealed class ColumnBinding
        {
            internal string ColumnKey { get; set; }
            internal string LabelResourceKey { get; set; }
            internal Func<PlaylistSettings, bool> GetVisible { get; set; }
            internal Action<PlaylistSettings, bool> SetVisible { get; set; }
        }

        private static readonly ColumnBinding[] StandardColumnBindings =
        {
            new ColumnBinding
            {
                ColumnKey = PlaylistColumnWidthLayout.RankColumnKey,
                LabelResourceKey = "LOCPlaylist_Column_Rank",
                GetVisible = settings => settings.ShowRankColumn,
                SetVisible = (settings, visible) => settings.ShowRankColumn = visible,
            },
            new ColumnBinding
            {
                ColumnKey = PlaylistColumnWidthLayout.PlaytimeColumnKey,
                LabelResourceKey = "LOCTimePlayed",
                GetVisible = settings => settings.ShowPlaytimeColumn,
                SetVisible = (settings, visible) => settings.ShowPlaytimeColumn = visible,
            },
            new ColumnBinding
            {
                ColumnKey = PlaylistColumnWidthLayout.CompletionStatusColumnKey,
                LabelResourceKey = "LOCCompletionStatus",
                GetVisible = settings => settings.ShowCompletionStatusColumn,
                SetVisible = (settings, visible) => settings.ShowCompletionStatusColumn = visible,
            },
            new ColumnBinding
            {
                ColumnKey = PlaylistColumnWidthLayout.LastPlayedColumnKey,
                LabelResourceKey = "LOCPlaylist_LastPlayedColumn",
                GetVisible = settings => settings.ShowLastPlayedColumn,
                SetVisible = (settings, visible) => settings.ShowLastPlayedColumn = visible,
            },
            new ColumnBinding
            {
                ColumnKey = PlaylistColumnWidthLayout.LastActivityColumnKey,
                LabelResourceKey = "LOCPlaylist_LastActivityColumn",
                GetVisible = settings => settings.ShowLastActivityColumn,
                SetVisible = (settings, visible) => settings.ShowLastActivityColumn = visible,
            },
        };

        private static readonly Dictionary<string, ColumnBinding> StandardColumnBindingsByKey =
            BuildBindingLookup(StandardColumnBindings);

        internal static string GetColumnLabel(string columnKey)
        {
            if (columnKey == PlaylistColumnWidthLayout.HowLongToBeatColumnKey)
            {
                return HltbColumnHeaderLabels.GetColumnBaseText();
            }

            ColumnBinding binding = TryGetStandardBinding(columnKey);
            return binding != null
                ? PlaylistLocalization.GetString(binding.LabelResourceKey)
                : columnKey;
        }

        internal static bool IsHowLongToBeatColumnVisible(PlaylistSettings settings)
        {
            if (settings == null || !settings.EnableHowLongToBeatIntegration)
            {
                return false;
            }

            if (!HowLongToBeatAddonNavigation.IsPluginEnabledInPlaynite(Playlist.StaticPlayniteApi))
            {
                return false;
            }

            return settings.ShowHowLongToBeatColumn;
        }

        internal static bool IsColumnVisible(PlaylistSettings settings, string columnKey)
        {
            if (settings == null)
            {
                return false;
            }

            if (columnKey == PlaylistColumnWidthLayout.HowLongToBeatColumnKey)
            {
                return IsHowLongToBeatColumnVisible(settings);
            }

            ColumnBinding binding = TryGetStandardBinding(columnKey);
            return binding != null && binding.GetVisible(settings);
        }

        internal static bool TrySetVisibility(PlaylistSettings settings, string columnKey, bool visible)
        {
            if (settings == null
                || string.IsNullOrEmpty(columnKey)
                || columnKey == PlaylistColumnWidthLayout.NameColumnKey
                || columnKey == PlaylistColumnWidthLayout.IconColumnKey)
            {
                return false;
            }

            if (columnKey == PlaylistColumnWidthLayout.HowLongToBeatColumnKey)
            {
                if (settings.ShowHowLongToBeatColumn == visible)
                {
                    return false;
                }

                settings.ShowHowLongToBeatColumn = visible;
                return true;
            }

            ColumnBinding binding = TryGetStandardBinding(columnKey);
            if (binding == null || binding.GetVisible(settings) == visible)
            {
                return false;
            }

            binding.SetVisible(settings, visible);
            return true;
        }

        internal static bool TryToggle(PlaylistSettings settings, string columnKey)
        {
            return TrySetVisibility(settings, columnKey, !IsColumnVisible(settings, columnKey));
        }

        internal static void PersistAndApplyOpenView()
        {
            Playlist.StaticPluginInstance?.PersistSettings();
            Playlist.StaticPluginInstance?.ApplySettingsToOpenView();
        }

        private static Dictionary<string, ColumnBinding> BuildBindingLookup(IEnumerable<ColumnBinding> bindings)
        {
            var lookup = new Dictionary<string, ColumnBinding>(StringComparer.Ordinal);
            foreach (ColumnBinding binding in bindings)
            {
                lookup[binding.ColumnKey] = binding;
            }

            return lookup;
        }

        private static ColumnBinding TryGetStandardBinding(string columnKey)
        {
            StandardColumnBindingsByKey.TryGetValue(columnKey, out ColumnBinding binding);
            return binding;
        }
    }
}
