namespace Playlist
{
    public sealed class PlaylistLanguageOption
    {
        public PlaylistLanguageOption(string localeId, string displayName, PlaylistLanguageOptionKind kind)
        {
            LocaleId = localeId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Kind = kind;
        }

        /// <summary>Empty when the option follows Playnite's configured language.</summary>
        public string LocaleId { get; }

        public string DisplayName { get; }

        public PlaylistLanguageOptionKind Kind { get; }
    }

    public enum PlaylistLanguageOptionKind
    {
        Playnite,
        Os,
        Supplemental,
    }
}
