#!/usr/bin/env pwsh

# Requires PowerShell 7+ (pwsh) for reliable UTF-8 JSON/locale handling.
# Localization/*.xaml is the source of truth for Playlist-owned keys in Playnite locales.
# Adds missing keys from en_US, removes retired keys, and sorts each locale file.

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$playlistLocalizationDir = Join-Path $ProjectDir "Localization"
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
    "LOCPlaylist_HLTB_EmptyTime",
    "LOCPlaylist_HLTB_SortSuffix_Active",
    "LOCPlaylist_HLTB_SortSuffix_Hover"
)

$englishBaselineExemptKeys = @(
    "LOCPlaylist_HLTB_SortSuffix_Active",
    "LOCPlaylist_HLTB_EmptyTime"
)

$removedPlaylistKeys = @(
    "LOCPlaylist_Playtime_UnitSeparator"
)

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

$updatedLocaleCount = 0
$changedKeyCount = 0
$sortedCount = 0

$supplementalLocales = Get-SupplementalLocaleNames -LocalizationDir $playlistLocalizationDir

$localeFiles = Get-ChildItem -LiteralPath $playlistLocalizationDir -Filter "*.xaml" |
    Where-Object { $_.BaseName -ne "en_US" -and $supplementalLocales -notcontains $_.BaseName }

foreach ($localeFile in ($localeFiles | Sort-Object Name)) {
    $locale = $localeFile.BaseName
    $localeChanged = $false

    foreach ($key in $playlistKeys) {
        $value = Get-LocValue -FilePath $localeFile.FullName -Key $key -Utf8 $utf8
        if (-not $value) {
            $value = $englishBaselines[$key]
            if (Set-LocValue -FilePath $localeFile.FullName -Key $key -Value $value -Utf8 $utf8) {
                $localeChanged = $true
                $changedKeyCount++
            }
            continue
        }

        if ($value -ceq $englishBaselines[$key] -and $englishBaselineExemptKeys -notcontains $key) {
            Write-Warning "Translation for $locale / $key matches English baseline; expected localized string."
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

    Write-Host "Checked $($localeFile.Name)"
}

Write-Host "Playlist-owned locale sync complete. Locales touched: $updatedLocaleCount; keys added: $changedKeyCount; files re-sorted: $sortedCount."

. (Join-Path $PSScriptRoot "Changelog.ps1")
Register-SyncChangelogOperation -OperationId "sync-playlist-owned-locales" -ProjectDir $ProjectDir
