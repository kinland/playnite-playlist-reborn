# Testing

## Overview

This repository now includes two test projects:

- `tests/Playlist.UnitTests` - fast unit tests for parser/formatting logic.
- `tests/Playlist.UiTests` - UI-test scaffold for future Playnite-hosted automation.

## Run tests

From repository root:

- `dotnet test tests/Playlist.UnitTests/Playlist.UnitTests.csproj`
- `dotnet test tests/Playlist.UiTests/Playlist.UiTests.csproj`

Current UI tests run on STA and validate real `HowLongToBeatCachedProgressBar` behavior:

- playtime marker geometry and rounded-corner styling
- unknown-state rendering for non-game data contexts (`--` and hidden marker)
- game-backed segment rendering and HLTB URL tooltip wiring
- label placement settings (`ProgressBarShowTimeAbove` / `ProgressBarShowTimeBelow`)
- integration disable behavior (`EnableIntegrationProgressBar`)
- custom segment brush rendering (solid/gradient appearance path)

## Playnite plugin testing notes

- Playnite plugin behavior is easiest to validate manually by loading the extension in Playnite and attaching debugger/runtime logs.
- There is no official end-to-end test harness in Playnite SDK docs; practical strategy is:
  - keep domain/parsing logic unit-tested,
  - keep UI behavior smoke-tested manually and progressively automate where feasible.

References:

- [Playnite plugins introduction](https://api.playnite.link/docs/tutorials/extensions/plugins.html)
- [Playnite extensions intro](https://api.playnite.link/docs/tutorials/extensions/intro.html)

## Packaging safety

Tests are isolated in separate projects under `tests/` and are not referenced by `Playlist.csproj`.

`Playlist.csproj` packages extension artifacts from its own output path (`bin/Debug` or `bin/Release`) and selected files only, so test binaries are not included in `.pext`.
