using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Threading;

namespace Playlist
{
    /// <summary>
    /// Keeps the Playlist search box in sync with Playnite's filter-panel search fields.
    /// </summary>
    internal sealed class MainSearchSync
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI playniteApi;
        private readonly Func<bool> isSyncEnabled;
        private readonly MainSearchFilterNameResolver filterNameResolver;
        private PlaylistViewModel viewModel;
        private bool suppressSync;
        private bool isSubscribedToMain;
        private EventInfo filterSettingsChangedEvent;
        private Delegate filterSettingsChangedHandler;
        private string preservedPlaylistQuery;
        private FilterPresetSettings mainSnapshotAfterPush;

        public MainSearchSync(IPlayniteAPI playniteApi, Func<bool> isSyncEnabled)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.isSyncEnabled = isSyncEnabled ?? throw new ArgumentNullException(nameof(isSyncEnabled));
            filterNameResolver = new MainSearchFilterNameResolver(playniteApi);
        }

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

        public void Attach(PlaylistViewModel playlistViewModel)
        {
            if (viewModel != null)
            {
                viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            viewModel = playlistViewModel ?? throw new ArgumentNullException(nameof(playlistViewModel));
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        public void OnViewOpened()
        {
            if (!IsEnabled)
            {
                return;
            }

            PullFromMain();
            SubscribeToMainFilterChanges();
        }

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
            if (viewModel == null)
            {
                return;
            }

            FilterPresetSettings currentMain = GetCurrentMainSettings();
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
            FilterPresetSettings currentMain = GetCurrentMainSettings();
            FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
                currentMain,
                preservedPlaylistQuery,
                filterNameResolver);
            mainSnapshotAfterPush = MainSearchFilterMapper.BuildSyncSnapshot(
                currentMain,
                preservedPlaylistQuery,
                filterNameResolver);
            ApplyMainFilterSettings(mapped);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!IsEnabled || suppressSync || viewModel == null || e.PropertyName != nameof(PlaylistViewModel.SearchQuery))
            {
                return;
            }

            suppressSync = true;
            try
            {
                preservedPlaylistQuery = viewModel.SearchQuery ?? string.Empty;
                FilterPresetSettings currentMain = GetCurrentMainSettings();
                FilterPresetSettings mapped = MainSearchFilterMapper.ApplySyncPush(
                    currentMain,
                    preservedPlaylistQuery,
                    filterNameResolver);
                mainSnapshotAfterPush = MainSearchFilterMapper.BuildSyncSnapshot(
                    currentMain,
                    preservedPlaylistQuery,
                    filterNameResolver);
                ApplyMainFilterSettings(mapped);
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

        private FilterPresetSettings GetCurrentMainSettings()
        {
            try
            {
                return playniteApi.MainView?.GetCurrentFilterSettings() ?? new FilterPresetSettings();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read Playnite main filter settings.");
                return new FilterPresetSettings();
            }
        }

        private void ApplyMainFilterSettings(FilterPresetSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            void Apply()
            {
                try
                {
                    object filterSettings = GetFilterSettingsObject();
                    if (filterSettings == null)
                    {
                        return;
                    }

                    MethodInfo applyMethod = filterSettings.GetType().GetMethod(
                        "ApplyFilter",
                        new[] { typeof(FilterPresetSettings) });
                    applyMethod?.Invoke(filterSettings, new object[] { settings });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to write Playnite main filter settings.");
                }
            }

            Dispatcher dispatcher = playniteApi.MainView?.UIDispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                dispatcher.Invoke(Apply);
            }
        }

        private object GetFilterSettingsObject()
        {
            object mainModel = GetDesktopMainModel();
            if (mainModel == null)
            {
                return null;
            }

            object appSettings = mainModel.GetType().GetProperty("AppSettings")?.GetValue(mainModel);
            return appSettings?.GetType().GetProperty("FilterSettings")?.GetValue(appSettings);
        }

        private object GetDesktopMainModel()
        {
            object mainView = playniteApi.MainView;
            if (mainView == null)
            {
                return null;
            }

            return mainView.GetType().GetField("mainModel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(mainView);
        }

        private void SubscribeToMainFilterChanges()
        {
            if (isSubscribedToMain || playniteApi.MainView == null)
            {
                return;
            }

            filterSettingsChangedEvent = playniteApi.MainView.GetType().GetEvent("FilterSettingsChanged");
            if (filterSettingsChangedEvent == null)
            {
                return;
            }

            MethodInfo handlerMethod = typeof(MainSearchSync).GetMethod(
                nameof(HandleFilterSettingsChanged),
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (handlerMethod == null)
            {
                return;
            }

            filterSettingsChangedHandler = Delegate.CreateDelegate(
                filterSettingsChangedEvent.EventHandlerType,
                this,
                handlerMethod);
            filterSettingsChangedEvent.AddEventHandler(playniteApi.MainView, filterSettingsChangedHandler);
            isSubscribedToMain = true;
        }

        private void UnsubscribeFromMainFilterChanges()
        {
            if (!isSubscribedToMain || filterSettingsChangedEvent == null || filterSettingsChangedHandler == null)
            {
                return;
            }

            filterSettingsChangedEvent.RemoveEventHandler(playniteApi.MainView, filterSettingsChangedHandler);
            filterSettingsChangedEvent = null;
            filterSettingsChangedHandler = null;
            isSubscribedToMain = false;
        }
    }
}
