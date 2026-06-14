# Testing

## Overview

This repository includes two test projects:

- `tests/Playlist.UnitTests` — fast unit tests for parser, sync, layout, and HLTB helpers.
- `tests/Playlist.UiTests` — STA-threaded WPF smoke tests for the HLTB progress bar and sort-header layout.

## Run tests

From repository root:

- `dotnet test Playlist.sln` (runs unit and UI tests)
- `dotnet test tests/Playlist.UnitTests/Playlist.UnitTests.csproj`
- `dotnet test tests/Playlist.UiTests/Playlist.UiTests.csproj`

Unit tests cover search sync (including live playlist→main push), HLTB sort keys,
interior label overlap suppression, column layout persistence, and localized HLTB
header resource-key mapping.

UI tests run on an explicit STA helper (`StaUiTest`) and validate real
`HowLongToBeatCachedProgressBar` behavior:

- playtime marker geometry and rounded-corner styling
- unknown-state rendering for non-game data contexts (`--` and hidden marker)
- game-backed segment rendering and HLTB URL tooltip wiring
- above/below label strips when interior labels are disabled
- interior label canvas when `ProgressBarShowTimeInterior` is enabled
- integration disable behavior (`EnableIntegrationProgressBar`)
- custom segment brush rendering (solid/gradient appearance path)
- sort-header layout helpers that require WPF measurement

## Playnite plugin testing notes

- Playnite plugin behavior is easiest to validate manually by loading the extension in Playnite and attaching debugger/runtime logs.
- There is no official end-to-end test harness in Playnite SDK docs; practical strategy is:
  - keep domain/parsing logic unit-tested,
  - keep UI behavior smoke-tested on STA threads and progressively automate where feasible.

References:

- [Playnite plugins introduction](https://api.playnite.link/docs/tutorials/extensions/plugins.html)
- [Playnite extensions intro](https://api.playnite.link/docs/tutorials/extensions/intro.html)

## Packaging safety

Tests are isolated in separate projects under `tests/` and are not referenced by `Playlist.csproj`.

`Playlist.csproj` packages extension artifacts from its own output path (`bin/Debug` or `bin/Release`) and selected files only, so test binaries are not included in `.pext`.
