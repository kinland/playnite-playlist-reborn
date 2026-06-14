# Sweeps PlayniteSDK versions and keeps Installer_Manifest RequiredApiVersion in sync.
# Restores all files to their pre-run content when finished.
#
# Re-run before the SDK/manifest commit (after other review fixes land). Default sweep
# starts at 6.5.0 (known minimum on 2026-06-13 HEAD); pass -Versions to probe lower.
param(
    [string[]] $Versions = @(
        "6.5.0", "6.6.0", "6.7.0", "6.8.0", "6.9.0", "6.10.0",
        "6.11.0", "6.12.0", "6.13.0", "6.14.0", "6.15.0", "6.16.0"
    )
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$mainCsproj = "Playlist.csproj"
$testCsproj = "tests/Playlist.UnitTests/Playlist.UnitTests.csproj"
$manifest = "Installer_Manifest.yaml"

$mainOrig = Get-Content $mainCsproj -Raw
$testOrig = Get-Content $testCsproj -Raw
$manifestOrig = Get-Content $manifest -Raw

function Set-SdkVersion {
    param([string] $Version)
    ($mainOrig -replace 'PlayniteSDK" Version="[^"]+"', "PlayniteSDK`" Version=`"$Version`"") | Set-Content $mainCsproj -NoNewline
    ($testOrig -replace 'PlayniteSDK" Version="[^"]+"', "PlayniteSDK`" Version=`"$Version`"") | Set-Content $testCsproj -NoNewline
    ($manifestOrig -replace '(?m)^(\s*RequiredApiVersion:\s*).+$', "`${1}$Version") | Set-Content $manifest -NoNewline
}

try {
    $results = @()
    foreach ($v in $Versions) {
        Set-SdkVersion -Version $v
        dotnet build Playlist.sln -c Release --nologo -v q 2>$null
        $ok = ($LASTEXITCODE -eq 0)
        $results += [pscustomobject]@{ Version = $v; Build = $(if ($ok) { "OK" } else { "FAIL" }) }
        Write-Output ("{0}: {1}" -f $v, $(if ($ok) { "OK" } else { "FAIL" }))
    }

    Write-Output "---"
    $firstOk = ($results | Where-Object { $_.Build -eq "OK" } | Select-Object -First 1).Version
    if ($firstOk) {
        Write-Output "Minimum OK: $firstOk"
    } else {
        Write-Output "Minimum OK: (none in sweep)"
    }
}
finally {
    $mainOrig | Set-Content $mainCsproj -NoNewline
    $testOrig | Set-Content $testCsproj -NoNewline
    $manifestOrig | Set-Content $manifest -NoNewline
    Write-Output "Restored Playlist.csproj, Playlist.UnitTests.csproj, Installer_Manifest.yaml"
}
