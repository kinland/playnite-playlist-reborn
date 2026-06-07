# Playlist extension for [Playnite](https://playnite.link/)

This extension provides a sidebar action with a manually ordered "playlist" queue of games.

Games can be added to the playlist by right clicking them in the library view and selecting "Add to Playlist".

This extension can be used as a simple quick access game list, or you can use it to work through your backlog of games. Move games to the top of the list that you intend to play soon. 

The playlist is accessible from the left sidebar in Playnite. Games can be reordered with drag and drop. Right click to remove games, or move a group of games to the top or the bottom. Games can be launched from the play button in the playlist or by pressing enter on the the keyboard.

You can also double-click on the ranking column to get a text box to quickly reorder a game without having to drag it. (Press enter to submit.)

You can sort the Playlist by clicking on column headers, but drag-reorder will be disabled unless you are sorted by rank.

Games are automatically tagged with the "Playlist" tag, and there's an auto-generated Filter with the same name.

This extension requires Playnite 9 or newer.

![Playlist extension screenshot](screenshots/Playlist.gif)

## Search

Playlist includes an inline search box at the top of the view with fuzzy match.

- General search uses case-insensitive partial matching with typo tolerance.
- Wildcards are supported: `*` (any number of characters) and `?` (single character).
- Scoped filters can be mixed with regular name terms in any order, for example:
  - `Alan genre:shooter`
  - `tag:puzzle Court`
  - `dev:remedy feature:co-op`

Supported scoped keywords:

| Keyword | Alias | Matches against |
| --- | --- | --- |
| `tag:` | - | Game tags |
| `genre:` | - | Game genres |
| `developer:` | `dev:` | Game developers |
| `publisher:` | `pub:` | Game publishers |
| `category:` | `cat:` | Game categories |
| `feature:` | `feat:` | Game features |

## External libraries, etc.

* Drag and drop is implemented using [GongSolutions.WPF.DragDrop](https://github.com/punker76/gong-wpf-dragdrop)
* `ui-play` and `play-alt-1` icons are used from [Icofont](https://icofont.com/) (CC BY 4.0)

## HLTB integration note

When the HowLongToBeat plugin is installed, Playlist reads HLTB data from the plugin's on-disk per-game cache files in `ExtensionsData/{hltb-plugin-id}/HowLongToBeat`.

Because those cache files do not include the `HltbUserData` payload used by HLTB's own user-time overlay logic, the HLTB setting `ProgressBarShowTimeUser` is currently not supported in Playlist.

Supported HLTB appearance/settings in Playlist:

- `EnableIntegrationViewItem`
- `EnableIntegrationButton`
- `EnableIntegrationProgressBar`
- `IntegrationViewItemOnlyHour`
- `UseHtltbClassic`
- `UseHtltbAverage`
- `UseHtltbMedian`
- `UseHtltbRushed`
- `UseHtltbLeisure`
- `ShowMainTime`
- `ShowExtraTime`
- `ShowCompletionistTime`
- `ShowSoloTime`
- `ShowCoOpTime`
- `ShowVsTime`
- `ProgressBarShowTime`
- `ProgressBarShowTimeInterior`
- `ProgressBarShowTimeAbove`
- `ProgressBarShowTimeBelow`
- `ProgressBarShowToolTip`
- `ThumbSolidColorBrush` / `ThumbLinearGradient`
- `FirstColorBrush` / `FirstLinearGradient`
- `SecondColorBrush` / `SecondLinearGradient`
- `ThirdColorBrush` / `ThirdLinearGradient`
- `FirstMultiColorBrush` / `FirstMultiLinearGradient`
- `SecondMultiColorBrush` / `SecondMultiLinearGradient`
- `ThirdMultiColorBrush` / `ThirdMultiLinearGradient`

Because those cache files do not include the `HltbUserData` payload used by HLTB's own user-time overlay logic, the HLTB setting `ProgressBarShowTimeUser` is currently not supported in Playlist.

## Credits
This project was forked from https://github.com/bburky/playnite-playlist
HLTB render code and data file formats were recreated by analysing the HLTB plugin source code: https://github.com/Lacro59/playnite-howlongtobeat-plugin