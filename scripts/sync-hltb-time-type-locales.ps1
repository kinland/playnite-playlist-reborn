#!/usr/bin/env pwsh

# Requires PowerShell 7+ (pwsh) for reliable UTF-8 JSON/locale handling.
# Localization/*.xaml is the checked-in source of truth for Playlist-owned strings.
# scripts/data/hltb-time-type-gap-overrides.json is a local cache (gitignored):
# generated from xaml when missing, updated after each sync to speed re-runs.

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$HltbLocalizationDir = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..\..\playnite-howlongtobeat-plugin")).Path "source\Localization")
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)

$playlistLocalizationDir = Join-Path $ProjectDir "Localization"
$gapOverridesPath = Join-Path $PSScriptRoot "data\hltb-time-type-gap-overrides.json"

$keyMappingDefinitions = @(
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_MainStory"; HltbKey = "LOCHowLongToBeatMainStory" },
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_MainExtra"; HltbKey = "LOCHowLongToBeatMainExtra" },
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_Completionist"; HltbKey = "LOCHowLongToBeatCompletionist" },
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_Solo"; HltbKey = "LOCHowLongToBeatSolo" },
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_CoOp"; HltbKey = "LOCHowLongToBeatCoOp" },
    @{ PlaylistKey = "LOCPlaylist_Hltb_TimeType_Versus"; HltbKey = "LOCHowLongToBeatVs" }
)

function Get-LocValue {
    param(
        [string]$FilePath,
        [string]$Key
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $null
    }

    $content = [System.IO.File]::ReadAllText($FilePath, $utf8)
    $pattern = 'x:Key="' + [regex]::Escape($Key) + '">([^<]+)</'
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) {
        return $match.Groups[1].Value
    }

    return $null
}

function Set-LocValue {
    param(
        [string]$FilePath,
        [string]$Key,
        [string]$Value
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $utf8)
    $escapedKey = [regex]::Escape($Key)
    $pattern = '(x:Key="' + $escapedKey + '">)([^<]+)(</)'
    $match = [regex]::Match($content, $pattern)
    if ($match.Success) {
        if ($match.Groups[2].Value -ceq $Value) {
            return
        }

        $replacement = '${1}' + $Value + '${3}'
        $updated = [regex]::Replace($content, $pattern, $replacement, 1)
        [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
        return
    }

    $insertPattern = '(<sys:String x:Key="LOCPlaylist_Hltb_SortSuffix_Active">)'
    $insertMatch = [regex]::Match($content, $insertPattern)
    if (-not $insertMatch.Success) {
        throw "Unable to insert $Key in $FilePath; sort suffix anchor is missing."
    }

    $line = '    <sys:String x:Key="' + $Key + '">' + $Value + '</sys:String>' + [Environment]::NewLine
    $updated = $content.Insert($insertMatch.Index, $line)
    [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
}

function Remove-LocKey {
    param(
        [string]$FilePath,
        [string]$Key
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $utf8)
    $escapedKey = [regex]::Escape($Key)
    $linePattern = '\r?\n[ \t]*<sys:String x:Key="' + $escapedKey + '">[^<]+</sys:String>'
    $updated = [regex]::Replace($content, $linePattern, [string]::Empty, 1)
    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
        return $true
    }

    $inlinePattern = '(</sys:String>)[ \t]*<sys:String x:Key="' + $escapedKey + '">[^<]+</sys:String>'
    $updated = [regex]::Replace($content, $inlinePattern, '$1', 1)
    if ($updated -eq $content) {
        return $false
    }

    [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
    return $true
}

function Repair-LocalizationFileNewlines {
    param(
        [string]$FilePath
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $utf8)
    $updated = [regex]::Replace(
        $content,
        '(</sys:String>)[ \t]+(<sys:String)',
        ('$1' + [Environment]::NewLine + '    $2'))
    if ($updated -eq $content) {
        return $false
    }

    [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
    return $true
}

function Test-HltbProvidesTranslation {
    param(
        [string]$HltbValue,
        [string]$EnglishBaseline
    )

    return [bool]($HltbValue -and ($hltbValue -cne $EnglishBaseline))
}

function Test-HltbCoversPlaylistKey {
    param(
        [string]$Locale,
        [string]$HltbValue,
        [string]$EnglishBaseline
    )

    if (Test-HltbProvidesTranslation -HltbValue $HltbValue -EnglishBaseline $EnglishBaseline) {
        return $true
    }

    return [bool]($Locale -eq "en_US" -and $HltbValue)
}

function Write-GapOverridesFile {
    param(
        [string]$Path,
        [hashtable]$GapOverridesByLocale
    )

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $payload = [ordered]@{}
    foreach ($locale in ($GapOverridesByLocale.Keys | Sort-Object)) {
        $localePayload = [ordered]@{}
        foreach ($playlistKey in ($GapOverridesByLocale[$locale].Keys | Sort-Object)) {
            $localePayload[$playlistKey] = $GapOverridesByLocale[$locale][$playlistKey]
        }

        $payload[$locale] = $localePayload
    }

    $json = if ($payload.Count -eq 0) { "{}" } else { $payload | ConvertTo-Json -Depth 5 }
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, $utf8)
}

function Import-GapOverridesFile {
    param(
        [string]$Path
    )

    $gapOverridesByLocale = @{}
    if (-not (Test-Path -LiteralPath $Path)) {
        return $gapOverridesByLocale
    }

    $gapOverrides = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($localeProperty in $gapOverrides.PSObject.Properties) {
        $localeGap = @{}
        foreach ($keyProperty in $localeProperty.Value.PSObject.Properties) {
            $localeGap[$keyProperty.Name] = $keyProperty.Value
        }
        $gapOverridesByLocale[$localeProperty.Name] = $localeGap
    }

    return $gapOverridesByLocale
}

function Build-GapOverridesFromLocalization {
    param(
        [string]$LocalizationDir,
        [array]$KeyMappings,
        [string]$HltbLocalizationDir
    )

    $gapOverridesByLocale = @{}
    $localeFiles = Get-ChildItem -LiteralPath $LocalizationDir -Filter "*.xaml"
    foreach ($localeFile in $localeFiles) {
        $locale = $localeFile.BaseName
        $hltbLocalePath = Join-Path $HltbLocalizationDir ($locale + ".xaml")

        foreach ($mapping in $KeyMappings) {
            $hltbValue = Get-LocValue -FilePath $hltbLocalePath -Key $mapping.HltbKey
            if (Test-HltbCoversPlaylistKey -Locale $locale -HltbValue $hltbValue -EnglishBaseline $mapping.EnglishBaseline) {
                continue
            }

            $playlistValue = Get-LocValue -FilePath $localeFile.FullName -Key $mapping.PlaylistKey
            if (-not $playlistValue) {
                continue
            }

            if (-not $gapOverridesByLocale.ContainsKey($locale)) {
                $gapOverridesByLocale[$locale] = @{}
            }

            $gapOverridesByLocale[$locale][$mapping.PlaylistKey] = $playlistValue
        }
    }

    return $gapOverridesByLocale
}

$hltbEnUsPath = Join-Path $HltbLocalizationDir "en_US.xaml"
if (-not (Test-Path -LiteralPath $hltbEnUsPath)) {
    throw "HLTB en_US localization is required at $hltbEnUsPath"
}

$keyMappings = @(
    foreach ($definition in $keyMappingDefinitions) {
        $englishBaseline = Get-LocValue -FilePath $hltbEnUsPath -Key $definition.HltbKey
        if (-not $englishBaseline) {
            throw "Missing HLTB en_US key: $($definition.HltbKey)"
        }

        @{
            PlaylistKey = $definition.PlaylistKey
            HltbKey = $definition.HltbKey
            EnglishBaseline = $englishBaseline
        }
    }
)

if (Test-Path -LiteralPath $gapOverridesPath) {
    $gapOverridesByLocale = Import-GapOverridesFile -Path $gapOverridesPath
    Write-Host "Loaded local gap overrides from $gapOverridesPath"
}
else {
    $gapOverridesByLocale = Build-GapOverridesFromLocalization `
        -LocalizationDir $playlistLocalizationDir `
        -KeyMappings $keyMappings `
        -HltbLocalizationDir $HltbLocalizationDir
    Write-GapOverridesFile -Path $gapOverridesPath -GapOverridesByLocale $gapOverridesByLocale
    Write-Host "Generated local gap overrides from Localization/*.xaml at $gapOverridesPath"
}

$originalOverrideCount = ($gapOverridesByLocale.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum

$retainedGapOverridesByLocale = @{}
$removedPlaylistKeyCount = 0
$localeFiles = Get-ChildItem -LiteralPath $playlistLocalizationDir -Filter "*.xaml"
foreach ($localeFile in $localeFiles) {
    $locale = $localeFile.BaseName
    $hltbLocalePath = Join-Path $HltbLocalizationDir ($locale + ".xaml")
    $localeGaps = $gapOverridesByLocale[$locale]

    foreach ($mapping in $keyMappings) {
        $hltbValue = Get-LocValue -FilePath $hltbLocalePath -Key $mapping.HltbKey
        if (Test-HltbCoversPlaylistKey -Locale $locale -HltbValue $hltbValue -EnglishBaseline $mapping.EnglishBaseline) {
            if (Remove-LocKey -FilePath $localeFile.FullName -Key $mapping.PlaylistKey) {
                $removedPlaylistKeyCount++
            }

            continue
        }

        if ($localeGaps -and $localeGaps.ContainsKey($mapping.PlaylistKey)) {
            $resolved = $localeGaps[$mapping.PlaylistKey]
        }
        else {
            $resolved = Get-LocValue -FilePath $localeFile.FullName -Key $mapping.PlaylistKey
        }

        if ($resolved) {
            if (-not $retainedGapOverridesByLocale.ContainsKey($locale)) {
                $retainedGapOverridesByLocale[$locale] = @{}
            }

            $retainedGapOverridesByLocale[$locale][$mapping.PlaylistKey] = $resolved
            Set-LocValue -FilePath $localeFile.FullName -Key $mapping.PlaylistKey -Value $resolved
            continue
        }

        if (Remove-LocKey -FilePath $localeFile.FullName -Key $mapping.PlaylistKey) {
            $removedPlaylistKeyCount++
        }

        throw "Missing HLTB time-type translation for $locale / $($mapping.PlaylistKey)"
    }

    if (Repair-LocalizationFileNewlines -FilePath $localeFile.FullName) {
        Write-Host "Repaired newline layout in $($localeFile.Name)"
    }

    Write-Host "Updated $($localeFile.Name)"
}

Write-GapOverridesFile -Path $gapOverridesPath -GapOverridesByLocale $retainedGapOverridesByLocale

$retainedOverrideCount = ($retainedGapOverridesByLocale.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
$prunedOverrideCount = $originalOverrideCount - $retainedOverrideCount
Write-Host "Removed $removedPlaylistKeyCount HLTB-covered Playlist key(s) from locale files."
Write-Host "Pruned $prunedOverrideCount redundant gap override(s); retained $retainedOverrideCount."
Write-Host "HLTB time-type locale sync complete."
