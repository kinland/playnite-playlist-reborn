#!/usr/bin/env pwsh

# Requires PowerShell 7+ (pwsh) for reliable UTF-8 JSON/locale handling.
# Localization/*.xaml is the source of truth for supplemental locales.
# Adds any keys present in en_US but missing from a supplemental file (English fallback), then sorts keys.

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$playlistLocalizationDir = Join-Path $ProjectDir "Localization"
$enUsPath = Join-Path $playlistLocalizationDir "en_US.xaml"
$markerPath = Join-Path $playlistLocalizationDir ".supplemental-locales"

if (-not (Test-Path -LiteralPath $enUsPath)) {
    throw "English baseline required at $enUsPath"
}

if (-not (Test-Path -LiteralPath $markerPath)) {
    throw "Supplemental locale marker not found: $markerPath"
}

$englishEntries = Get-LocalizationEntries -Content ([System.IO.File]::ReadAllText($enUsPath, $utf8))
$supplementalLocales = @(Get-Content -LiteralPath $markerPath -Encoding UTF8 |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_.Length -gt 0 } |
    Sort-Object -Unique)

if ($supplementalLocales.Count -eq 0) {
    throw "Supplemental locale marker is empty: $markerPath"
}

$localesTouched = 0
$keysAdded = 0
$sortedCount = 0

foreach ($locale in $supplementalLocales) {
    $localePath = Join-Path $playlistLocalizationDir ($locale + ".xaml")
    if (-not (Test-Path -LiteralPath $localePath)) {
        Copy-Item -LiteralPath $enUsPath -Destination $localePath
        $localesTouched++
        Write-Host "Created $locale.xaml from en_US baseline"
    }

    $localeChanged = $false
    foreach ($entry in $englishEntries.Values) {
        $currentValue = Get-LocValue -FilePath $localePath -Key $entry.Key -Utf8 $utf8
        if ($currentValue) {
            continue
        }

        if (Set-LocValue -FilePath $localePath -Key $entry.Key -Value $entry.Value -Utf8 $utf8) {
            $localeChanged = $true
            $keysAdded++
        }
    }

    if (Sort-LocalizationFileByKey -FilePath $localePath -Utf8 $utf8) {
        $sortedCount++
        $localeChanged = $true
    }

    if ($localeChanged) {
        $localesTouched++
    }

    Write-Host "Checked $locale.xaml"
}

[System.IO.File]::WriteAllLines($markerPath, $supplementalLocales, $utf8)
Write-Host "Supplemental locale sync complete. Locales touched: $localesTouched; keys added: $keysAdded; files re-sorted: $sortedCount."

. (Join-Path $PSScriptRoot "Changelog.ps1")
Register-SyncChangelogOperation -OperationId "sync-supplemental-locales" -ProjectDir $ProjectDir
