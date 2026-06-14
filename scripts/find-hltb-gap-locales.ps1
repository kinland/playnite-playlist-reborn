#!/usr/bin/env pwsh

# Reports Playlist-owned HLTB time-type keys still present in Localization/*.xaml.
# Locales with no entries here rely on the HLTB plugin at runtime (or English baselines when it is absent).

$dir = Join-Path $PSScriptRoot "..\Localization"
$keys = @(
    "LOCPlaylist_HLTB_TimeType_MainStory",
    "LOCPlaylist_HLTB_TimeType_MainExtra",
    "LOCPlaylist_HLTB_TimeType_Completionist",
    "LOCPlaylist_HLTB_TimeType_Solo",
    "LOCPlaylist_HLTB_TimeType_CoOp",
    "LOCPlaylist_HLTB_TimeType_Versus"
)

$any = $false
Get-ChildItem -LiteralPath $dir -Filter "*.xaml" |
    Where-Object { $_.BaseName -ne "en_US" } |
    Sort-Object Name |
    ForEach-Object {
        $content = Get-Content -LiteralPath $_.FullName -Raw
        $present = @()
        foreach ($key in $keys) {
            if ($content -match [regex]::Escape($key)) {
                $present += $key -replace "LOCPlaylist_HLTB_TimeType_", ""
            }
        }
        if ($present.Count -gt 0) {
            $any = $true
            Write-Output "$($_.BaseName): $($present -join ', ')"
        }
    }

if (-not $any) {
    Write-Output "No Playlist-owned HLTB time-type keys in non-en_US locales."
}
