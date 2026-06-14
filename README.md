# Playlist extension for [Playnite](https://playnite.link/)

### Localization

العربية · Български · Català · Čeština · Deutsch · Ελληνικά · Español · Français · עברית · 日本語 · 한국어 · Polski · Português · Русский · српски · 简体中文 · 繁體中文 · …

<details>
<summary>Supports all 45 Playnite locales:</summary>

| Locale | Language | Autonym |
| --- | --- | --- |
| `af_ZA` | Afrikaans | Afrikaans |
| `ar_SA` | Arabic | العربية |
| `bg_BG` | Bulgarian | Български |
| `ca_ES` | Catalan | Català |
| `cs_CZ` | Czech | Čeština |
| `cy_GB` | Welsh | Cymraeg |
| `da_DK` | Danish | Dansk |
| `de_DE` | German | Deutsch |
| `el_GR` | Greek | Ελληνικά |
| `en_US` | English | English |
| `eo_UY` | Esperanto | Esperanto |
| `es_ES` | Spanish | Español |
| `et_EE` | Estonian | Eesti |
| `fa_IR` | Persian | فارسی |
| `fi_FI` | Finnish | Suomi |
| `fr_FR` | French | Français |
| `ga_IE` | Irish | Gaeilge |
| `gl_ES` | Galician | Galego |
| `he_IL` | Hebrew | עברית |
| `hr_HR` | Croatian | Hrvatski |
| `hu_HU` | Hungarian | Magyar |
| `id_ID` | Indonesian | Bahasa Indonesia |
| `it_IT` | Italian | Italiano |
| `ja_JP` | Japanese | 日本語 |
| `ka_GE` | Georgian | ქართული |
| `ko_KR` | Korean | 한국어 |
| `lt_LT` | Lithuanian | Lietuvių |
| `mr_IN` | Marathi | मराठी |
| `nl_NL` | Dutch | Nederlands |
| `no_NO` | Norwegian | Norsk |
| `pl_PL` | Polish | Polski |
| `pt_BR` | Portuguese (Brazil) | Português (Brasil) |
| `pt_PT` | Portuguese (Portugal) | Português (Portugal) |
| `ro_RO` | Romanian | Română |
| `ru_RU` | Russian | Русский |
| `si_LK` | Sinhala | සිංහල |
| `sk_SK` | Slovak | Slovenčina |
| `sl_SI` | Slovenian | Slovenščina |
| `sr_SP` | Serbian | српски / srpski |
| `sv_SE` | Swedish | Svenska |
| `tr_TR` | Turkish | Türkçe |
| `uk_UA` | Ukrainian | Українська |
| `vi_VN` | Vietnamese | Tiếng Việt |
| `zh_CN` | Chinese (Simplified) | 简体中文 |
| `zh_TW` | Chinese (Traditional) | 繁體中文 |

</details>

Playnite UI strings for this extension are available in every locale Playnite ships. I only speak English, so these are primarily machine translated — contributions welcome.

This extension provides a sidebar action with a manually ordered "playlist" queue of games.

Games can be added to the playlist by right clicking them in the library view and selecting "Add to Playlist".

This extension can be used as a simple quick access game list, or you can use it to work through your backlog of games. Move games to the top of the list that you intend to play soon. 

The playlist is accessible from the left sidebar in Playnite. Games can be reordered with drag and drop. Right click to remove games, or move a group of games to the top or the bottom. Games can be launched from the play button in the playlist or by pressing enter on the the keyboard.

You can also double-click on the ranking column to get a text box to quickly reorder a game without having to drag it. (Press enter to submit.)

You can sort the Playlist by clicking on column headers (Rank, Name, Time Played, Completion Status, Last Played, Last Activity, and HowLongToBeat when enabled). The active sort column shows a direction glyph. Drag-reorder is disabled unless you are sorted by rank; when it is unavailable the cursor shows a no-drop indicator.

When sorted by **Time Played** or **Last Played**, games with no play time or last-played date are pinned to the bottom. Time Played defaults to descending on first click. When sorted by Last Played, drag-reorder only moves games within the same relative-time bucket (for example, two games both labeled "3 days ago").

Sorting by **HowLongToBeat** uses the HLTB plugin's preferred time category (Main Story, Completionist, and so on). Hover the column header to see which category will be used; the active sort label includes it.

Games are automatically tagged with the "Playlist" tag, and there's an auto-generated Filter with the same name.

![Playlist extension screenshot](screenshots/Playlist.gif)

## Columns

Optional columns can be shown or hidden from the column header right-click menu. Column order, widths, visibility, and sort state are persisted across sessions. Drag column headers to reorder them; a theme-aware insert guide appears while dragging.

Available columns:

- **Rank** — playlist position; double-click to jump to a rank via text box.
- **Icon** — game cover (fixed width, not resizable).
- **Name** — game title.
- **Time Played** — formatted play time from Playnite.
- **Completion Status** — completion state from your library.
- **Last Played** — relative labels such as "Moments ago", "3 days ago", or blank when never played.
- **Last Activity** (hidden by default) — same relative formatting as Last Played, but keyed on `Game.Modified` so installs and other record updates appear alongside plays.
- **HowLongToBeat** — HLTB playtime, progress bar, and detail button when the plugin is installed and integration is enabled (see below).

Layout notes:

- **HowLongToBeat** is hidden automatically when the HLTB plugin is not installed or integration is disabled in Playlist settings.
- Resizing a toggleable column to zero width hides it (same as the header menu).
- Column order is remembered even when columns are hidden.
- On first launch, column widths are distributed from sensible minimums so the list fills the view; saved layouts are restored afterward and only reflow when they would overflow horizontally. Resizing via a column gripper saves that width; growing the window restores saved widths rather than reflowing them.

## Settings

Open Playlist settings from Playnite's extension settings. Per-column visibility is controlled from the column header menu instead.

- **Enable HowLongToBeat integration** — gates reading HLTB cache data, not just column visibility. When HLTB is missing or disabled in Playnite, the checkbox offers to open Add-ons to install or enable the plugin; after install, Playlist can apply a pending enable intent on next launch.
- **Sync search with main filter panel** — keeps the playlist search box aligned with Playnite's library filter search (including negation, OR/AND, and quoted values).

## Search

Playlist includes an inline search box at the top of the view with fuzzy match.

- General search uses case-insensitive partial matching with typo tolerance.
- Wildcards are supported: `*` (any number of characters) and `?` (single character).
- Scoped filters can be mixed with regular name terms in any order (see keywords below).
- Prefix a scoped filter with `!` to negate it, for example `Alan !dev:remedy` or `!tag:backlog`.
- Combine multiple values on one scope with OR (`dev:10tons,17-BIT` or `dev:10tons|17-BIT`) or AND (`tag:fps&roguelike`). Repeating a scope also means AND (`tag:fps tag:roguelike`).
- Quote names or values that contain spaces or special characters, for example `"Alan Wake"` or `dev:"11 bit studios"`. Inside quotes, `,`, `|`, and `&` are treated as literal text.
- Example queries:
  - `Alan genre:shooter`
  - `tag:puzzle Court`
  - `dev:remedy feature:co-op`
  - `Alan !dev:remedy`
  - `dev:10tons|17-BIT`
  - `"Alan Wake" !tag:backlog`
- A clear button resets the search box.
- Optional **Sync search with main filter panel** (Playlist settings) keeps the playlist search box aligned with Playnite's library filter search.

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

## HowLongToBeat integration

When the HowLongToBeat plugin is installed and integration is enabled, Playlist renders HLTB playtime, a multi-segment progress bar, and the HLTB detail button in the playlist grid. Sorting and display respect the HLTB plugin's preferred time type and appearance settings.

Playlist reads HLTB data from the plugin's on-disk per-game cache files in `ExtensionsData/{hltb-plugin-id}/HowLongToBeat`. Cache reads use shared read access so Playlist does not lock files while the HLTB plugin updates them; malformed or partially written JSON is ignored and treated as missing data.

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
- `PreferredForTimeToBeat`
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

Segment colors are read from the HLTB plugin appearance settings when available.

## Credits
This project was forked from https://github.com/bburky/playnite-playlist

HLTB render code and data file formats were recreated by analysing the HLTB plugin source code: https://github.com/Lacro59/playnite-howlongtobeat-plugin