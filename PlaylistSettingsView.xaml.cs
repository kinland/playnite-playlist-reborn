using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Playlist
{
    public partial class PlaylistSettingsView : UserControl
    {
        private bool isApplyingLanguageOverridePreview;

        public PlaylistSettingsView()
        {
            InitializeComponent();
            Loaded += PlaylistSettingsView_Loaded;
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

            languageOverrideComboBox.GetBindingExpression(ComboBox.SelectedValueProperty)?.UpdateTarget();
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
