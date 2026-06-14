#!/usr/bin/env pwsh

param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$utf8 = New-Object System.Text.UTF8Encoding($false)
. (Join-Path $PSScriptRoot "LocalizationXaml.ps1")

$localizationDir = Join-Path $ProjectDir "Localization"
$sortedCount = 0

Get-ChildItem -LiteralPath $localizationDir -Filter "*.xaml" |
    Sort-Object Name |
    ForEach-Object {
        if (Sort-LocalizationFileByKey -FilePath $_.FullName -Utf8 $utf8) {
            $sortedCount++
        }

        Write-Host "Sorted $($_.Name)"
    }

Write-Host "Localization sort complete. Files reordered: $sortedCount."

. (Join-Path $PSScriptRoot "Changelog.ps1")
Register-SyncChangelogOperation -OperationId "sort-localization-files" -ProjectDir $ProjectDir
