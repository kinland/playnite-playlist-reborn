using Playnite.SDK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Playlist
{
    public enum HltbInstallState
    {
        NotInstalled,
        InstalledDisabled,
        InstalledEnabled,
    }

    /// <summary>
    /// Detects HowLongToBeat add-on install state and opens Playnite's Add-ons dialog on the
    /// Installed &gt; Generic or Browse &gt; Generic page for HowLongToBeat.
    /// Uses Playnite desktop internals via reflection because the SDK exposes no browse API.
    /// </summary>
    internal static class HowLongToBeatAddonNavigation
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        internal const string HltbExtensionId = "playnite-howlongtobeat-plugin";

        private const int InstalledGenericTreeTag = 2;
        private const int BrowseGenericTreeTag = 7;
        private const int MaxNavigationAttempts = 40;

        private const string PackedExtensionFileExtension = ".pext";
        private const string ExtensionManifestFileName = "extension.yaml";
        private const int ExtInstallTypeInstall = 0;

        private static readonly JsonSerializerOptions ExtensionQueueJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Unit tests only; when set, bypasses Playnite add-on detection.</summary>
        internal static Func<IPlayniteAPI, HltbInstallState> TestInstallStateResolver { get; set; }

        /// <summary>Unit tests only; when set, bypasses Playnite extension install queue detection.</summary>
        internal static Func<bool> TestExtensionInstallQueuePendingResolver { get; set; }

        /// <summary>Unit tests only; overrides the Playnite extension queue JSON file path.</summary>
        internal static string TestExtensionQueueFilePathOverride { get; set; }

        /// <summary>
        /// True when Playnite has a queued HowLongToBeat extension install waiting for restart
        /// (e.g. user installed HLTB from Add-ons but chose restart later).
        /// </summary>
        internal static bool IsExtensionInstallQueuedForRestart()
        {
            if (TestExtensionInstallQueuePendingResolver != null)
            {
                return TestExtensionInstallQueuePendingResolver();
            }

            bool sawQueuedExtensionPackage = false;
            foreach (string packagePath in TryGetQueuedExtensionInstallPaths())
            {
                sawQueuedExtensionPackage = true;
                if (TryGetPackedExtensionId(packagePath, out string extensionId)
                    && string.Equals(extensionId, HltbExtensionId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (sawQueuedExtensionPackage)
            {
                logger.Warn("Playnite has queued extension installs, but none match HowLongToBeat.");
            }

            return false;
        }

        private static IEnumerable<string> TryGetQueuedExtensionInstallPaths()
        {
            List<string> paths = TryGetQueuedExtensionInstallPathsViaReflection();
            if (paths.Count > 0)
            {
                return paths;
            }

            return TryGetQueuedExtensionInstallPathsViaQueueFile();
        }

        private static List<string> TryGetQueuedExtensionInstallPathsViaReflection()
        {
            var paths = new List<string>();
            try
            {
                Type installerType = Type.GetType("Playnite.Plugins.ExtensionInstaller, Playnite");
                MethodInfo getQueuedItems = installerType?.GetMethod(
                    "GetQueuedItems",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getQueuedItems?.Invoke(null, null) is IList queueItems)
                {
                    Type queueItemType = Type.GetType("Playnite.Plugins.ExtensionInstallQueueItem, Playnite");
                    Type installTypeEnum = Type.GetType("Playnite.Plugins.ExtInstallType, Playnite");
                    object installValue = installTypeEnum != null
                        ? Enum.Parse(installTypeEnum, "Install")
                        : null;
                    PropertyInfo installTypeProperty = queueItemType?.GetProperty("InstallType");
                    PropertyInfo pathProperty = queueItemType?.GetProperty("Path");

                    foreach (object item in queueItems)
                    {
                        if (item == null || installTypeProperty == null || installValue == null || pathProperty == null)
                        {
                            continue;
                        }

                        if (!Equals(installTypeProperty.GetValue(item), installValue))
                        {
                            continue;
                        }

                        string path = pathProperty.GetValue(item) as string;
                        if (IsQueuedExtensionPackagePath(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read Playnite extension install queue via reflection.");
            }

            return paths;
        }

        private static IEnumerable<string> TryGetQueuedExtensionInstallPathsViaQueueFile()
        {
            string queueFilePath = TryGetExtensionQueueFilePath();
            if (string.IsNullOrEmpty(queueFilePath) || !File.Exists(queueFilePath))
            {
                yield break;
            }

            List<ExtensionInstallQueueItemDto> queueItems;
            try
            {
                string json = File.ReadAllText(queueFilePath);
                queueItems = JsonSerializer.Deserialize<List<ExtensionInstallQueueItemDto>>(json, ExtensionQueueJsonOptions);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to read Playnite extension install queue file.");
                yield break;
            }

            if (queueItems == null)
            {
                yield break;
            }

            foreach (ExtensionInstallQueueItemDto item in queueItems)
            {
                if (item?.InstallType != ExtInstallTypeInstall || !IsQueuedExtensionPackagePath(item.Path))
                {
                    continue;
                }

                yield return item.Path;
            }
        }

        private static string TryGetExtensionQueueFilePath()
        {
            if (!string.IsNullOrEmpty(TestExtensionQueueFilePathOverride))
            {
                return TestExtensionQueueFilePathOverride;
            }

            Type pathsType = Type.GetType("Playnite.PlaynitePaths, Playnite");
            return pathsType?.GetProperty(
                "ExtensionQueueFilePath",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
        }

        private static bool IsQueuedExtensionPackagePath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.EndsWith(PackedExtensionFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetPackedExtensionId(string packagePath, out string extensionId)
        {
            extensionId = null;
            if (!IsQueuedExtensionPackagePath(packagePath) || !File.Exists(packagePath))
            {
                return false;
            }

            try
            {
                Type installerType = Type.GetType("Playnite.Plugins.ExtensionInstaller, Playnite");
                MethodInfo getManifest = installerType?.GetMethod(
                    "GetPackedExtensionManifest",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                object manifest = getManifest?.Invoke(null, new object[] { packagePath });
                extensionId = GetProperty(manifest, "Id") as string;
                if (!string.IsNullOrEmpty(extensionId))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to read queued extension manifest via Playnite for {packagePath}.");
            }

            return TryGetExtensionIdFromZip(packagePath, out extensionId);
        }

        private static bool TryGetExtensionIdFromZip(string packagePath, out string extensionId)
        {
            extensionId = null;
            try
            {
                using (var archive = ZipFile.OpenRead(packagePath))
                {
                    var manifestEntry = archive.GetEntry(ExtensionManifestFileName)
                        ?? archive.Entries.FirstOrDefault(entry =>
                            string.Equals(entry.Name, ExtensionManifestFileName, StringComparison.OrdinalIgnoreCase));
                    if (manifestEntry == null)
                    {
                        return false;
                    }

                    using (StreamReader reader = new StreamReader(manifestEntry.Open()))
                    {
                        extensionId = ParseExtensionIdFromYaml(reader.ReadToEnd());
                        return !string.IsNullOrEmpty(extensionId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to read extension manifest from {packagePath}.");
                return false;
            }
        }

        internal static string ParseExtensionIdFromYaml(string yaml)
        {
            if (string.IsNullOrEmpty(yaml))
            {
                return null;
            }

            foreach (string line in yaml.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Id:", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(3).Trim();
                }
            }

            return null;
        }

        private sealed class ExtensionInstallQueueItemDto
        {
            public int InstallType { get; set; }

            public string Path { get; set; }
        }

        public static HltbInstallState GetInstallState(IPlayniteAPI api)
        {
            if (TestInstallStateResolver != null)
            {
                return TestInstallStateResolver(api);
            }

            if (api?.Addons?.Addons == null)
            {
                return HltbInstallState.NotInstalled;
            }

            bool installed = api.Addons.Addons.Any(id =>
                string.Equals(id, HltbExtensionId, StringComparison.OrdinalIgnoreCase));
            if (!installed)
            {
                return HltbInstallState.NotInstalled;
            }

            // Trust the running plugin list first — after a post-enable restart HLTB is loaded here
            // even if DisabledPlugins has not been re-read yet.
            if (HowLongToBeatCache.IsPluginLoaded(api))
            {
                return HltbInstallState.InstalledEnabled;
            }

            IList<string> disabledIds = GetDisabledPluginIdsFromAppSettings() ?? api.Addons.DisabledAddons;
            bool disabled = disabledIds?.Any(id =>
                string.Equals(id, HltbExtensionId, StringComparison.OrdinalIgnoreCase)) == true;
            return disabled ? HltbInstallState.InstalledDisabled : HltbInstallState.InstalledEnabled;
        }

        public static bool IsPluginEnabledInPlaynite(IPlayniteAPI api)
        {
            return GetInstallState(api) == HltbInstallState.InstalledEnabled;
        }

        public static void OpenInstalledAddonPageFromPlaylistPrompt(IPlayniteAPI api)
        {
            RunOnMainView(api, () => OpenAddonsPageCore(api, installedGeneric: true, fromPlaylistPrompt: true));
        }

        public static void OpenBrowseAddonPageFromPlaylistPrompt(IPlayniteAPI api)
        {
            RunOnMainView(api, () => OpenAddonsPageCore(api, installedGeneric: false, fromPlaylistPrompt: true));
        }

        private static void RunOnMainView(IPlayniteAPI api, Action action)
        {
            if (api?.MainView?.UIDispatcher == null
                || api.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                return;
            }

            if (api.MainView.UIDispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                api.MainView.UIDispatcher.Invoke(action);
            }
        }

        private static void OpenAddonsPageCore(IPlayniteAPI api, bool installedGeneric, bool fromPlaylistPrompt)
        {
            object addonsViewModel = null;
            try
            {
                addonsViewModel = CreateAddonsViewModel();
                if (addonsViewModel == null)
                {
                    return;
                }

                int treeTag = installedGeneric ? InstalledGenericTreeTag : BrowseGenericTreeTag;
                object installedPluginItem = null;
                int installedPluginIndex = -1;

                if (installedGeneric)
                {
                    PrepareInstalledGenericView(addonsViewModel);
                    IEnumerable otherPluginList = GetProperty(addonsViewModel, "OtherPluginList") as IEnumerable;
                    installedPluginItem = FindInstalledPlugin(otherPluginList, HltbExtensionId);
                    installedPluginIndex = GetListIndex(otherPluginList, installedPluginItem);
                }

                var navigation = new PendingAddonsNavigation
                {
                    TreeTag = treeTag,
                    InstalledGeneric = installedGeneric,
                    InstalledPluginItem = installedPluginItem,
                    InstalledPluginIndex = installedPluginIndex,
                };

                SchedulePostOpenNavigation(addonsViewModel, api.MainView.UIDispatcher, navigation);
                if (fromPlaylistPrompt)
                {
                    (Playlist.StaticSettings as PlaylistSettings)?.MarkPendingIntegrationEnableFromPlaylistPrompt();
                }

                InvokeOpenView(addonsViewModel);
                RefreshSettingsInstallState();

                if (fromPlaylistPrompt)
                {
                    // Keep persisted pending intent when Add-ons closed with HLTB install queued for restart;
                    // otherwise clear if HLTB is still unavailable (user cancelled the prompt flow).
                    (Playlist.StaticSettings as PlaylistSettings)?.ExpireAddonPendingIfHltbStillUnavailable();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open HowLongToBeat add-on page.");
                if (fromPlaylistPrompt)
                {
                    (Playlist.StaticSettings as PlaylistSettings)?.ExpireAddonPendingIfHltbStillUnavailable();
                }
            }
        }

        private static void RefreshSettingsInstallState()
        {
            (Playlist.StaticSettings as PlaylistSettings)?.RefreshHowLongToBeatInstallState();
            Playlist.StaticPluginInstance?.ApplySettingsToOpenView();
        }

        private static IList<string> GetDisabledPluginIdsFromAppSettings()
        {
            object appSettings = GetPlayniteAppSettings();
            if (appSettings == null)
            {
                return null;
            }

            return GetProperty(appSettings, "DisabledPlugins") as IList<string>;
        }

        private static object GetPlayniteAppSettings()
        {
            Type appType = Type.GetType("Playnite.PlayniteApplication, Playnite");
            object app = appType?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return app?.GetType().GetProperty("AppSettings", BindingFlags.Public | BindingFlags.Instance)?.GetValue(app);
        }

        private static object CreateAddonsViewModel()
        {
            Type appType = Type.GetType("Playnite.PlayniteApplication, Playnite");
            if (appType == null)
            {
                return null;
            }

            object app = appType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object mainModel = appType.GetProperty("MainModelBase", BindingFlags.Public | BindingFlags.Instance)?.GetValue(app);
            if (app == null || mainModel == null)
            {
                return null;
            }

            Type addonsVmType = Type.GetType("Playnite.DesktopApp.ViewModels.AddonsViewModel, Playnite.DesktopApp");
            Type windowFactoryType = Type.GetType("Playnite.DesktopApp.Windows.AddonsWindowFactory, Playnite.DesktopApp");
            if (addonsVmType == null || windowFactoryType == null)
            {
                return null;
            }

            object windowFactory = Activator.CreateInstance(windowFactoryType);
            object dialogs = mainModel.GetType().GetProperty("Dialogs", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mainModel);
            object resources = mainModel.GetType().GetProperty("Resources", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mainModel);
            object extensions = mainModel.GetType().GetProperty("Extensions", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mainModel);
            object appSettings = mainModel.GetType().GetProperty("AppSettings", BindingFlags.Public | BindingFlags.Instance)?.GetValue(mainModel);
            object servicesClient = app.GetType().GetProperty("ServicesClient", BindingFlags.Public | BindingFlags.Instance)?.GetValue(app);

            return Activator.CreateInstance(
                addonsVmType,
                windowFactory,
                dialogs,
                resources,
                servicesClient,
                extensions,
                appSettings,
                app);
        }

        private static void PrepareInstalledGenericView(object addonsViewModel)
        {
            SetProperty(addonsViewModel, "IsUpdateSectionSelected", false);
            object otherPluginList = GetProperty(addonsViewModel, "OtherPluginList");
            SetProperty(addonsViewModel, "ActiveInstalledExtensionsList", otherPluginList);
            SetSelectedSectionView(addonsViewModel, InstalledGenericTreeTag);
        }

        private static void EnsureBrowseGenericSearchMode(object addonsViewModel)
        {
            Type addonTypeEnum = Type.GetType("Playnite.Services.AddonType, Playnite");
            if (addonTypeEnum == null)
            {
                return;
            }

            object genericType = Enum.Parse(addonTypeEnum, "Generic");
            SetField(addonsViewModel, "activeAddonSearchMode", genericType);
        }

        /// <summary>
        /// Playnite's BrowseAddons TextBox uses TwoWay binding with 500 ms delay. Submit search through
        /// the TextBox so SearchAddon runs after Browse &gt; Generic finishes loading.
        /// </summary>
        private static void TriggerBrowseHltbSearch(object addonsViewModel, Window addonsWindow)
        {
            if (GetProperty(addonsViewModel, "IsOnlineListLoading") as bool? ?? false)
            {
                return;
            }

            EnsureBrowseGenericSearchMode(addonsViewModel);
            const string searchTerm = "HowLongToBeat";

            TextBox searchBox = FindBrowseSearchTextBox(addonsWindow);
            if (searchBox != null)
            {
                searchBox.Text = searchTerm;
                BindingOperations.GetBindingExpression(searchBox, TextBox.TextProperty)?.UpdateSource();
            }
            else
            {
                SetProperty(addonsViewModel, "AddonSearchText", searchTerm);
            }

            InvokeMethod(addonsViewModel, "SearchAddon");
        }

        private static TextBox FindBrowseSearchTextBox(Window addonsWindow)
        {
            object sectionView = GetProperty(addonsWindow.DataContext, "SelectedSectionView");
            return PlaylistVisualTree.FindFirstVisualChild<TextBox>(sectionView as DependencyObject, _ => true);
        }

        private static void EnsureBrowseNavigationHandler(
            object addonsViewModel,
            Dispatcher dispatcher,
            PendingAddonsNavigation navigation)
        {
            if (navigation.BrowseHandlerAttached || navigation.InstalledGeneric)
            {
                return;
            }

            if (!(addonsViewModel is INotifyPropertyChanged notify))
            {
                return;
            }

            navigation.BrowseHandlerAttached = true;
            PropertyChangedEventHandler handler = null;
            handler = (sender, e) =>
            {
                if (navigation.SelectionApplied || navigation.InstalledGeneric)
                {
                    return;
                }

                bool isLoadingChange = string.Equals(e.PropertyName, "IsOnlineListLoading", StringComparison.Ordinal);
                if (!isLoadingChange)
                {
                    return;
                }

                if (GetProperty(addonsViewModel, "IsOnlineListLoading") as bool? ?? false)
                {
                    return;
                }

                dispatcher.BeginInvoke(
                    new Action(() => ContinueBrowseNavigation(addonsViewModel, navigation, notify, handler)),
                    DispatcherPriority.Normal);
            };

            notify.PropertyChanged += handler;
            SetProperty(addonsViewModel, "IsUpdateSectionSelected", false);
            ContinueBrowseNavigation(addonsViewModel, navigation, notify, handler);
        }

        private static void ContinueBrowseNavigation(
            object addonsViewModel,
            PendingAddonsNavigation navigation,
            INotifyPropertyChanged notify,
            PropertyChangedEventHandler handler)
        {
            if (navigation.SelectionApplied)
            {
                notify.PropertyChanged -= handler;
                return;
            }

            Window addonsWindow = FindAddonsWindow();
            if (addonsWindow == null)
            {
                return;
            }

            if (!navigation.BrowseTreeSelected)
            {
                if (SelectAddonsTreeByTag(addonsWindow, navigation.TreeTag))
                {
                    navigation.BrowseTreeSelected = true;
                }

                return;
            }

            if (!navigation.BrowseSearchSubmitted)
            {
                TriggerBrowseHltbSearch(addonsViewModel, addonsWindow);
                navigation.BrowseSearchSubmitted = true;
                return;
            }

            if (SelectBrowseHltbAddon(addonsViewModel, addonsWindow))
            {
                navigation.SelectionApplied = true;
                notify.PropertyChanged -= handler;
            }
        }

        private static void SetSelectedSectionView(object addonsViewModel, int viewTag)
        {
            IDictionary sectionViews = GetField(addonsViewModel, "sectionViews") as IDictionary;
            if (sectionViews == null)
            {
                return;
            }

            Type viewEnumType = addonsViewModel.GetType().GetNestedType("View", BindingFlags.NonPublic);
            if (viewEnumType == null)
            {
                return;
            }

            object viewKey = Enum.ToObject(viewEnumType, viewTag);
            if (sectionViews.Contains(viewKey))
            {
                SetProperty(addonsViewModel, "SelectedSectionView", sectionViews[viewKey]);
            }
        }

        private static void InvokeOpenView(object addonsViewModel)
        {
            InvokeMethodWithReturn(addonsViewModel, "OpenView");
        }

        private static void SchedulePostOpenNavigation(object addonsViewModel, Dispatcher dispatcher, PendingAddonsNavigation navigation)
        {
            dispatcher.BeginInvoke(
                new Action(() => TryNavigateAddonsWindow(addonsViewModel, dispatcher, navigation, attempt: 0)),
                DispatcherPriority.ApplicationIdle);
        }

        private static void TryNavigateAddonsWindow(
            object addonsViewModel,
            Dispatcher dispatcher,
            PendingAddonsNavigation navigation,
            int attempt)
        {
            if (navigation.SelectionApplied)
            {
                return;
            }

            Window addonsWindow = FindAddonsWindow();
            if (addonsWindow == null)
            {
                RetryNavigation(addonsViewModel, dispatcher, navigation, attempt);
                return;
            }

            if (!navigation.InstalledGeneric)
            {
                EnsureBrowseNavigationHandler(addonsViewModel, dispatcher, navigation);
                return;
            }

            SetProperty(addonsViewModel, "IsUpdateSectionSelected", false);

            if (!SelectAddonsTreeByTag(addonsWindow, navigation.TreeTag))
            {
                RetryNavigation(addonsViewModel, dispatcher, navigation, attempt);
                return;
            }

            bool targetSelected = JumpToInstalledExtension(addonsViewModel, navigation);

            if (!targetSelected)
            {
                RetryNavigation(addonsViewModel, dispatcher, navigation, attempt);
                return;
            }

            navigation.SelectionApplied = true;
        }

        private static void RetryNavigation(
            object addonsViewModel,
            Dispatcher dispatcher,
            PendingAddonsNavigation navigation,
            int attempt)
        {
            if (attempt >= MaxNavigationAttempts)
            {
                return;
            }

            dispatcher.BeginInvoke(
                new Action(() => TryNavigateAddonsWindow(addonsViewModel, dispatcher, navigation, attempt + 1)),
                DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Mirrors post-progress state from AddonsViewModel.SelectedOnlineAddon without opening a nested
        /// progress dialog during the modal Add-ons window.
        /// </summary>
        private static bool SelectBrowseHltbAddon(object addonsViewModel, Window addonsWindow)
        {
            try
            {
                object hltbAddon = FindHltbBrowseAddon(addonsViewModel);
                if (hltbAddon == null)
                {
                    logger.Warn("HowLongToBeat browse selection skipped: no matching add-on in OnlineAddonList.");
                    return false;
                }

                object manifest = GetProperty(hltbAddon, "InstallerManifest");
                object packages = InvokeParameterlessMethod(manifest, "GetCompatiblePackages")
                    ?? CreateEmptyInstallerPackageList(manifest);

                object selectedPackage = null;
                if (packages is IList packageList && packageList.Count > 0)
                {
                    selectedPackage = packageList[0];
                }

                SetField(addonsViewModel, "selectedOnlineAddon", hltbAddon);
                SetProperty(addonsViewModel, "AvailablePackages", packages);
                SetProperty(addonsViewModel, "SelectedInstallPackage", selectedPackage);
                NotifyPropertyChanged(addonsViewModel, "SelectedOnlineAddon");

                ListBox onlineList = FindBrowseOnlineList(addonsWindow);
                onlineList?.ScrollIntoView(hltbAddon);

                if (!IsMatchingBrowseAddon(GetProperty(addonsViewModel, "SelectedOnlineAddon")))
                {
                    logger.Warn("HowLongToBeat browse selection failed: SelectedOnlineAddon was not applied.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to select HowLongToBeat in add-ons browse list.");
                return false;
            }
        }

        private static object InvokeParameterlessMethod(object target, string methodName)
        {
            if (target == null)
            {
                return null;
            }

            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            return method?.Invoke(target, null);
        }

        private static object CreateEmptyInstallerPackageList(object manifest)
        {
            Type packageType = manifest?.GetType().Assembly.GetType("Playnite.AddonInstallerPackage");
            if (packageType == null)
            {
                return null;
            }

            return Activator.CreateInstance(typeof(List<>).MakeGenericType(packageType));
        }

        private static void NotifyPropertyChanged(object target, string propertyName)
        {
            target?.GetType().GetMethod(
                "OnPropertyChanged",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null)?.Invoke(target, new object[] { propertyName });
        }

        private static object FindHltbBrowseAddon(object addonsViewModel)
        {
            IEnumerable onlineList = GetProperty(addonsViewModel, "OnlineAddonList") as IEnumerable;
            if (onlineList == null)
            {
                return null;
            }

            foreach (object addon in onlineList)
            {
                if (IsMatchingBrowseAddon(addon))
                {
                    return addon;
                }
            }

            return null;
        }

        private static ListBox FindBrowseOnlineList(Window addonsWindow)
        {
            return PlaylistVisualTree.FindFirstVisualChild<ListBox>(addonsWindow, listBox => listBox.Name == "ListOnlineAddons");
        }

        private static bool JumpToInstalledExtension(object addonsViewModel, PendingAddonsNavigation navigation)
        {
            if (navigation.InstalledPluginItem == null || navigation.InstalledPluginIndex < 0)
            {
                return false;
            }

            DependencyObject sectionRoot = GetProperty(addonsViewModel, "SelectedSectionView") as DependencyObject;
            ListBox listBox = PlaylistVisualTree.FindFirstVisualChild<ListBox>(sectionRoot, child => child.Name == "ListPlugins");
            if (listBox == null || listBox.Items.Count <= navigation.InstalledPluginIndex)
            {
                return false;
            }

            if (listBox.SelectedIndex != navigation.InstalledPluginIndex)
            {
                listBox.SelectedIndex = navigation.InstalledPluginIndex;
            }

            object selectedItem = listBox.Items[navigation.InstalledPluginIndex];
            listBox.ScrollIntoView(selectedItem);
            return listBox.SelectedIndex == navigation.InstalledPluginIndex
                && ReferenceEquals(listBox.SelectedItem, navigation.InstalledPluginItem);
        }

        private static bool IsMatchingBrowseAddon(object addon)
        {
            if (addon == null)
            {
                return false;
            }

            string addonId = GetProperty(addon, "AddonId") as string;
            string name = GetProperty(addon, "Name") as string;
            return string.Equals(addonId, HltbExtensionId, StringComparison.OrdinalIgnoreCase)
                || (name != null && name.IndexOf("HowLongToBeat", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static object FindInstalledPlugin(IEnumerable list, string extensionId)
        {
            if (list == null)
            {
                return null;
            }

            foreach (object item in list)
            {
                object description = GetProperty(item, "Description");
                string id = GetProperty(description, "Id") as string;
                if (string.Equals(id, extensionId, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }

        private static int GetListIndex(IEnumerable list, object item)
        {
            if (list == null || item == null)
            {
                return -1;
            }

            if (list is IList indexedList)
            {
                return indexedList.IndexOf(item);
            }

            int index = 0;
            foreach (object current in list)
            {
                if (ReferenceEquals(current, item))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static bool SelectAddonsTreeByTag(DependencyObject addonsWindow, int tag)
        {
            TreeViewItem match = FindTreeViewItemByTagVisual(addonsWindow, tag);
            if (match == null)
            {
                return false;
            }

            ExpandTreeViewItemAncestors(match);
            if (!match.IsSelected)
            {
                match.IsSelected = true;
                match.Focus();
            }

            match.BringIntoView();
            return match.IsSelected;
        }

        private static void ExpandTreeViewItemAncestors(TreeViewItem item)
        {
            DependencyObject current = item;
            while (current != null)
            {
                if (current is TreeViewItem treeItem)
                {
                    treeItem.IsExpanded = true;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        private static TreeViewItem FindTreeViewItemByTagVisual(DependencyObject root, int tag)
        {
            if (root == null)
            {
                return null;
            }

            if (root is TreeViewItem treeItem
                && int.TryParse(treeItem.Tag?.ToString(), out int itemTag)
                && itemTag == tag)
            {
                return treeItem;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                TreeViewItem nested = FindTreeViewItemByTagVisual(VisualTreeHelper.GetChild(root, i), tag);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static Window FindAddonsWindow()
        {
            return Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.GetType().FullName == "Playnite.DesktopApp.Windows.AddonsWindow");
        }

        private static object GetProperty(object target, string name)
        {
            return target?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(target);
        }

        private static void SetProperty(object target, string name, object value)
        {
            target?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.SetValue(target, value);
        }

        private static object GetField(object target, string name)
        {
            return target?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(target, value);
        }

        private static void InvokeMethod(object target, string name)
        {
            if (target == null)
            {
                return;
            }

            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            method?.Invoke(target, null);
        }

        private static object InvokeMethodWithReturn(object target, string name, params object[] args)
        {
            if (target == null)
            {
                return null;
            }

            Type[] argTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                argTypes,
                null);
            return method?.Invoke(target, args);
        }

        private sealed class PendingAddonsNavigation
        {
            public int TreeTag;
            public bool InstalledGeneric;
            public bool BrowseTreeSelected;
            public bool BrowseSearchSubmitted;
            public bool BrowseHandlerAttached;
            public object InstalledPluginItem;
            public int InstalledPluginIndex = -1;
            public bool SelectionApplied;
        }
    }
}
