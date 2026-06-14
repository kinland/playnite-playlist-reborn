using Playnite.SDK;

namespace Playlist;

/// <summary>
/// Minimal stub so linked <see cref="HowLongToBeatAddonNavigation"/> compiles in the unit test assembly.
/// Install state is supplied via <see cref="HowLongToBeatAddonNavigation.TestInstallStateResolver"/>.
/// </summary>
internal static class HowLongToBeatCache
{
    public static bool IsPluginLoaded(IPlayniteAPI api) => false;
}
