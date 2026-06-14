# Changelog

All notable changes to the Playlist Playnite extension are documented here.

Release notes are mirrored in `Installer_Manifest.yaml` for the Playnite add-on catalog.
Regenerate with `pwsh ./scripts/sync-changelog.ps1` (appends new commit summaries to the manifest and refreshes `[Unreleased]`).

## [Unreleased]

- Add Playlist settings to map Playnite completion statuses to HowLongToBeat outbound sync tiers.
- Auto-map Playnite Not Played to HLTB Backlog (fixed mapping, not configurable in settings).
- Rename HLTB resource keys in column headers and embedded progress UI.
- Localize completion status names for playlist override mode.
- Refresh localized column headers when language override changes.
- Guard column reorder UI during active playlist row drags.
- Add localized strings for Playlist language picker.
- Show blocked reorder feedback with a mouse-following adorner.
- Prefer supplemental override strings in HLTB label resolver.
- Update locale sync scripts for HLTB and menu key names.
- Add Playlist language catalog and override runtime.
- Add Scots DSL lookup tooling for supplemental locale maintenance.
- Add supplemental borrowed-locale sync script and translation data.
- Persist Playlist language override in settings.
- Catalog borrowed Playnite keys for supplemental locale overrides.
- Rename playlist-owned keys in Playnite-supported locale files.
- Replace merged override dictionaries safely when locale changes.
- Stabilize language override ComboBox binding in settings.
- Sync borrowed Playnite strings into supplemental locale files.
- Wire Playlist language picker in settings and views.

## [1.7.0] - 2026-06-14

- HowLongToBeat integration in the playlist grid
  - Playtime, progress bar, and detail button in each row
  - Sort and display follow the HLTB plugin preferred time type; header shows category on hover and when active
  - Respects HLTB appearance settings (segments, progress bar, time display)
  - Prompts to install or enable the plugin when unavailable
- Optional columns (toggle from the column header menu)
  - Time Played with theme-aware formatting
  - Completion Status
  - HowLongToBeat (hidden when the plugin is missing or integration is disabled)
  - Last Played with relative-time labels
  - Last Activity (hidden by default; keyed on Game.Modified)
- Playlist search improvements
  - Scoped filters: tag, genre, developer, publisher, category, feature (with aliases)
  - Negation, OR/AND combinators, and quoted values
  - Clear button; optional sync with Playnite's main filter panel
- Sorting and drag reorder
  - Sort by any column; sortable headers use a hand cursor
  - Drag-reorder by rank only, with no-drop cursor when unavailable
  - Time Played and Last Played sorts pin unplayed games to the bottom; Time Played defaults to descending
  - Last Played drag reorder stays within the same relative-time bucket
- Column layout and styling
  - Persist column order, widths, visibility, and sort state across restarts
  - Drag column headers to reorder; theme-aware full-height drop indicator while dragging; distribute widths on first launch
  - Theme-agnostic headers, sort highlighting, and row styling
- Localization for all 45 Playnite-supported locales plus 14 supplemental Playlist locales (machine translated except initial Spanish by BanCrash)
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

