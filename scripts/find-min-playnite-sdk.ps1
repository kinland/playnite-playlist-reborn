# Sweeps PlayniteSDK versions and keeps all SDK version surfaces in sync during the probe.
# Restores every touched file to its pre-run content when finished.
#
# Maintained surfaces (when present):
#   - Any repo *.csproj with PackageReference Include="PlayniteSDK" (Playlist, UnitTests, UiTests, …)
#   - Installer_Manifest.yaml RequiredApiVersion
#   - Installer_Manifest.yaml changelog "Requires Playnite API … or newer"
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

$manifest = "Installer_Manifest.yaml"
$sdkCsprojPattern = 'PackageReference Include="PlayniteSDK"'

function Get-SdkCsprojPaths {
    Get-ChildItem -Path $repoRoot -Recurse -Filter *.csproj -File |
        Where-Object {
            $_.FullName -notlike "*\.tools\*" -and
            (Get-Content -LiteralPath $_.FullName -Raw) -match $sdkCsprojPattern
        } |
        ForEach-Object {
            $_.FullName.Substring($repoRoot.Length + 1) -replace '\\', '/'
        } |
        Sort-Object
}

$sdkCsprojPaths = @(Get-SdkCsprojPaths)
if ($sdkCsprojPaths.Count -eq 0) {
    throw "No csproj files with PlayniteSDK PackageReference found under $repoRoot"
}

$originalContents = @{}
foreach ($path in $sdkCsprojPaths) {
    $originalContents[$path] = Get-Content -LiteralPath $path -Raw
}
$originalContents[$manifest] = Get-Content -LiteralPath $manifest -Raw

Write-Output ("PlayniteSDK surfaces: {0}, {1}" -f ($sdkCsprojPaths -join ", "), $manifest)

function Set-SdkVersion {
    param([string] $Version)

    foreach ($path in $sdkCsprojPaths) {
        $content = $originalContents[$path] -replace 'PlayniteSDK" Version="[^"]+"', "PlayniteSDK`" Version=`"$Version`""
        Set-Content -LiteralPath $path -Value $content -NoNewline
    }

    $manifestContent = $originalContents[$manifest]
    $manifestContent = $manifestContent -replace '(?m)^(\s*RequiredApiVersion:\s*).+$', "`${1}$Version"
    $manifestContent = $manifestContent -replace 'Requires Playnite API [0-9.]+ or newer', "Requires Playnite API $Version or newer"
    Set-Content -LiteralPath $manifest -Value $manifestContent -NoNewline
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
    foreach ($path in ($sdkCsprojPaths + @($manifest))) {
        Set-Content -LiteralPath $path -Value $originalContents[$path] -NoNewline
    }
    Write-Output ("Restored {0}" -f (($sdkCsprojPaths + @($manifest)) -join ", "))
}
