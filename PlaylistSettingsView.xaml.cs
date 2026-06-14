using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace Playlist
{
    public partial class PlaylistSettingsView : UserControl
    {
        private bool isApplyingLanguageOverridePreview;
        private bool isSyncingLanguageComboBox;
        private bool languageComboBoxSyncQueued;
        private PlaylistSettings subscribedSettings;

        public PlaylistSettingsView()
        {
            InitializeComponent();
            Loaded += PlaylistSettingsView_Loaded;
            Unloaded += PlaylistSettingsView_Unloaded;
            DataContextChanged += PlaylistSettingsView_DataContextChanged;
        }

        private void PlaylistSettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnsubscribeFromSettings(subscribedSettings);
            subscribedSettings = DataContext as PlaylistSettings;
            SubscribeToSettings(subscribedSettings);
            QueueLanguageComboBoxSync();
        }

        private void PlaylistSettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is PlaylistSettings settings))
            {
                return;
            }

            PlaylistLocalizationOverride.ApplyFromSettings(settings);
            PlaylistLocalizationOverride.MergeInto(this);
            if (settings.TryOfferOsLocaleMismatchPrompt())
            {
                PlaylistLocalizationOverride.ApplyFromSettings(settings);
                PlaylistLocalizationOverride.MergeInto(this);
            }

            QueueLanguageComboBoxSync();
        }

        private void PlaylistSettingsView_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromSettings(subscribedSettings);
            subscribedSettings = null;
            languageComboBoxSyncQueued = false;
        }

        private void SubscribeToSettings(PlaylistSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.PropertyChanged += Settings_PropertyChanged;
        }

        private void UnsubscribeFromSettings(PlaylistSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.PropertyChanged -= Settings_PropertyChanged;
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only re-sync when the option list is rebuilt. Do not react to combo value
            // changes; that creates binding feedback loops and can exhaust WPF weak tables.
            if (e.PropertyName == nameof(PlaylistSettings.LanguageOptions))
            {
                QueueLanguageComboBoxSync();
            }
        }

        private void QueueLanguageComboBoxSync()
        {
            if (!IsLoaded || languageComboBoxSyncQueued)
            {
                return;
            }

            languageComboBoxSyncQueued = true;
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    languageComboBoxSyncQueued = false;
                    SyncLanguageComboBoxSelection();
                }),
                DispatcherPriority.DataBind);
        }

        private void SyncLanguageComboBoxSelection()
        {
            if (isSyncingLanguageComboBox || !(DataContext is PlaylistSettings settings))
            {
                return;
            }

            string expectedLocaleId = settings.LanguageOverrideComboValue ?? string.Empty;
            string currentLocaleId = languageOverrideComboBox.SelectedValue?.ToString() ?? string.Empty;
            if (string.Equals(currentLocaleId, expectedLocaleId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            isSyncingLanguageComboBox = true;
            isApplyingLanguageOverridePreview = true;
            try
            {
                // SelectedValue is already two-way bound to LanguageOverrideComboValue.
                // After RefreshLanguageOptions rebuilds option instances, nudge the target once.
                languageOverrideComboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateTarget();
            }
            finally
            {
                isApplyingLanguageOverridePreview = false;
                isSyncingLanguageComboBox = false;
            }
        }

        private void LanguageOverrideComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isApplyingLanguageOverridePreview || !(DataContext is PlaylistSettings settings))
            {
                return;
            }

            isApplyingLanguageOverridePreview = true;
            try
            {
                PlaylistLocalizationOverride.ApplyFromSettings(settings);
                PlaylistLocalizationOverride.MergeInto(this);
                Playlist.StaticPluginInstance?.ApplySettingsToOpenView();
            }
            finally
            {
                isApplyingLanguageOverridePreview = false;
            }
        }

        private void EnableHowLongToBeatIntegrationCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(DataContext is PlaylistSettings settings))
            {
                return;
            }

            switch (settings.HowLongToBeatInstallState)
            {
                case HltbInstallState.InstalledDisabled:
                    e.Handled = true;
                    HowLongToBeatAddonNavigation.OpenInstalledAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    RefreshHowLongToBeatIntegrationCheckBox(settings);
                    break;
                case HltbInstallState.NotInstalled:
                    e.Handled = true;
                    HowLongToBeatAddonNavigation.OpenBrowseAddonPageFromPlaylistPrompt(Playlist.StaticPlayniteApi);
                    RefreshHowLongToBeatIntegrationCheckBox(settings);
                    break;
            }
        }

        private void RefreshHowLongToBeatIntegrationCheckBox(PlaylistSettings settings)
        {
            settings.RefreshHowLongToBeatInstallState();
            enableHowLongToBeatIntegrationCheckBox.GetBindingExpression(CheckBox.IsCheckedProperty)?.UpdateTarget();
        }
    }
}
