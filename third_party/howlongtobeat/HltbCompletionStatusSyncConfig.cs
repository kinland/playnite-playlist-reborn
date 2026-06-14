using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Playlist
{
    /// <summary>
    /// Reads and writes HowLongToBeat plugin completion-status sync settings in its on-disk config.
    /// Playlist owns the mapping UI; HLTB's plugin performs outbound sync when statuses change.
    /// </summary>
    internal static class HltbCompletionStatusSyncConfig
    {
        private static readonly Guid HltbPluginId = Guid.Parse("e08cd51f-9c9a-4ee3-a094-fde03b55492f");
        private static readonly ILogger Logger = LogManager.GetLogger();
        private static readonly object ConfigWriteLock = new object();

        internal static string TestConfigPathOverride { get; set; }

        internal static HltbCompletionStatusMapping ReadMapping()
        {
            string configPath = ResolveConfigPath();
            if (string.IsNullOrEmpty(configPath) || !File.Exists(configPath))
            {
                return new HltbCompletionStatusMapping();
            }

            try
            {
                string json = HltbCacheFileAccess.ReadTextAllowingWriter(configPath);
                return ParseMapping(json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to read HowLongToBeat completion status sync settings.");
                return new HltbCompletionStatusMapping();
            }
        }

        internal static bool TryWriteMapping(HltbCompletionStatusMapping mapping)
        {
            if (mapping == null)
            {
                return false;
            }

            string configPath = ResolveConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                return false;
            }

            lock (ConfigWriteLock)
            {
                try
                {
                    JsonObject root;
                    if (File.Exists(configPath))
                    {
                        string existingJson = HltbCacheFileAccess.ReadTextAllowingWriter(configPath);
                        root = JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject();
                    }
                    else
                    {
                        root = new JsonObject();
                    }

                    ApplyMapping(root, mapping);

                    string directory = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string tempPath = configPath + ".playlist.tmp";
                    string payload = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
                    File.WriteAllText(tempPath, payload, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                    }

                    File.Move(tempPath, configPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to write HowLongToBeat completion status sync settings.");
                    return false;
                }
            }
        }

        internal static void ApplyPlaylistSettings(IPlayniteAPI api, PlaylistSettings settings)
        {
            if (api == null || settings == null)
            {
                return;
            }

            if (!settings.EnableHowLongToBeatIntegration
                || settings.HowLongToBeatInstallState != HltbInstallState.InstalledEnabled)
            {
                return;
            }

            if (!settings.SyncCompletionStatusWithHltb)
            {
                HltbCompletionStatusMapping current = ReadMapping();
                if (current.AutoSetGameStatusToHltb)
                {
                    current.AutoSetGameStatusToHltb = false;
                    TryWriteMapping(current);
                }

                return;
            }

            HltbCompletionStatusMapping mapping = settings.ToHltbCompletionStatusMapping();
            if (!mapping.IsConfigured())
            {
                mapping = HltbCompletionStatusMapping.ResolveDefaults(api.Database.CompletionStatuses);
                settings.ApplyHltbCompletionStatusMapping(mapping);
            }

            mapping.ApplyFixedBacklogMapping(api.Database.CompletionStatuses);
            mapping.AutoSetGameStatusToHltb = true;
            TryWriteMapping(mapping);
        }

        internal static void ImportIntoPlaylistSettings(IPlayniteAPI api, PlaylistSettings settings)
        {
            if (api == null || settings == null)
            {
                return;
            }

            if (settings.HltbSyncStatusPlayingId != Guid.Empty
                && settings.HltbSyncStatusCompletedId != Guid.Empty
                && settings.HltbSyncStatusCompletionistId != Guid.Empty)
            {
                return;
            }

            HltbCompletionStatusMapping fromHltb = ReadMapping();
            if (fromHltb.IsConfigured())
            {
                settings.SyncCompletionStatusWithHltb = fromHltb.AutoSetGameStatusToHltb;
                settings.ApplyHltbCompletionStatusMapping(fromHltb);
                return;
            }

            HltbCompletionStatusMapping defaults = HltbCompletionStatusMapping.ResolveDefaults(api.Database.CompletionStatuses);
            if (defaults.IsConfigured())
            {
                settings.ApplyHltbCompletionStatusMapping(defaults);
            }
        }

        internal static string ResolveConfigPath()
        {
            if (!string.IsNullOrEmpty(TestConfigPathOverride))
            {
                return TestConfigPathOverride;
            }

            try
            {
                string thisPluginPath = Playlist.StaticPluginUserDataPath;
                if (string.IsNullOrEmpty(thisPluginPath))
                {
                    return null;
                }

                string extensionsDataPath = Directory.GetParent(thisPluginPath)?.FullName;
                if (string.IsNullOrEmpty(extensionsDataPath))
                {
                    return null;
                }

                string pluginUserDataPath = Path.Combine(extensionsDataPath, HltbPluginId.ToString());
                string[] candidates =
                {
                    Path.Combine(pluginUserDataPath, "config.json"),
                    Path.Combine(pluginUserDataPath, "settings.json"),
                    Path.Combine(pluginUserDataPath, "HowLongToBeatSettings.json"),
                };
                return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
            }
            catch
            {
                return null;
            }
        }

        internal static HltbCompletionStatusMapping ParseMapping(string json)
        {
            var mapping = new HltbCompletionStatusMapping();
            if (string.IsNullOrWhiteSpace(json))
            {
                return mapping;
            }

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                Walk(document.RootElement, mapping);
            }

            return mapping;
        }

        private static void Walk(JsonElement element, HltbCompletionStatusMapping mapping)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "AutoSetGameStatusToHltb" when property.Value.ValueKind == JsonValueKind.True
                        || property.Value.ValueKind == JsonValueKind.False:
                        mapping.AutoSetGameStatusToHltb = property.Value.GetBoolean();
                        break;
                    case "GameStatusPlaying":
                        TryApplyGuid(property.Value, value => mapping.GameStatusPlaying = value);
                        break;
                    case "GameStatusCompleted":
                        TryApplyGuid(property.Value, value => mapping.GameStatusCompleted = value);
                        break;
                    case "GameStatusCompletionist":
                        TryApplyGuid(property.Value, value => mapping.GameStatusCompletionist = value);
                        break;
                    case "GameStatusBacklog":
                        TryApplyGuid(property.Value, value => mapping.GameStatusBacklog = value);
                        break;
                }

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    Walk(property.Value, mapping);
                }
            }
        }

        private static void ApplyMapping(JsonObject root, HltbCompletionStatusMapping mapping)
        {
            root["AutoSetGameStatusToHltb"] = mapping.AutoSetGameStatusToHltb;
            WriteGuid(root, "GameStatusPlaying", mapping.GameStatusPlaying);
            WriteGuid(root, "GameStatusCompleted", mapping.GameStatusCompleted);
            WriteGuid(root, "GameStatusCompletionist", mapping.GameStatusCompletionist);
            WriteGuid(root, "GameStatusBacklog", mapping.GameStatusBacklog);
        }

        private static void WriteGuid(JsonObject root, string propertyName, Guid value)
        {
            if (value == Guid.Empty)
            {
                root.Remove(propertyName);
                return;
            }

            root[propertyName] = value.ToString();
        }

        private static void TryApplyGuid(JsonElement value, Action<Guid> apply)
        {
            if (value.ValueKind == JsonValueKind.String
                && Guid.TryParse(value.GetString(), out Guid parsed))
            {
                apply(parsed);
            }
        }
    }

    internal sealed class HltbCompletionStatusMapping
    {
        internal bool AutoSetGameStatusToHltb { get; set; }
        internal Guid GameStatusPlaying { get; set; }
        internal Guid GameStatusCompleted { get; set; }
        internal Guid GameStatusCompletionist { get; set; }
        internal Guid GameStatusBacklog { get; set; }

        internal const string NotPlayedCanonicalName = "Not Played";

        internal bool IsConfigured()
        {
            return GameStatusPlaying != Guid.Empty
                && GameStatusCompleted != Guid.Empty
                && GameStatusCompletionist != Guid.Empty;
        }

        internal static HltbCompletionStatusMapping ResolveDefaults(IEnumerable<CompletionStatus> completionStatuses)
        {
            var mapping = new HltbCompletionStatusMapping();
            if (completionStatuses == null)
            {
                return mapping;
            }

            List<CompletionStatus> statuses = completionStatuses
                .Where(status => status != null && status.Id != Guid.Empty)
                .ToList();
            if (statuses.Count == 0)
            {
                return mapping;
            }

            mapping.GameStatusPlaying = ResolveByNames(statuses, "Playing");
            mapping.GameStatusCompleted = ResolveByNames(statuses, "Beaten", "Completed");
            mapping.GameStatusCompletionist = ResolveByNames(statuses, "Completed", "Completionist");
            mapping.ApplyFixedBacklogMapping(statuses);

            if (mapping.GameStatusCompleted != Guid.Empty
                && mapping.GameStatusCompletionist == mapping.GameStatusCompleted
                && statuses.Count >= 3)
            {
                CompletionStatus alternate = statuses.FirstOrDefault(status =>
                    status.Id != mapping.GameStatusPlaying
                    && status.Id != mapping.GameStatusCompleted);
                if (alternate != null)
                {
                    mapping.GameStatusCompletionist = alternate.Id;
                }
            }

            return mapping;
        }

        internal void ApplyFixedBacklogMapping(IEnumerable<CompletionStatus> completionStatuses)
        {
            GameStatusBacklog = ResolveNotPlayedId(completionStatuses);
        }

        internal static Guid ResolveNotPlayedId(IEnumerable<CompletionStatus> completionStatuses)
        {
            if (completionStatuses == null)
            {
                return Guid.Empty;
            }

            List<CompletionStatus> statuses = completionStatuses
                .Where(status => status != null && status.Id != Guid.Empty)
                .ToList();
            return ResolveByNames(statuses, NotPlayedCanonicalName);
        }

        private static Guid ResolveByNames(IEnumerable<CompletionStatus> statuses, params string[] preferredNames)
        {
            foreach (string preferredName in preferredNames)
            {
                CompletionStatus match = statuses.FirstOrDefault(status =>
                    string.Equals(status.Name, preferredName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return match.Id;
                }
            }

            return Guid.Empty;
        }
    }
}
