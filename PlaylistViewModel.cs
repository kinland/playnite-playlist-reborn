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
        /// Drag reorder is enabled for rank view and Last Played view (with Last Played bucket checks in drop handler).
        /// </summary>
        public bool IsDragReorderEnabled =>
            activeViewSortColumn == null || activeViewSortColumn == "Rank" || activeViewSortColumn == "LastPlayed";
        public bool IsLastPlayedSortActive => activeViewSortColumn == "LastPlayed";

        /// <summary>
        /// True when the grid is sorted by # descending; drag indices need a custom drop handler.
        /// </summary>
        internal bool IsViewRankDescending =>
            activeViewSortColumn == "Rank" && activeViewSortDirection == ListSortDirection.Descending;

        /// <summary>
        /// True when Last Played is active with descending direction (bucket-local rank order is visually reversed).
        /// </summary>
        internal bool IsViewLastPlayedDescending =>
            activeViewSortColumn == "LastPlayed" && activeViewSortDirection == ListSortDirection.Descending;

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
        private SearchQueryMatcher searchMatcher = SearchQueryMatcher.Create(string.Empty);
        private List<SearchQueryMatcher> tagMatchers = new List<SearchQueryMatcher>();
        private List<SearchQueryMatcher> genreMatchers = new List<SearchQueryMatcher>();
        private List<SearchQueryMatcher> developerMatchers = new List<SearchQueryMatcher>();
        private List<SearchQueryMatcher> publisherMatchers = new List<SearchQueryMatcher>();
        private List<SearchQueryMatcher> categoryMatchers = new List<SearchQueryMatcher>();
        private List<SearchQueryMatcher> featureMatchers = new List<SearchQueryMatcher>();
        private string searchQuery = string.Empty;

        /// <summary>
        /// Bumped on every playlist mutation so rank bindings (which depend on index in the list) refresh.
        /// </summary>
        public int PlaylistGamesRevision { get; private set; }

        public string SearchQuery
        {
            get => searchQuery;
            set
            {
                string nextValue = value ?? string.Empty;
                if (string.Equals(searchQuery, nextValue, StringComparison.Ordinal))
                {
                    return;
                }

                searchQuery = nextValue;
                SearchQuerySpec querySpec = SearchQuerySpec.Parse(searchQuery);
                searchMatcher = SearchQueryMatcher.Create(querySpec.NameQuery);
                tagMatchers = querySpec.TagQueries.Select(SearchQueryMatcher.Create).ToList();
                genreMatchers = querySpec.GenreQueries.Select(SearchQueryMatcher.Create).ToList();
                developerMatchers = querySpec.DeveloperQueries.Select(SearchQueryMatcher.Create).ToList();
                publisherMatchers = querySpec.PublisherQueries.Select(SearchQueryMatcher.Create).ToList();
                categoryMatchers = querySpec.CategoryQueries.Select(SearchQueryMatcher.Create).ToList();
                featureMatchers = querySpec.FeatureQueries.Select(SearchQueryMatcher.Create).ToList();
                PlaylistGamesView.Refresh();
                OnPropertyChanged(nameof(SearchQuery));
            }
        }

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
            PlaylistGamesView.Filter = FilterPlaylistGameBySearch;
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
        public string ActiveViewSortColumn => activeViewSortColumn;
        public ListSortDirection ActiveViewSortDirection => activeViewSortDirection;

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
                case "LastPlayed":
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
                case "LastPlayed":
                    listView.CustomSort = new LastPlayedGameComparer(
                        PlaylistGames,
                        descending: activeViewSortDirection == ListSortDirection.Descending);
                    break;
            }

            listView.Refresh();
            OnPropertyChanged(nameof(ActiveViewSortColumn));
            OnPropertyChanged(nameof(ActiveViewSortDirection));
            OnPropertyChanged(nameof(IsDragReorderEnabled));
            OnPropertyChanged(nameof(IsLastPlayedSortActive));
        }

        internal void RestoreViewSort(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrWhiteSpace(columnKey))
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
                case "LastPlayed":
                    break;
                default:
                    return;
            }

            activeViewSortColumn = columnKey;
            activeViewSortDirection = direction;

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
                case "LastPlayed":
                    listView.CustomSort = new LastPlayedGameComparer(
                        PlaylistGames,
                        descending: activeViewSortDirection == ListSortDirection.Descending);
                    break;
            }

            listView.Refresh();
            OnPropertyChanged(nameof(ActiveViewSortColumn));
            OnPropertyChanged(nameof(ActiveViewSortDirection));
            OnPropertyChanged(nameof(IsDragReorderEnabled));
            OnPropertyChanged(nameof(IsLastPlayedSortActive));
        }

        private void OnPlaylistGamesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PlaylistGamesRevision++;
            OnPropertyChanged(nameof(PlaylistGamesRevision));
        }

        /// <summary>
        /// Applies current search query/spec matcher state to a row item.
        /// </summary>
        private bool FilterPlaylistGameBySearch(object item)
        {
            if (item is Game game)
            {
                if (!searchMatcher.IsMatch(game.Name))
                {
                    return false;
                }

                if (tagMatchers.Count > 0 && !MatchesAllScopedTerms(tagMatchers, GetSearchableTagNames(game)))
                {
                    return false;
                }

                if (genreMatchers.Count > 0 && !MatchesAllScopedTerms(genreMatchers, GetSearchableGenreNames(game)))
                {
                    return false;
                }

                if (developerMatchers.Count > 0 && !MatchesAllScopedTerms(developerMatchers, GetSearchableDeveloperNames(game)))
                {
                    return false;
                }

                if (publisherMatchers.Count > 0 && !MatchesAllScopedTerms(publisherMatchers, GetSearchablePublisherNames(game)))
                {
                    return false;
                }

                if (categoryMatchers.Count > 0 && !MatchesAllScopedTerms(categoryMatchers, GetSearchableCategoryNames(game)))
                {
                    return false;
                }

                if (featureMatchers.Count > 0 && !MatchesAllScopedTerms(featureMatchers, GetSearchableFeatureNames(game)))
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Requires each scoped matcher to match at least one candidate value.
        /// </summary>
        private static bool MatchesAllScopedTerms(IEnumerable<SearchQueryMatcher> matchers, IEnumerable<string> candidates)
        {
            string[] scopedCandidates = candidates.Where((c) => !string.IsNullOrWhiteSpace(c)).ToArray();
            foreach (SearchQueryMatcher matcher in matchers)
            {
                if (!scopedCandidates.Any((candidate) => matcher.IsMatch(candidate)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Resolves game tag names from TagIds for scoped tag filtering.
        /// </summary>
        private IEnumerable<string> GetSearchableTagNames(Game game)
        {
            if (game?.TagIds == null || game.TagIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.TagIds)
            {
                Tag tag = playniteApi.Database.Tags.Get(id);
                if (!string.IsNullOrWhiteSpace(tag?.Name))
                {
                    names.Add(tag.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Resolves game genre names from GenreIds for scoped genre filtering.
        /// </summary>
        private IEnumerable<string> GetSearchableGenreNames(Game game)
        {
            if (game?.GenreIds == null || game.GenreIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.GenreIds)
            {
                Genre genre = playniteApi.Database.Genres.Get(id);
                if (!string.IsNullOrWhiteSpace(genre?.Name))
                {
                    names.Add(genre.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Resolves developer company names from DeveloperIds.
        /// </summary>
        private IEnumerable<string> GetSearchableDeveloperNames(Game game)
        {
            if (game?.DeveloperIds == null || game.DeveloperIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.DeveloperIds)
            {
                var company = playniteApi.Database.Companies.Get(id);
                if (!string.IsNullOrWhiteSpace(company?.Name))
                {
                    names.Add(company.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Resolves publisher company names from PublisherIds.
        /// </summary>
        private IEnumerable<string> GetSearchablePublisherNames(Game game)
        {
            if (game?.PublisherIds == null || game.PublisherIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.PublisherIds)
            {
                var company = playniteApi.Database.Companies.Get(id);
                if (!string.IsNullOrWhiteSpace(company?.Name))
                {
                    names.Add(company.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Resolves category names from CategoryIds.
        /// </summary>
        private IEnumerable<string> GetSearchableCategoryNames(Game game)
        {
            if (game?.CategoryIds == null || game.CategoryIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.CategoryIds)
            {
                var category = playniteApi.Database.Categories.Get(id);
                if (!string.IsNullOrWhiteSpace(category?.Name))
                {
                    names.Add(category.Name);
                }
            }

            return names;
        }

        /// <summary>
        /// Resolves feature names from FeatureIds.
        /// </summary>
        private IEnumerable<string> GetSearchableFeatureNames(Game game)
        {
            if (game?.FeatureIds == null || game.FeatureIds.Count == 0)
            {
                return Enumerable.Empty<string>();
            }

            List<string> names = new List<string>();
            foreach (Guid id in game.FeatureIds)
            {
                var feature = playniteApi.Database.Features.Get(id);
                if (!string.IsNullOrWhiteSpace(feature?.Name))
                {
                    names.Add(feature.Name);
                }
            }

            return names;
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

        private sealed class LastPlayedGameComparer : IComparer
        {
            private readonly IList<Game> playlistOrder;
            private readonly LastPlayedSortBucketComparer sortComparer;
            private readonly DateTime nowUtc;

            public LastPlayedGameComparer(IList<Game> playlistOrder, bool descending)
            {
                this.playlistOrder = playlistOrder;
                sortComparer = new LastPlayedSortBucketComparer(descending);
                nowUtc = DateTime.UtcNow;
            }

            /// <summary>
            /// Sorts games by display bucket first, then rank (and exact recency in Moments bucket).
            /// </summary>
            public int Compare(object x, object y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                LastPlayedSortKey keyX = BuildSortKey(x as Game);
                LastPlayedSortKey keyY = BuildSortKey(y as Game);
                return sortComparer.Compare(keyX, keyY);
            }

            /// <summary>
            /// Builds a comparable key from a game's LastActivity and persisted rank index.
            /// </summary>
            private LastPlayedSortKey BuildSortKey(Game game)
            {
                if (game == null)
                {
                    return new LastPlayedSortKey(int.MaxValue, 0, int.MaxValue);
                }

                int rankIndex = playlistOrder.IndexOf(game);
                if (rankIndex < 0)
                {
                    rankIndex = int.MaxValue;
                }

                DateTime? lastPlayedUtc = ExtractLastPlayedUtc(game);
                LastPlayedDisplayValue formatted = LastPlayedRelativeFormatter.Format(lastPlayedUtc, nowUtc);
                long ticksUtc = lastPlayedUtc?.Ticks ?? 0;
                return new LastPlayedSortKey(formatted.SortBucket, ticksUtc, rankIndex);
            }

            /// <summary>
            /// Normalizes LastActivity to UTC nullable value for formatting and sorting.
            /// </summary>
            private static DateTime? ExtractLastPlayedUtc(Game game)
            {
                DateTime? dt = game.LastActivity;
                if (!dt.HasValue || dt.Value == default)
                {
                    return null;
                }

                DateTime value = dt.Value;
                if (value.Kind == DateTimeKind.Utc)
                {
                    return value;
                }

                if (value.Kind == DateTimeKind.Local)
                {
                    return value.ToUniversalTime();
                }

                return DateTime.SpecifyKind(value, DateTimeKind.Utc);
            }
        }
    }
}
