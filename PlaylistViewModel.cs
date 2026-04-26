using GongSolutions.Wpf.DragDrop;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Playlist
{
    public class PlaylistViewModel : ObservableObject
    {
        private readonly IPlayniteAPI playniteApi;

        public ObservableCollection<Game> PlaylistGames { get; set; }

        /// <summary>
        /// View over <see cref="PlaylistGames"/>; sorting this does not change persisted playlist order.
        /// </summary>
        public ICollectionView PlaylistGamesView { get; }

        /// <summary>
        /// Gong drag-drop reorder is only safe when the view follows playlist rank order (unsorted or # column).
        /// </summary>
        public bool IsDragReorderEnabled => activeViewSortColumn == null || activeViewSortColumn == "Rank";

        /// <summary>
        /// True when the grid is sorted by # descending; drag indices need a custom drop handler.
        /// </summary>
        internal bool IsViewRankDescending =>
            activeViewSortColumn == "Rank" && activeViewSortDirection == ListSortDirection.Descending;

        /// <summary>
        /// Custom Gong drop target so rank-descending reorder matches on-screen order.
        /// </summary>
        public IDropTarget PlaylistDropHandler { get; }

        /// <summary>
        /// Gong drag source wrapper; clears HowLongToBeat game context during reorder drags for smoother interaction.
        /// </summary>
        public IDragSource PlaylistDragHandler { get; }

        /// <summary>
        /// True while the user is dragging to reorder the playlist (rank sort only).
        /// </summary>
        public bool IsPlaylistDragReorderActive => isPlaylistDragReorderActive;

        private bool isPlaylistDragReorderActive;

        /// <summary>
        /// Bumped on every playlist mutation so rank bindings (which depend on index in the list) refresh.
        /// </summary>
        public int PlaylistGamesRevision { get; private set; }

        public RelayCommand<object> NavigateBackCommand { get; }

        public RelayCommand<Game> StartGameCommand { get; }

        public RelayCommand<ObservableCollection<object>> RemoveGamesCommand { get; }

        public RelayCommand<ObservableCollection<object>> MoveGamesToTopCommand { get; }

        public RelayCommand<ObservableCollection<object>> MoveGamesToBottomCommand { get; }

        public RelayCommand<Game> ShowGameInLibraryCommand { get; }

        public bool MoveGameToRank(Game game, int rank)
        {
            if (game == null || PlaylistGames.Count == 0)
            {
                return false;
            }

            int currentIndex = PlaylistGames.IndexOf(game);
            if (currentIndex < 0)
            {
                return false;
            }

            if (rank < 1 || rank > PlaylistGames.Count)
            {
                return false;
            }

            int targetIndex = rank - 1;
            if (targetIndex == currentIndex)
            {
                return false;
            }

            PlaylistGames.RemoveAt(currentIndex);
            if (targetIndex > currentIndex)
            {
                targetIndex--;
            }

            PlaylistGames.Insert(targetIndex, game);
            return true;
        }

        public IEnumerable<KeyValuePair<CompletionStatus, RelayCommand<IEnumerable<object>>>> CompletionStatusCommands
        {
            get
            {
                foreach (CompletionStatus completionStatus in playniteApi.Database.CompletionStatuses.OrderBy(a => a.Name))
                {
                    yield return new KeyValuePair<CompletionStatus, RelayCommand<IEnumerable<object>>>(
                        completionStatus,
                        new RelayCommand<IEnumerable<object>>((games) =>
                        {
                            foreach (Game game in games.Cast<Game>())
                            {
                                game.CompletionStatusId = completionStatus.Id;
                                playniteApi.Database.Games.Update(game);
                            }
                        })
                    );
                }
            }
        }

        public PlaylistViewModel(ObservableCollection<Game> playlistGames, IPlayniteAPI playniteApi)
        {
            PlaylistGames = playlistGames ?? throw new ArgumentNullException(nameof(playlistGames));
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));

            PlaylistGamesView = CollectionViewSource.GetDefaultView(PlaylistGames);
            PlaylistDropHandler = new PlaylistListDropHandler(this);
            PlaylistDragHandler = new PlaylistDragSourceHandler(this);
            PlaylistGames.CollectionChanged += OnPlaylistGamesCollectionChanged;

            NavigateBackCommand = new RelayCommand<object>((a) =>
            {
                playniteApi.MainView.SwitchToLibraryView();
            });

            StartGameCommand = new RelayCommand<Game>(
                (game) =>
                {
                    if (game == null)
                    {
                        return;
                    }
                    playniteApi.StartGame(game.Id);
                },
                new KeyGesture(Key.Enter)
            );

            RemoveGamesCommand = new RelayCommand<ObservableCollection<object>>(
                (games) =>
                {
                    if (playniteApi.Dialogs.ShowMessage(
                        string.Format(playniteApi.Resources.GetString("LOCGamesRemoveAskMessage"), games.Count()),
                        "LOCGameRemoveAskTitle",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    foreach (Game game in games.Cast<Game>().ToList())
                    {
                        PlaylistGames.Remove(game);
                    }
                },
                new KeyGesture(Key.Delete)
            );


            MoveGamesToTopCommand = new RelayCommand<ObservableCollection<object>>((games) =>
            {
                foreach (Game game in games.Cast<Game>().OrderBy((g) => PlaylistGames.IndexOf(g)).Reverse().ToList())
                {
                    PlaylistGames.Remove(game);
                    PlaylistGames.Insert(0, game);
                }
            });

            MoveGamesToBottomCommand = new RelayCommand<ObservableCollection<object>>((games) =>
            {
                foreach (Game game in games.Cast<Game>().OrderBy((g) => PlaylistGames.IndexOf(g)).ToList())
                {
                    PlaylistGames.Remove(game);
                    PlaylistGames.Add(game);
                }
            });

            ShowGameInLibraryCommand = new RelayCommand<Game>((game) =>
            {
                if (game == null)
                {
                    return;
                }
                // This does select the game, but does not currently scroll it into view
                playniteApi.MainView.SelectGame(game.Id);
                playniteApi.MainView.SwitchToLibraryView();
            });
        }

        private string activeViewSortColumn;
        private ListSortDirection activeViewSortDirection = ListSortDirection.Ascending;

        /// <summary>
        /// Toggles ascending/descending when the same column is clicked again. Does not mutate <see cref="PlaylistGames"/>.
        /// </summary>
        public void ToggleViewSort(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey))
            {
                return;
            }

            ListCollectionView listView = PlaylistGamesView as ListCollectionView;
            if (listView == null)
            {
                return;
            }

            switch (columnKey)
            {
                case "Rank":
                case "Name":
                case "Playtime":
                case "CompletionStatus":
                    break;
                default:
                    return;
            }

            if (columnKey == activeViewSortColumn)
            {
                activeViewSortDirection = activeViewSortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                activeViewSortColumn = columnKey;
                activeViewSortDirection = ListSortDirection.Ascending;
            }

            listView.SortDescriptions.Clear();
            listView.CustomSort = null;

            switch (columnKey)
            {
                case "Rank":
                    listView.CustomSort = new PlaylistRankIndexComparer(PlaylistGames, activeViewSortDirection);
                    break;
                case "Name":
                    listView.SortDescriptions.Add(new SortDescription(nameof(Game.Name), activeViewSortDirection));
                    break;
                case "Playtime":
                    listView.SortDescriptions.Add(new SortDescription(nameof(Game.Playtime), activeViewSortDirection));
                    break;
                case "CompletionStatus":
                    listView.SortDescriptions.Add(new SortDescription(nameof(Game.CompletionStatus), activeViewSortDirection));
                    break;
            }

            listView.Refresh();
            OnPropertyChanged(nameof(IsDragReorderEnabled));
        }

        private void OnPlaylistGamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PlaylistGamesRevision++;
            OnPropertyChanged(nameof(PlaylistGamesRevision));
        }

        internal void SetPlaylistDragReorderActive(bool active)
        {
            if (isPlaylistDragReorderActive == active)
            {
                return;
            }

            isPlaylistDragReorderActive = active;
            OnPropertyChanged(nameof(IsPlaylistDragReorderActive));
        }

        private sealed class PlaylistRankIndexComparer : IComparer
        {
            private readonly IList<Game> playlistOrder;
            private readonly int directionSign;

            public PlaylistRankIndexComparer(IList<Game> playlistOrder, ListSortDirection direction)
            {
                this.playlistOrder = playlistOrder;
                directionSign = direction == ListSortDirection.Descending ? -1 : 1;
            }

            public int Compare(object x, object y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                int ix = x is Game gx ? playlistOrder.IndexOf(gx) : -1;
                int iy = y is Game gy ? playlistOrder.IndexOf(gy) : -1;
                if (ix < 0 && iy < 0)
                {
                    return 0;
                }

                if (ix < 0)
                {
                    return 1;
                }

                if (iy < 0)
                {
                    return -1;
                }

                return directionSign * ix.CompareTo(iy);
            }
        }
    }
}
