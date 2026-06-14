# Changelog

All notable changes to the Playlist Playnite extension are documented here.

Release notes are mirrored in `Installer_Manifest.yaml` for the Playnite add-on catalog.
Regenerate this file with `pwsh ./scripts/sync-changelog.ps1`.

## [Unreleased]

_No unreleased changes recorded yet._

## [1.7.0] - 2026-06-14

- Optional columns (toggle from the column header menu)
  - Rank with double-click jump to position
  - Last Played with relative-time labels
  - Time Played with theme-aware formatting
  - Completion Status
  - HowLongToBeat (hidden when the plugin is missing or integration is disabled)
  - Last Activity (hidden by default; keyed on Game.Modified)
- Inline playlist search
  - Fuzzy matching with `*` and `?` wildcards
  - Scoped filters: tag, genre, developer, publisher, category, feature (with aliases)
  - Negation, OR/AND combinators, and quoted values
  - Clear button; optional sync with Playnite's main filter panel
- HowLongToBeat integration in the playlist grid
  - Playtime, progress bar, and detail button in each row
  - Sort and display follow the HLTB plugin preferred time type
  - Respects HLTB appearance settings (segments, progress bar, time display)
  - Prompts to install or enable the plugin when unavailable
- Sorting and drag reorder
  - Sort by any column; active column shows a direction glyph
  - Drag-reorder by rank only, with no-drop cursor when unavailable
  - Time Played and Last Played sorts pin unplayed games to the bottom
  - Last Played drag reorder stays within the same relative-time bucket
- Column layout and styling
  - Persist order, widths, visibility, and sort state across restarts
  - Drag column headers to reorder; distribute widths on first launch
  - Theme-agnostic headers, sort highlighting, and row styling
- Localization for all 45 Playnite-supported locales plus 14 supplemental Playlist locales (machine translated except initial Spanish by BanCrash)
- Synchronised Playlist tag on member games and auto-generated Playlist filter
- Requires Playnite API 6.5.0 or newer

## [1.6.1] - 2026-06-06

- Fuzzy search with tag filters and wildcards; active sort column indicators
- Persist column layouts and sort state across sessions

## [1.6.0] - 2026-06-06

- Last Played column
- Fuzzy search in the playlist view

## [1.5.1] - 2026-04-26

- Update add-on package URL for the playnite-playlist-reborn repository

## [1.5.0] - 2026-04-26

- First release under Kinland as primary maintainer (same feature set as v1.4.3)

**Maintainer change:** After **v1.4.2**, primary maintenance transferred from [@bburky](https://github.com/bburky) to [@Kinland](https://github.com/kinland). **v1.5.0** is the first release with Kinland as primary maintainer.

## [1.4.3] - 2026-03-20

- Rank column with double-click text box to reorder
- Column sorting
- HowLongToBeat plugin support
- Synchronised Playlist tag and filter
- Spanish localization (thanks @BanCrash)

## [1.4.2] - 2021-11-02

- Fix selecting Add to Playlist before opening the sidebar

## [1.4.1] - 2021-10-28

- Fix error logged to playnite.log when Playnite is in fullscreen mode

## [1.4] - 2021-10-22

- Double-click to start games, Delete key to remove games, and improved context menu

## [1.3] - 2021-09-18

- Context menu to set completion status

## [1.2] - 2021-09-18

- Installation and launch status indicators; UI fixes for icon and context menu

## [1.1] - 2021-09-13

- Initial release

