using Playnite.SDK;

namespace Playlist;

/// <summary>
/// Minimal Playlist host for unit tests; production <see cref="Playlist"/> is not linked into the test assembly.
/// </summary>
public class Playlist
{
    public static IPlayniteAPI StaticPlayniteApi { get; set; }
    public static PlaylistSettings StaticSettings { get; set; }
    internal static Playlist StaticPluginInstance { get; set; }

    public int PersistSettingsCallCount { get; private set; }
    public int SaveSettingsCallCount { get; private set; }

    internal void PersistSettings()
    {
        PersistSettingsCallCount++;
    }

    internal void SaveSettings(PlaylistSettings updatedSettings)
    {
        SaveSettingsCallCount++;
        StaticSettings = updatedSettings;
    }

    internal void ApplySettingsToOpenView()
    {
    }
}
