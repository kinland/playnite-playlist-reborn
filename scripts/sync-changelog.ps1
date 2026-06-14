#!/usr/bin/env pwsh

# Regenerates CHANGELOG.md from Installer_Manifest.yaml history and optional unreleased commit candidates.
param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$DocumentedThroughVersion,
    [string]$ProcessedThroughCommit,
    [switch]$IncludeCommitCandidates,
    [switch]$BootstrapOnly
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "Changelog.ps1")

if (-not $DocumentedThroughVersion) {
    $DocumentedThroughVersion = Get-ExtensionVersion -ProjectDir $ProjectDir
}

$includeCommits = -not $BootstrapOnly
$params = @{
    ProjectDir = $ProjectDir
    DocumentedThroughVersion = $DocumentedThroughVersion
}
if ($IncludeCommitCandidates -or $includeCommits) {
    $params.IncludeCommitCandidates = $true
}
if ($ProcessedThroughCommit) {
    $params.ProcessedThroughCommit = $ProcessedThroughCommit
}
elseif ($BootstrapOnly) {
    $bumpCommit = Get-LastVersionBumpCommit -ProjectDir $ProjectDir
    if ($bumpCommit) {
        $params.ProcessedThroughCommit = $bumpCommit
    }
}

Sync-ChangelogMarkdown @params
