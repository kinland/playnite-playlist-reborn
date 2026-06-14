HowLongToBeat plugin attribution
================================

Upstream project:

- https://github.com/Lacro59/playnite-howlongtobeat-plugin

License:

- MIT (see `LICENSE` in this folder). The disclaimer at the top of `LICENSE`
  limits that MIT text to the HLTB-related sources **in this folder only**.

What lives here:

Cache and settings:

- `HowLongToBeatCache.cs` — resolves HLTB extension data paths, reads per-game
  cache JSON and plugin settings, and exposes render settings / cached times to
  Playlist.
- `HltbCacheFileAccess.cs` — shared-read file access helpers used while the HLTB
  extension may be writing cache files.
- `HltbSettingsJson.cs` — merges HLTB plugin settings from Playnite's on-disk
  JSON into `HltbRenderSettings`.
- `HltbPlaytimeFormat.cs` — formats playtime labels to match HLTB / Playnite
  conventions.

UI and navigation:

- `HowLongToBeatCachedProgressBar.cs` — list-view HLTB progress bar (segment
  geometry, labels, and playtime marker) driven by cached times.
- `HltbInteriorLabelOverlap.cs` — overlap suppression for interior time labels.
- `HowLongToBeatPluginButtonHost.cs` — optional embedding of the HLTB extension's
  small game view control in Playlist rows.
- `HowLongToBeatAddonNavigation.cs` — detects whether HowLongToBeat is
  installed/enabled and opens Playnite's Add-ons dialog on the HLTB entry.

Sorting:

- `HltbSortKeyBuilder.cs` — builds sort keys for the HLTB column from cached
  times and plugin time-type preferences.

Implementation was informed by reading the upstream HowLongToBeat plugin and
related materials.

Why this notice exists:

- To record third-party provenance and keep attribution transparent for
  HLTB-derived interoperability code, separate from the rest of the Playlist
  extension.
