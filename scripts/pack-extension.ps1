# Packs the built Playlist extension with Playnite Toolbox.exe.
# Toolbox strips conflicting dependencies; do not use Compress-Archive for release artifacts.
param(
    [Parameter(Mandatory = $true)]
    [string] $ExtensionDir,
    [Parameter(Mandatory = $true)]
    [string] $OutputDir,
    [string] $ProjectDir = (Split-Path -Parent $PSScriptRoot),
    [string] $ToolboxPath = $env:PLAYNITE_TOOLBOX
)

$ErrorActionPreference = "Stop"

function Resolve-ToolboxPath {
    param([string] $ExplicitPath)
    if ($ExplicitPath -and (Test-Path -LiteralPath $ExplicitPath)) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = @(
        $env:PLAYNITE_TOOLBOX,
        (Join-Path $ProjectDir ".tools\Playnite\Toolbox.exe"),
        "${env:ProgramFiles}\Playnite\Toolbox.exe",
        "${env:ProgramFiles(x86)}\Playnite\Toolbox.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    if (@($candidates).Count -gt 0) {
        return (Resolve-Path -LiteralPath @($candidates)[0]).Path
    }

    return $null
}

function Ensure-Toolbox {
    param([string] $ProjectRoot)
    $toolsDir = Join-Path $ProjectRoot ".tools"
    $playniteDir = Join-Path $toolsDir "Playnite"
    $toolbox = Join-Path $playniteDir "Toolbox.exe"
    if (Test-Path -LiteralPath $toolbox) {
        return (Resolve-Path -LiteralPath $toolbox).Path
    }

    if (Test-Path -LiteralPath $playniteDir) {
        Remove-Item -LiteralPath $playniteDir -Recurse -Force
    }

    $downloadUrl = "https://github.com/JosefNemec/Playnite/releases/download/10.32/Playnite1032.zip"
    $zipPath = Join-Path $toolsDir "Playnite1032.zip"
    New-Item -ItemType Directory -Force -Path $toolsDir | Out-Null
    Write-Host "Downloading Playnite Toolbox from $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $playniteDir)
    Remove-Item -LiteralPath $zipPath -Force
    if (-not (Test-Path -LiteralPath $toolbox)) {
        throw "Toolbox.exe not found after extracting Playnite to $playniteDir"
    }

    return (Resolve-Path -LiteralPath $toolbox).Path
}

$extensionDir = (Resolve-Path -LiteralPath $ExtensionDir).Path
$outputDir = (Resolve-Path -LiteralPath $OutputDir).Path
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

foreach ($file in @("README.md", "LICENSE")) {
    $source = Join-Path $ProjectDir $file
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $extensionDir $file) -Force
    }
}

$hltbThirdPartySource = Join-Path $ProjectDir "third_party\howlongtobeat"
$hltbThirdPartyDest = Join-Path $extensionDir "third_party\howlongtobeat"
if (Test-Path -LiteralPath $hltbThirdPartySource) {
    New-Item -ItemType Directory -Force -Path $hltbThirdPartyDest | Out-Null
    foreach ($file in @("LICENSE", "NOTICE.md")) {
        $source = Join-Path $hltbThirdPartySource $file
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $hltbThirdPartyDest $file) -Force
        }
    }
}

$localizationSource = Join-Path $ProjectDir "Localization"
$localizationDest = Join-Path $extensionDir "Localization"
if (Test-Path -LiteralPath $localizationSource) {
    New-Item -ItemType Directory -Force -Path $localizationDest | Out-Null
    Copy-Item -LiteralPath (Join-Path $localizationSource "*.xaml") -Destination $localizationDest -Force
}

$toolboxExe = Resolve-ToolboxPath -ExplicitPath $ToolboxPath
if (-not $toolboxExe) {
    $toolboxExe = Ensure-Toolbox -ProjectRoot $ProjectDir
}

Write-Host "Packing $extensionDir -> $outputDir via $toolboxExe"
& $toolboxExe pack $extensionDir $outputDir
if ($LASTEXITCODE -ne 0) {
    throw "Toolbox pack failed with exit code $LASTEXITCODE"
}

$packed = Get-ChildItem -LiteralPath $outputDir -Filter "*.pext" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $packed) {
    throw "Toolbox did not produce a .pext file in $outputDir"
}

$stableName = Join-Path $outputDir "Playlist.pext"
Copy-Item -LiteralPath $packed.FullName -Destination $stableName -Force

Write-Host "Packed $($packed.FullName)"
Write-Host "Copied stable artifact to $stableName"
