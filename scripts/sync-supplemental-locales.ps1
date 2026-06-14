#!/usr/bin/env pwsh

# Requires PowerShell 7+ (pwsh) for reliable UTF-8 JSON/locale handling.
# Generates supplemental Localization/*.xaml files from en_US + scripts/data/supplemental-locale-translations*.json.

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$playlistLocalizationDir = Join-Path $ProjectDir "Localization"
$translationsDir = Join-Path $PSScriptRoot "data"
$enUsPath = Join-Path $playlistLocalizationDir "en_US.xaml"
$markerPath = Join-Path $playlistLocalizationDir ".supplemental-locales"

function Import-SupplementalTranslationsFile {
    param(
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Supplemental translations file not found: $Path"
    }

    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $byLocale = [ordered]@{}
    foreach ($localeProperty in $raw.PSObject.Properties) {
        $keys = [ordered]@{}
        foreach ($keyProperty in $localeProperty.Value.PSObject.Properties) {
            $keys[$keyProperty.Name] = [string]$keyProperty.Value
        }
        $byLocale[$localeProperty.Name] = $keys
    }

    return $byLocale
}

function Import-AllSupplementalTranslations {
    $merged = [ordered]@{}
    $files = Get-ChildItem -LiteralPath $translationsDir -Filter "supplemental-locale-translations*.json" |
        Sort-Object Name

    foreach ($file in $files) {
        $fileLocales = Import-SupplementalTranslationsFile -Path $file.FullName
        foreach ($locale in $fileLocales.Keys) {
            if ($merged.Contains($locale)) {
                throw "Duplicate supplemental locale definition: $locale in $($file.Name)"
            }

            $merged[$locale] = $fileLocales[$locale]
        }
    }

    return $merged
}

if (-not (Test-Path -LiteralPath $enUsPath)) {
    throw "English baseline required at $enUsPath"
}

$englishEntries = Get-LocalizationEntries -Content ([System.IO.File]::ReadAllText($enUsPath, $utf8))
$translationsByLocale = Import-AllSupplementalTranslations

$writtenCount = 0
foreach ($locale in ($translationsByLocale.Keys | Sort-Object)) {
    $localeTranslations = $translationsByLocale[$locale]
    $merged = [ordered]@{}
    foreach ($entry in $englishEntries.Values) {
        $merged[$entry.Key] = [pscustomobject]@{
            Key = $entry.Key
            Attributes = $entry.Attributes
            Value = $entry.Value
        }
    }

    foreach ($key in $localeTranslations.Keys) {
        $value = $localeTranslations[$key]
        if ($merged.Contains($key)) {
            $entry = $merged[$key]
            $merged[$key] = [pscustomobject]@{
                Key = $key
                Attributes = $entry.Attributes
                Value = $value
            }
        }
        else {
            $merged[$key] = [pscustomobject]@{
                Key = $key
                Attributes = ''
                Value = $value
            }
        }
    }

    foreach ($entry in $englishEntries.Values) {
        if (-not $localeTranslations.Contains($entry.Key)) {
            throw "Missing supplemental translation for $locale / $($entry.Key)"
        }
    }

    $localePath = Join-Path $playlistLocalizationDir ($locale + ".xaml")
    if (-not (Test-Path -LiteralPath $localePath)) {
        Copy-Item -LiteralPath $enUsPath -Destination $localePath
    }

    Write-LocalizationFile -FilePath $localePath -Entries $merged -Utf8 $utf8
    $writtenCount++
    Write-Host "Wrote $locale.xaml"
}

$markerLines = @($translationsByLocale.Keys | Sort-Object)
[System.IO.File]::WriteAllLines($markerPath, $markerLines, $utf8)
Write-Host "Updated supplemental marker with $($markerLines.Count) locale(s)."
Write-Host "Supplemental locale sync complete. Files written: $writtenCount."
