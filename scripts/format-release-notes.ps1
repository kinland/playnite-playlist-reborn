#!/usr/bin/env pwsh

# Writes GitHub release notes from Installer_Manifest.yaml for the current extension version.
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OutputPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "release-notes.md")
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Changelog.ps1")

$markdown = Get-InstallerManifestReleaseNotesMarkdown -Version $Version -ProjectDir $ProjectDir
Set-Content -LiteralPath $OutputPath -Value $markdown -Encoding UTF8 -NoNewline
Add-Content -LiteralPath $OutputPath -Value "" -Encoding UTF8 -NoNewline:$false
Write-Host "Wrote $OutputPath"
