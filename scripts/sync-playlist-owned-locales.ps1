#!/usr/bin/env pwsh

# Requires PowerShell 7+ (pwsh) for reliable UTF-8 JSON/locale handling.
# Syncs Playlist-owned locale keys from scripts/data/playlist-owned-locale-translations.json
# into Localization/*.xaml (en_US is the English baseline source, not modified).

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$playlistLocalizationDir = Join-Path $ProjectDir "Localization"
$translationsPath = Join-Path $PSScriptRoot "data\playlist-owned-locale-translations.json"
$enUsPath = Join-Path $playlistLocalizationDir "en_US.xaml"

$playlistKeys = @(
    "LOCPlaylist_DragReorderBlocked_SortActive",
    "LOCPlaylist_DragReorderBlocked_Bucket",
    "LOCPlaylist_LastPlayed_MomentsAgo",
    "LOCPlaylist_LastPlayed_OneMinuteAgo",
    "LOCPlaylist_LastPlayed_MinutesAgo",
    "LOCPlaylist_LastPlayed_OneHourAgo",
    "LOCPlaylist_LastPlayed_HoursAgo",
    "LOCPlaylist_LastPlayed_OneDayAgo",
    "LOCPlaylist_LastPlayed_DaysAgo",
    "LOCPlaylist_LastPlayed_OneWeekAgo",
    "LOCPlaylist_LastPlayed_WeeksAgo",
    "LOCPlaylist_LastPlayed_OneMonthAgo",
    "LOCPlaylist_LastPlayed_MonthsAgo",
    "LOCPlaylist_LastPlayed_OneYearAgo",
    "LOCPlaylist_LastPlayed_LongAgo",
    "LOCPlaylist_Playtime_Minutes",
    "LOCPlaylist_Playtime_HoursMinutes",
    "LOCPlaylist_Playtime_HoursOnly",
    "LOCPlaylist_Playtime_MinuteUnit",
    "LOCPlaylist_Hltb_EmptyTime",
    "LOCPlaylist_Hltb_SortSuffix_Active",
    "LOCPlaylist_Hltb_SortSuffix_Hover"
)

# Active sort suffix is often identical across locales: it wraps the already-localized time-type label in parentheses.
$englishBaselineExemptKeys = @(
    "LOCPlaylist_Hltb_SortSuffix_Active",
    "LOCPlaylist_Hltb_EmptyTime"
)

function Import-TranslationsFile {
    param(
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Translations file not found: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $byLocale = @{}
    foreach ($localeProperty in $raw.PSObject.Properties) {
        $keys = @{}
        foreach ($keyProperty in $localeProperty.Value.PSObject.Properties) {
            $keys[$keyProperty.Name] = [string]$keyProperty.Value
        }
        $byLocale[$localeProperty.Name] = $keys
    }

    return $byLocale
}

if (-not (Test-Path -LiteralPath $enUsPath)) {
    throw "English baseline required at $enUsPath"
}

$englishBaselines = @{}
foreach ($key in $playlistKeys) {
    $value = Get-LocValue -FilePath $enUsPath -Key $key -Utf8 $utf8
    if (-not $value) {
        throw "Missing en_US baseline key: $key"
    }
    $englishBaselines[$key] = $value
}

$translationsByLocale = Import-TranslationsFile -Path $translationsPath

$removedPlaylistKeys = @(
    "LOCPlaylist_Playtime_UnitSeparator"
)

$updatedLocaleCount = 0
$changedKeyCount = 0
$sortedCount = 0

$supplementalLocales = Get-SupplementalLocaleNames -LocalizationDir $playlistLocalizationDir

$localeFiles = Get-ChildItem -LiteralPath $playlistLocalizationDir -Filter "*.xaml" |
    Where-Object { $_.BaseName -ne "en_US" -and $supplementalLocales -notcontains $_.BaseName }

foreach ($localeFile in ($localeFiles | Sort-Object Name)) {
    $locale = $localeFile.BaseName
    $localeTranslations = $translationsByLocale[$locale]
    if (-not $localeTranslations) {
        throw "Missing translations for locale: $locale"
    }

    $localeChanged = $false
    foreach ($key in $playlistKeys) {
        if (-not $localeTranslations.ContainsKey($key)) {
            throw "Missing translation for $locale / $key"
        }

        $value = $localeTranslations[$key]
        if ($value -ceq $englishBaselines[$key] -and $englishBaselineExemptKeys -notcontains $key) {
            Write-Warning "Translation for $locale / $key matches English baseline; expected localized string."
        }

        if (Set-LocValue -FilePath $localeFile.FullName -Key $key -Value $value -Utf8 $utf8) {
            $localeChanged = $true
            $changedKeyCount++
        }
    }

    foreach ($removedKey in $removedPlaylistKeys) {
        if (Remove-LocKey -FilePath $localeFile.FullName -Key $removedKey -Utf8 $utf8) {
            $localeChanged = $true
        }
    }

    if (Sort-LocalizationFileByKey -FilePath $localeFile.FullName -Utf8 $utf8) {
        $sortedCount++
        $localeChanged = $true
    }

    if ($localeChanged) {
        $updatedLocaleCount++
    }

    Write-Host "Updated $($localeFile.Name)"
}

Write-Host "Playlist-owned locale sync complete. Locales touched: $updatedLocaleCount; key writes: $changedKeyCount; files re-sorted: $sortedCount."
