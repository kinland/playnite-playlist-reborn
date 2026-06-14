using Playnite.SDK;

namespace Playlist;

/// <summary>
/// Minimal Playlist host for unit tests; production <see cref="Playlist"/> is not linked into the test assembly.
/// </summary>
public class Playlist
{
    public static IPlayniteAPI StaticPlayniteApi { get; set; }
    public static string StaticPluginUserDataPath { get; set; }
    public static PlaylistSettings StaticSettings { get; set; }
    internal static Playlist StaticPluginInstance { get; set; }

    internal void PersistSettings()
    {
        StaticSettings?.NotifyPersistedToStorage();
    }

    internal void SaveSettings(PlaylistSettings updatedSettings)
    {
        StaticSettings = updatedSettings;
    }

    internal void ApplySettingsToOpenView()
    {
    }
}
