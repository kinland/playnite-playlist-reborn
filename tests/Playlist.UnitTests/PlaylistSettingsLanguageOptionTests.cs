using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Playlist.UnitTests;

public class PlaylistSettingsLanguageOptionTests
{
    [Fact]
    public void RefreshLanguageOptions_preserves_selected_language_option()
    {
        var settings = new PlaylistSettings();
        settings.LanguageOverrideLocaleId = "haw_US";

        settings.RefreshLanguageOptions();

        Assert.Equal("haw_US", settings.LanguageOverrideLocaleId);
        Assert.Equal("haw_US", settings.LanguageOverrideComboValue);
        Assert.NotNull(settings.SelectedLanguageOption);
        Assert.Equal("haw_US", settings.SelectedLanguageOption.LocaleId);
    }

    [Fact]
    public void RefreshLanguageOptions_twice_preserves_combo_value_for_begin_edit_timing()
    {
        var settings = new PlaylistSettings();
        settings.LanguageOverrideLocaleId = "gd_GB";

        settings.RefreshLanguageOptions();
        settings.RefreshLanguageOptions();

        Assert.Equal("gd_GB", settings.LanguageOverrideLocaleId);
        Assert.Equal("gd_GB", settings.LanguageOverrideComboValue);
        Assert.Equal("gd_GB", settings.SelectedLanguageOption?.LocaleId);
    }

    [Fact]
    public void LanguageOverrideComboValue_empty_string_clears_override()
    {
        var settings = new PlaylistSettings();
        settings.LanguageOverrideLocaleId = "haw_US";

        settings.LanguageOverrideComboValue = string.Empty;

        Assert.Null(settings.LanguageOverrideLocaleId);
        Assert.Equal(string.Empty, settings.LanguageOverrideComboValue);
    }

    [Fact]
    public void SelectedLanguageOption_follow_playnite_uses_empty_locale_id()
    {
        var settings = new PlaylistSettings();
        settings.LanguageOverrideLocaleId = null;
        settings.RefreshLanguageOptions();

        PlaylistLanguageOption selected = settings.SelectedLanguageOption;
        Assert.NotNull(selected);
        Assert.Equal(string.Empty, selected.LocaleId);
        Assert.Equal(string.Empty, settings.LanguageOverrideComboValue);
        Assert.Equal(PlaylistLanguageOptionKind.Playnite, selected.Kind);
    }

    [Fact]
    public void RefreshLanguageOptions_notifies_language_options_for_combo_resync()
    {
        var settings = new PlaylistSettings();
        settings.LanguageOverrideLocaleId = "gd_GB";
        var notified = new List<string>();
        settings.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        settings.RefreshLanguageOptions();

        Assert.Contains(nameof(PlaylistSettings.LanguageOptions), notified);
    }

    [Fact]
    public void SelectedLanguageOption_updates_language_override_locale_id()
    {
        var settings = new PlaylistSettings();
        settings.RefreshLanguageOptions();

        PlaylistLanguageOption supplemental = settings.LanguageOptions
            .First(option => option.Kind == PlaylistLanguageOptionKind.Supplemental);
        settings.SelectedLanguageOption = supplemental;

        Assert.Equal(supplemental.LocaleId, settings.LanguageOverrideLocaleId);
    }
}
