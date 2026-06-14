#!/usr/bin/env pwsh

# Bumps extension version surfaces and finalizes Installer_Manifest + CHANGELOG.md for a release.
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$ReleaseDate = (Get-Date -Format "yyyy-MM-dd"),
    [switch]$PromoteUnreleasedOnly
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Changelog.ps1")

$params = @{
    Version = $Version
    ReleaseDate = $ReleaseDate
    ProjectDir = $ProjectDir
}
if ($PromoteUnreleasedOnly) {
    $params.PromoteUnreleasedOnly = $true
}

Invoke-ProjectVersionBump @params
