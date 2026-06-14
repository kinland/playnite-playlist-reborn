using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.ComponentModel;

namespace Playlist
{
    /// <summary>
    /// Keeps the Playlist search box in sync with Playnite's filter-panel search fields.
    /// </summary>
    internal sealed class MainSearchSync
    {
        private readonly Func<bool> isSyncEnabled;
        private readonly MainSearchFilterNameResolver filterNameResolver;
        private readonly IMainFilterPanelBridge mainFilterBridge;
        private IPlaylistSearchSyncTarget viewModel;
        private bool suppressSync;
        private IDisposable mainFilterSubscription;
        private string preservedPlaylistQuery;
        private FilterPresetSettings mainSnapshotAfterPush;

        public MainSearchSync(IPlayniteAPI playniteApi, Func<bool> isSyncEnabled)
            : this(playniteApi, isSyncEnabled, new PlayniteMainFilterPanelBridge(playniteApi))
        {
        }

        internal MainSearchSync(
            IPlayniteAPI playniteApi,
            Func<bool> isSyncEnabled,
            IMainFilterPanelBridge mainFilterBridge)
            : this(isSyncEnabled, mainFilterBridge, new MainSearchFilterNameResolver(playniteApi))
        {
        }

        internal MainSearchSync(
            Func<bool> isSyncEnabled,
            IMainFilterPanelBridge mainFilterBridge,
            MainSearchFilterNameResolver filterNameResolver)
        {
            this.isSyncEnabled = isSyncEnabled ?? throw new ArgumentNullException(nameof(isSyncEnabled));
            this.mainFilterBridge = mainFilterBridge ?? throw new ArgumentNullException(nameof(mainFilterBridge));
            this.filterNameResolver = filterNameResolver ?? throw new ArgumentNullException(nameof(filterNameResolver));
        }

        /// <summary>Subscribes or unsubscribes from main-panel changes when the sync setting toggles.</summary>
        public void ApplySettingsChange(bool playlistViewActive)
        {
            if (!IsEnabled)
            {
                UnsubscribeFromMainFilterChanges();
                return;
            }

            if (playlistViewActive && viewModel != null)
            {
                OnViewOpened();
            }
        }

        /// <summary>Wires playlist search edits to this sync instance.</summary>
        public void Attach(IPlaylistSearchSyncTarget playlistViewModel)
        {
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            viewModel = playlistViewModel ?? throw new ArgumentNullException(nameof(playlistViewModel));
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        /// <summary>Pulls the main filter panel into the playlist search box when the view opens.</summary>
        public void OnViewOpened()
        {
            if (!IsEnabled)
            {
                return;
            }

            PullFromMain();
            SubscribeToMainFilterChanges();
        }

        /// <summary>Pushes the playlist search box back to the main filter panel when the view closes.</summary>
        public void OnViewClosed()
        {
            if (!IsEnabled)
            {
                return;
            }

            PushToMain();
            UnsubscribeFromMainFilterChanges();
        }

        private bool IsEnabled => isSyncEnabled();

        private void PullFromMain()
        {
            // preservedPlaylistQuery holds playlist-only syntax across a main-panel edit session;
            // mainSnapshotAfterPush detects when the user reverts main fields to the last pushed state.
            if (viewModel == null)
            {
                return;
            }

            FilterPresetSettings currentMain = mainFilterBridge.GetCurrentSettings();
            string nextQuery = MainSearchFilterMapper.ResolveReturnQuery(
                preservedPlaylistQuery,
                mainSnapshotAfterPush,
                currentMain,
                filterNameResolver);

            if (string.Equals(viewModel.SearchQuery, nextQuery, StringComparison.Ordinal))
            {
                return;
            }

            suppressSync = true;
            try
            {
                viewModel.SearchQuery = nextQuery;
            }
            finally
            {
                suppressSync = false;
            }
        }

        private void PushToMain()
        {
            if (viewModel == null)
            {
                return;
            }

            preservedPlaylistQuery = viewModel.SearchQuery ?? string.Empty;
            FilterPresetSettings currentMain = mainFilterBridge.GetCurrentSettings();
            FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
                currentMain,
                preservedPlaylistQuery,
                filterNameResolver);
            mainSnapshotAfterPush = MainSearchFilterMapper.BuildSyncSnapshot(
                currentMain,
                preservedPlaylistQuery,
                filterNameResolver);
            mainFilterBridge.ApplySettings(mapped);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!IsEnabled || suppressSync || viewModel == null || e.PropertyName != nameof(IPlaylistSearchSyncTarget.SearchQuery))
            {
                return;
            }

            suppressSync = true;
            try
            {
                preservedPlaylistQuery = viewModel.SearchQuery ?? string.Empty;
                FilterPresetSettings currentMain = mainFilterBridge.GetCurrentSettings();
                FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
                    currentMain,
                    preservedPlaylistQuery,
                    filterNameResolver);
                mainSnapshotAfterPush = MainSearchFilterMapper.BuildSyncSnapshot(
                    currentMain,
                    preservedPlaylistQuery,
                    filterNameResolver);
                mainFilterBridge.ApplySettings(mapped);
            }
            finally
            {
                suppressSync = false;
            }
        }

        private void HandleFilterSettingsChanged(object sender, FilterPresetSettings settings)
        {
            if (!IsEnabled || suppressSync || viewModel == null)
            {
                return;
            }

            string nextQuery = MainSearchFilterMapper.ToPlaylistQuery(settings, filterNameResolver);
            if (string.Equals(viewModel.SearchQuery, nextQuery, StringComparison.Ordinal))
            {
                return;
            }

            suppressSync = true;
            try
            {
                viewModel.SearchQuery = nextQuery;
                preservedPlaylistQuery = null;
                mainSnapshotAfterPush = null;
            }
            finally
            {
                suppressSync = false;
            }
        }

        private void SubscribeToMainFilterChanges()
        {
            if (mainFilterSubscription != null)
            {
                return;
            }

            mainFilterSubscription = mainFilterBridge.Subscribe(HandleFilterSettingsChanged);
        }

        private void UnsubscribeFromMainFilterChanges()
        {
            if (mainFilterSubscription == null)
            {
                return;
            }

            mainFilterSubscription.Dispose();
            mainFilterSubscription = null;
        }
    }
}
