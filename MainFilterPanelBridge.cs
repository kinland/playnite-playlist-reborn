using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Reflection;
using System.Windows.Threading;

namespace Playlist
{
    internal interface IMainFilterPanelBridge
    {
        FilterPresetSettings GetCurrentSettings();

        void ApplySettings(FilterPresetSettings settings);

        IDisposable Subscribe(EventHandler<FilterPresetSettings> handler);
    }

    /// <summary>
    /// Reads and writes Playnite desktop main-view filter settings via reflection.
    /// </summary>
    internal sealed class PlayniteMainFilterPanelBridge : IMainFilterPanelBridge
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI playniteApi;

        public PlayniteMainFilterPanelBridge(IPlayniteAPI playniteApi)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
        }

        public FilterPresetSettings GetCurrentSettings()
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

        public void ApplySettings(FilterPresetSettings settings)
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

        public IDisposable Subscribe(EventHandler<FilterPresetSettings> handler)
        {
            if (handler == null || playniteApi.MainView == null)
            {
                return EmptyDisposable.Instance;
            }

            EventInfo filterSettingsChangedEvent = playniteApi.MainView.GetType().GetEvent("FilterSettingsChanged");
            if (filterSettingsChangedEvent == null)
            {
                return EmptyDisposable.Instance;
            }

            Delegate boundHandler = Delegate.CreateDelegate(
                filterSettingsChangedEvent.EventHandlerType,
                handler.Target,
                handler.Method);
            filterSettingsChangedEvent.AddEventHandler(playniteApi.MainView, boundHandler);
            return new EventSubscription(filterSettingsChangedEvent, playniteApi.MainView, boundHandler);
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

        private sealed class EventSubscription : IDisposable
        {
            private readonly EventInfo eventInfo;
            private readonly object target;
            private readonly Delegate handler;

            public EventSubscription(EventInfo eventInfo, object target, Delegate handler)
            {
                this.eventInfo = eventInfo;
                this.target = target;
                this.handler = handler;
            }

            public void Dispose()
            {
                eventInfo?.RemoveEventHandler(target, handler);
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();

            public void Dispose()
            {
            }
        }
    }
}
