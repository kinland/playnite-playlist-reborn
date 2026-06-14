using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Playlist
{
    public partial class PlaylistSettingsView : UserControl
    {
        public PlaylistSettingsView()
        {
            InitializeComponent();
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
