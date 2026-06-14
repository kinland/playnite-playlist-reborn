using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace Playlist
{
    /// <summary>
    /// Builds Playlist quick settings for the column header context menu and Extensions main menu.
    /// </summary>
    internal static class PlaylistQuickAccessMenuBuilder
    {
        private const string CheckmarkPrefix = "\u2713  ";
        // Playnite strips the leading '@' and nests under extensionsItem.Items via MenuHelpers.GenerateMenuParents.
        // "@" alone puts items at Extensions root; "@Playlist" creates Extensions → Playlist; "@Playlist|…" adds submenus.
        private const string ExtensionMenuRoot = "@Playlist";

        private static readonly string[] StandardColumnKeys =
        {
            PlaylistColumnWidthLayout.RankColumnKey,
            PlaylistColumnWidthLayout.PlaytimeColumnKey,
            PlaylistColumnWidthLayout.CompletionStatusColumnKey,
            PlaylistColumnWidthLayout.LastPlayedColumnKey,
            PlaylistColumnWidthLayout.LastActivityColumnKey,
        };

        internal static ContextMenu BuildColumnVisibilityContextMenu()
        {
            if (!TryGetSettings(out PlaylistSettings settings))
            {
                return null;
            }

            ContextMenu menu = new ContextMenu
            {
                StaysOpen = true,
            };
            AppendColumnVisibilityItems(menu.Items, settings);
            return menu;
        }

        internal static IEnumerable<MainMenuItem> BuildExtensionMainMenuItems()
        {
            if (!TryGetSettings(out PlaylistSettings settings))
            {
                yield break;
            }

            settings.RefreshHowLongToBeatInstallState();
            settings.RefreshLanguageOptions();

            string columnsSection = ExtensionSubmenuSection(PlaylistLocalization.GetString("LOCPlaylist_Menu_Columns"));
            foreach (MainMenuItem item in BuildColumnVisibilityMainMenuItems(settings, columnsSection))
            {
                yield return item;
            }

            string languageSection = ExtensionSubmenuSection(PlaylistLocalization.GetLanguageOverrideLabel());
            foreach (MainMenuItem item in BuildLanguageMainMenuItems(settings, languageSection))
            {
                yield return item;
            }

            yield return new MainMenuItem
            {
                MenuSection = ExtensionMenuRoot,
                Description = FormatToggleDescription(
                    settings.HowLongToBeatIntegrationCheckboxDisplay,
                    PlaylistLocalization.GetString("LOCPlaylist_Settings_EnableHLTBIntegration")),
                Action = _ => OnHowLongToBeatIntegrationMenuActivated(),
            };

            yield return new MainMenuItem
            {
                MenuSection = ExtensionMenuRoot,
                Description = FormatToggleDescription(
                    settings.SyncSearchWithMainPanel,
                    PlaylistLocalization.GetSyncSearchWithMainPanelLabel()),
                Action = _ => ToggleSyncSearchWithMainPanel(),
            };

            yield return new MainMenuItem { MenuSection = ExtensionMenuRoot, Description = "-" };

            yield return new MainMenuItem
            {
                MenuSection = ExtensionMenuRoot,
                Description = PlaylistLocalization.GetString("LOCPlaylist_Menu_OpenSettings"),
                Action = _ => OpenPluginSettings(fromHltbColumnMenu: false),
            };
        }

        private static string ExtensionSubmenuSection(string localizedSubmenuTitle) =>
            ExtensionMenuRoot + "|" + localizedSubmenuTitle;

        private static void AppendColumnVisibilityItems(ItemCollection items, PlaylistSettings settings)
        {
            foreach (string columnKey in StandardColumnKeys)
            {
                items.Add(BuildColumnToggleMenuItem(columnKey, settings));
            }

            items.Add(BuildHowLongToBeatColumnMenuItem(settings));
        }

        private static IEnumerable<MainMenuItem> BuildColumnVisibilityMainMenuItems(PlaylistSettings settings, string menuSection)
        {
            foreach (string columnKey in StandardColumnKeys)
            {
                yield return BuildColumnToggleMainMenuItem(menuSection, columnKey, settings);
            }

            yield return BuildHowLongToBeatColumnMainMenuItem(settings, menuSection);
        }

        private static IEnumerable<MainMenuItem> BuildLanguageMainMenuItems(PlaylistSettings settings, string menuSection)
        {
            string activeLocaleId = settings.LanguageOverrideLocaleId ?? string.Empty;
            foreach (PlaylistLanguageOption option in settings.LanguageOptions)
            {
                string localeId = option.LocaleId ?? string.Empty;
                bool isSelected = string.Equals(activeLocaleId, localeId, StringComparison.OrdinalIgnoreCase);
                yield return new MainMenuItem
                {
                    MenuSection = menuSection,
                    Description = FormatToggleDescription(isSelected, option.DisplayName),
                    Action = _ => ApplyLanguageOverride(localeId),
                };
            }
        }

        private static MenuItem BuildColumnToggleMenuItem(string columnKey, PlaylistSettings settings)
        {
            MenuItem item = new MenuItem
            {
                Header = PlaylistColumnVisibilitySettings.GetColumnLabel(columnKey),
                IsCheckable = true,
                IsChecked = PlaylistColumnVisibilitySettings.IsColumnVisible(settings, columnKey),
            };

            item.Click += (s, e) => OnColumnVisibilityToggled(columnKey);
            return item;
        }

        private static MainMenuItem BuildColumnToggleMainMenuItem(
            string menuSection,
            string columnKey,
            PlaylistSettings settings)
        {
            return new MainMenuItem
            {
                MenuSection = menuSection,
                Description = FormatToggleDescription(
                    PlaylistColumnVisibilitySettings.IsColumnVisible(settings, columnKey),
                    PlaylistColumnVisibilitySettings.GetColumnLabel(columnKey)),
                Action = _ => OnColumnVisibilityToggled(columnKey),
            };
        }

        private static MenuItem BuildHowLongToBeatColumnMenuItem(PlaylistSettings settings)
        {
            string header = HltbColumnHeaderLabels.GetColumnBaseText();
            if (TryGetHowLongToBeatDisabledAction(settings, out string disabledToolTip, out Action onClick))
            {
                return BuildHowLongToBeatDisabledAppearanceMenuItem(header, disabledToolTip, onClick);
            }

            return BuildColumnToggleMenuItem(PlaylistColumnWidthLayout.HowLongToBeatColumnKey, settings);
        }

        private static MainMenuItem BuildHowLongToBeatColumnMainMenuItem(PlaylistSettings settings, string menuSection)
        {
            string header = HltbColumnHeaderLabels.GetColumnBaseText();
            if (TryGetHowLongToBeatDisabledAction(settings, out _, out Action onClick))
            {
                return new MainMenuItem
                {
                    MenuSection = menuSection,
                    Description = header,
                    Action = _ => onClick(),
                };
            }

            return BuildColumnToggleMainMenuItem(menuSection, PlaylistColumnWidthLayout.HowLongToBeatColumnKey, settings);
        }

        private static bool TryGetHowLongToBeatDisabledAction(
            PlaylistSettings settings,
            out string disabledToolTip,
            out Action onClick)
        {
            switch (HowLongToBeatAddonNavigation.GetInstallState(Playlist.StaticPlayniteApi))
            {
                case HltbInstallState.NotInstalled:
                    disabledToolTip = ResourceProvider.GetString("LOCPlaylist_HLTB_OpenAddonsToInstall");
                    onClick = () => HowLongToBeatAddonNavigation.OpenBrowseAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    return true;
                case HltbInstallState.InstalledDisabled:
                    disabledToolTip = ResourceProvider.GetString("LOCPlaylist_HLTB_OpenAddonsToEnable");
                    onClick = () => HowLongToBeatAddonNavigation.OpenInstalledAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    return true;
            }

            if (!settings.EnableHowLongToBeatIntegration)
            {
                disabledToolTip = ResourceProvider.GetString("LOCPlaylist_HLTB_OpenSettingsToEnable");
                onClick = () => OpenPluginSettings(fromHltbColumnMenu: true);
                return true;
            }

            disabledToolTip = null;
            onClick = null;
            return false;
        }

        private static MenuItem BuildHowLongToBeatDisabledAppearanceMenuItem(
            string header,
            string disabledToolTip,
            Action onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = false,
                IsEnabled = true,
                Opacity = 0.55,
            };

            if (!string.IsNullOrEmpty(disabledToolTip))
            {
                item.ToolTip = disabledToolTip;
            }

            item.Click += (s, e) =>
            {
                item.IsChecked = false;
                onClick();
            };
            return item;
        }

        private static void OnColumnVisibilityToggled(string columnKey)
        {
            if (!TryGetSettings(out PlaylistSettings settings)
                || !PlaylistColumnVisibilitySettings.TryToggle(settings, columnKey))
            {
                return;
            }

            PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();
        }

        private static void OnHowLongToBeatIntegrationMenuActivated()
        {
            if (!TryGetSettings(out PlaylistSettings settings))
            {
                return;
            }

            switch (settings.HowLongToBeatInstallState)
            {
                case HltbInstallState.InstalledDisabled:
                    HowLongToBeatAddonNavigation.OpenInstalledAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    settings.RefreshHowLongToBeatInstallState();
                    return;
                case HltbInstallState.NotInstalled:
                    HowLongToBeatAddonNavigation.OpenBrowseAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    settings.RefreshHowLongToBeatInstallState();
                    return;
            }

            if (!settings.IsHowLongToBeatAvailable)
            {
                return;
            }

            settings.EnableHowLongToBeatIntegration = !settings.EnableHowLongToBeatIntegration;
            PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();
        }

        private static void ToggleSyncSearchWithMainPanel()
        {
            if (!TryGetSettings(out PlaylistSettings settings))
            {
                return;
            }

            settings.SyncSearchWithMainPanel = !settings.SyncSearchWithMainPanel;
            PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();
        }

        private static void ApplyLanguageOverride(string localeId)
        {
            if (!TryGetSettings(out PlaylistSettings settings))
            {
                return;
            }

            settings.LanguageOverrideLocaleId = string.IsNullOrEmpty(localeId) ? null : localeId;
            PlaylistLocalizationOverride.ApplyFromSettings(settings);
            PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();
        }

        internal static void OpenPluginSettings(bool fromHltbColumnMenu)
        {
            PlaylistSettings settings = Playlist.StaticSettings as PlaylistSettings;
            if (fromHltbColumnMenu)
            {
                settings?.MarkPendingShowHowLongToBeatColumnFromHeaderMenu();
            }

            Playlist.StaticPluginInstance?.OpenSettingsView();
            settings?.ExpireSessionOnlyHltbPendingFlags();
            PlaylistColumnVisibilitySettings.PersistAndApplyOpenView();
        }

        private static bool TryGetSettings(out PlaylistSettings settings)
        {
            settings = Playlist.StaticSettings;
            return settings != null;
        }

        private static string FormatToggleDescription(bool isOn, string label)
        {
            return isOn ? CheckmarkPrefix + label : label;
        }
    }
}
