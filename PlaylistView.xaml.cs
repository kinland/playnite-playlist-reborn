using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Playlist
{
    /// <summary>
    /// Interaction logic for PlaylistView.xaml
    /// </summary>
    public partial class PlaylistView : UserControl
    {
        public PlaylistView()
        {
            InitializeComponent();
        }
        public PlaylistView(PlaylistViewModel model)
        {
            DataContext = model;
            InitializeComponent();
        }

        private const string RankCellTag = "PlaylistRankCell";

        private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsUnderRankCell(e.OriginalSource as DependencyObject))
            {
                return;
            }

            Control item = sender as Control;
            (DataContext as PlaylistViewModel)?.StartGameCommand.Execute(item?.DataContext);
        }

        private void OnRankCellMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (!(sender is Grid container) || container.Children.Count < 2)
            {
                return;
            }

            if (!(container.Children[0] is TextBlock textBlock) || !(container.Children[1] is TextBox rankEditor))
            {
                return;
            }

            // Already editing — still mark handled so the row does not start the game.
            if (rankEditor.Visibility == Visibility.Visible)
            {
                e.Handled = true;
                return;
            }

            rankEditor.Text = textBlock.Text;
            textBlock.Visibility = Visibility.Collapsed;
            rankEditor.Visibility = Visibility.Visible;
            rankEditor.Focus();
            rankEditor.SelectAll();
            e.Handled = true;
        }

        private void OnRankTextBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Row listens for double-click to launch; swallow while editing rank.
            e.Handled = true;
        }

        private static bool IsUnderRankCell(DependencyObject source)
        {
            for (DependencyObject d = source; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if (d is FrameworkElement fe && fe.Tag is string s && s == RankCellTag)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnRankTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!(sender is TextBox rankEditor))
            {
                return;
            }

            // Main keyboard Enter is Key.Return in WPF; numpad is often Key.Enter.
            // If we don't handle Return, the key stays unhandled and the row's IsDefault play button launches the game.
            if (IsCommitRankKey(e.Key))
            {
                CommitRankEdit(rankEditor);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelRankEdit(rankEditor);
                e.Handled = true;
            }
        }

        private static bool IsCommitRankKey(Key key)
        {
            return key == Key.Enter || key == Key.Return;
        }

        private void OnRankTextBoxLostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is TextBox rankEditor)
            {
                CancelRankEdit(rankEditor);
            }
        }

        private void CommitRankEdit(TextBox rankEditor)
        {
            if (!(rankEditor.DataContext is Playnite.SDK.Models.Game game) || !(DataContext is PlaylistViewModel model))
            {
                CancelRankEdit(rankEditor);
                return;
            }

            if (int.TryParse(rankEditor.Text, out int rank))
            {
                model.MoveGameToRank(game, rank);
                playlistListView.SelectedItem = game;
                playlistListView.ScrollIntoView(game);
            }

            CloseRankEditor(rankEditor);
        }

        private void CancelRankEdit(TextBox rankEditor)
        {
            CloseRankEditor(rankEditor);
        }

        private static void CloseRankEditor(TextBox rankEditor)
        {
            rankEditor.Visibility = System.Windows.Visibility.Collapsed;
            if (rankEditor.Parent is Grid container && container.Children.Count > 0 && container.Children[0] is TextBlock textBlock)
            {
                textBlock.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
