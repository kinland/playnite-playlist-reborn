#!/usr/bin/env pwsh

# Appends deduplicated commit summaries to the current Installer_Manifest package and
# regenerates CHANGELOG.md [Unreleased] from all commits since the last version bump.
param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$DocumentedThroughVersion,
    [string]$ProcessedThroughCommit,
    [switch]$IncludeCommitCandidates,
    [switch]$SkipManifestAppend,
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
if ($SkipManifestAppend -or $BootstrapOnly) {
    $params.SkipManifestAppend = $true
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

$null = Sync-ChangelogMarkdown @params
