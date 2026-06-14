#!/usr/bin/env pwsh

# Adds Playnite-borrowed localization keys to supplemental locale xaml files.
# Override locales resolve these before Playnite's ResourceProvider chain.

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$localizationDir = Join-Path $ProjectDir "Localization"
$baselinesPath = Join-Path $PSScriptRoot "data/playlist-borrowed-playnite-locale-baselines.json"
$translationsPath = Join-Path $PSScriptRoot "data/playlist-borrowed-playnite-locale-translations.json"

$baselines = Get-Content -LiteralPath $baselinesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$translations = Get-Content -LiteralPath $translationsPath -Raw -Encoding UTF8 | ConvertFrom-Json
$supplementalLocales = Get-SupplementalLocaleNames -LocalizationDir $localizationDir

$deprecatedHltbPluginKeys = @(
    "LOCHowLongToBeat",
    "LOCHowLongToBeatMainStory",
    "LOCHowLongToBeatMainExtra",
    "LOCHowLongToBeatCompletionist",
    "LOCHowLongToBeatSolo",
    "LOCHowLongToBeatCoOp",
    "LOCHowLongToBeatVs"
)

$keysAdded = 0
$keysRemoved = 0
$localesTouched = 0

foreach ($locale in $supplementalLocales) {
    $localePath = Join-Path $localizationDir ($locale + ".xaml")
    if (-not (Test-Path -LiteralPath $localePath)) {
        throw "Missing supplemental locale file: $localePath"
    }

    $localeChanged = $false
    $localeTranslations = $translations.$locale

    foreach ($deprecatedKey in $deprecatedHltbPluginKeys) {
        if (Remove-LocKey -FilePath $localePath -Key $deprecatedKey -Utf8 $utf8) {
            $localeChanged = $true
            $keysRemoved++
        }
    }

    foreach ($key in $baselines.PSObject.Properties.Name) {
        $value = $baselines.$key
        if ($localeTranslations -and $localeTranslations.$key) {
            $value = $localeTranslations.$key
        }

        if (Set-LocValue -FilePath $localePath -Key $key -Value $value -Utf8 $utf8) {
            $localeChanged = $true
            $keysAdded++
        }
    }

    if ($localeTranslations) {
        foreach ($key in $localeTranslations.PSObject.Properties.Name) {
            if ($baselines.PSObject.Properties.Name -contains $key) {
                continue
            }

            if (-not $key.StartsWith("LOCPlaylist_")) {
                continue
            }

            if (Set-LocValue -FilePath $localePath -Key $key -Value $localeTranslations.$key -Utf8 $utf8) {
                $localeChanged = $true
                $keysAdded++
            }
        }
    }

    if (Sort-LocalizationFileByKey -FilePath $localePath -Utf8 $utf8) {
        $localeChanged = $true
    }

    if ($localeChanged) {
        $localesTouched++
    }

    Write-Host "Synced borrowed Playnite keys for $locale"
}

Write-Host "Borrowed Playnite locale sync complete. Locales touched: $localesTouched; keys added/updated: $keysAdded; deprecated HLTB plugin keys removed: $keysRemoved."
