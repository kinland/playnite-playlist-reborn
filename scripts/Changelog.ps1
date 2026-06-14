# Changelog and release-version helpers for Playlist.
# Dot-source from scripts/sync-changelog.ps1, scripts/bump-version.ps1, and locale sync scripts.

$ErrorActionPreference = "Stop"

function Get-ChangelogProjectRoot {
    param([string]$StartDir = $PSScriptRoot)
    return (Resolve-Path (Join-Path $StartDir "..")).Path
}

function Get-ChangelogMarkdownPath {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    return (Join-Path $ProjectDir "CHANGELOG.md")
}

function Get-ChangelogProcessedThroughCommit {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    if ($state.lastSyncProcessedCommit) {
        return [string]$state.lastSyncProcessedCommit
    }

    # Backward compatibility for state files written before lastSyncProcessedCommit existed.
    if ($state.lastVersionBumpCommit) {
        return [string]$state.lastVersionBumpCommit
    }

    return Get-LastVersionBumpCommit -ProjectDir $ProjectDir
}

function Save-ChangelogProcessedThroughCommit {
    param(
        [string]$CommitHash,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    if ([string]::IsNullOrWhiteSpace($CommitHash)) {
        return
    }

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $state | Add-Member -NotePropertyName lastSyncProcessedCommit -NotePropertyValue $CommitHash.Trim() -Force
    if ($state.PSObject.Properties['lastVersionBumpCommit']) {
        $state.lastVersionBumpCommit = $null
    }
    Save-ChangelogState -State $state -ProjectDir $ProjectDir
}

function Clear-ChangelogSyncWatermark {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    if ($state.PSObject.Properties['lastSyncProcessedCommit']) {
        $state.lastSyncProcessedCommit = $null
    }
    if ($state.PSObject.Properties['lastVersionBumpCommit']) {
        $state.lastVersionBumpCommit = $null
    }
    Save-ChangelogState -State $state -ProjectDir $ProjectDir
}

function Get-ChangelogUnreleasedEntriesFromFile {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    $path = Get-ChangelogMarkdownPath -ProjectDir $ProjectDir
    if (-not (Test-Path -LiteralPath $path)) {
        return @()
    }

    $lines = Get-Content -LiteralPath $path -Encoding UTF8
    $entries = [System.Collections.Generic.List[string]]::new()
    $inUnreleased = $false
    foreach ($line in $lines) {
        if ($line -match '^## \[Unreleased\]') {
            $inUnreleased = $true
            continue
        }
        if ($inUnreleased -and $line -match '^## \[') {
            break
        }
        if (-not $inUnreleased) {
            continue
        }
        if ($line -match '^_No unreleased changes recorded yet\._$') {
            continue
        }
        if ($line -match '^- (.+)$') {
            $null = $entries.Add((ConvertTo-NormalizedChangelogText $Matches[1]))
        }
    }

    return @($entries)
}

function Get-ChangelogReleasedSectionsFromFile {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    $path = Get-ChangelogMarkdownPath -ProjectDir $ProjectDir
    if (-not (Test-Path -LiteralPath $path)) {
        return @{}
    }

    $lines = Get-Content -LiteralPath $path -Encoding UTF8
    $sections = @{}
    $currentVersion = $null
    $entries = $null

    foreach ($line in $lines) {
        if ($line -match '^## \[([^\]]+)\]') {
            if ($currentVersion -and $entries -and $entries.Count -gt 0) {
                $sections[$currentVersion] = @($entries)
            }
            $headerVersion = $Matches[1]
            if ($headerVersion -eq 'Unreleased') {
                $currentVersion = $null
                $entries = $null
                continue
            }
            $currentVersion = $headerVersion
            $entries = [System.Collections.Generic.List[string]]::new()
            continue
        }
        if (-not $currentVersion) {
            continue
        }
        if ($line -match '^(\s*)- ') {
            $null = $entries.Add($line)
        }
    }

    if ($currentVersion -and $entries -and $entries.Count -gt 0) {
        $sections[$currentVersion] = @($entries)
    }

    return $sections
}

function Resolve-ChangelogProcessedThroughCommit {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    Push-Location $ProjectDir
    try {
        $hash = git rev-parse HEAD 2>$null
        if ($hash) {
            return $hash.Trim()
        }
    }
    finally {
        Pop-Location
    }

    return Get-ChangelogProcessedThroughCommit -ProjectDir $ProjectDir
}

function Get-ChangelogStatePath {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    return (Join-Path $ProjectDir "scripts\data\changelog-state.json")
}

function Get-ChangelogState {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    $path = Get-ChangelogStatePath -ProjectDir $ProjectDir
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Changelog state not found: $path"
    }
    return (Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Save-ChangelogState {
    param(
        [object]$State,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )
    $path = Get-ChangelogStatePath -ProjectDir $ProjectDir
    $State | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $path -Encoding UTF8 -NoNewline
    Add-Content -LiteralPath $path -Value "" -Encoding UTF8 -NoNewline:$false
}

function ConvertTo-NormalizedChangelogText {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }
    $value = $Text.Trim()
    if ($value.StartsWith('"') -and $value.EndsWith('"')) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    return ($value -replace '\s+', ' ').Trim()
}

function Compare-SemVer {
    param(
        [string]$Left,
        [string]$Right
    )
    $parse = {
        param([string]$Version)
        $parts = $Version.Split('.')
        $patch = 0
        if ($parts.Count -gt 2) {
            $patch = [int]$parts[2]
        }
        [pscustomobject]@{
            Major = [int]$parts[0]
            Minor = [int]$parts[1]
            Patch = $patch
        }
    }
    $a = & $parse $Left
    $b = & $parse $Right
    if ($a.Major -ne $b.Major) { return [math]::Sign($a.Major - $b.Major) }
    if ($a.Minor -ne $b.Minor) { return [math]::Sign($a.Minor - $b.Minor) }
    return [math]::Sign($a.Patch - $b.Patch)
}

function Sort-SemVerDescending {
    param([string[]]$Versions)
    return @($Versions | Sort-Object { [version]($_ -replace '^(\d+\.\d+\.\d+).*$', '$1') } -Descending)
}

function Get-ChangelogEntryScore {
    param([string]$Text)
    $t = (ConvertTo-NormalizedChangelogText $Text).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($t)) { return 0 }

    $score = 0
    if ($t -match 'initial release') { $score += 200 }
    if ($t -match 'howlongtobeat|\bhltb\b') { $score += 100 }
    if ($t -match 'locali[sz]') { $score += 90 }
    if ($t -match 'search|fuzzy|wildcard|scoped metadata|filter panel') { $score += 85 }
    if ($t -match '\bsort') { $score += 80 }
    if ($t -match 'column|rank|playtime|last played|last activity|completion status') { $score += 75 }
    if ($t -match 'drag|reorder') { $score += 70 }
    if ($t -match 'playlist tag|auto-generated filter|synchronised playlist') { $score += 65 }
    if ($t -match '\badd\b|\bnew\b|\bshow\b|\bhide\b|\bpersist\b|\btoggle\b') { $score += 50 }
    if ($t -match 'theme|styling|chrome|glyph|cursor|highlight') { $score += 45 }
    if ($t -match 'improve|better|clearer') { $score += 40 }
    if ($t -match 'fix|bug|error|clamp|harden') { $score += 30 }
    if ($t -match 'dependenc|requires playnite|toolbox|compress-archive|api ') { $score += 10 }
    return $score
}

function Sort-ChangelogEntries {
    param([string[]]$Entries)
    return @(
        $Entries |
            ForEach-Object { [pscustomobject]@{ Text = $_; Score = Get-ChangelogEntryScore $_ } } |
            Sort-Object -Property @{ Expression = 'Score'; Descending = $true }, @{ Expression = 'Text'; Descending = $false } |
            ForEach-Object { $_.Text }
    )
}

function Read-InstallerManifestText {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    $path = Join-Path $ProjectDir "Installer_Manifest.yaml"
    return (Get-Content -LiteralPath $path -Raw -Encoding UTF8)
}

function Write-InstallerManifestText {
    param(
        [string]$Content,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )
    $path = Join-Path $ProjectDir "Installer_Manifest.yaml"
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8 -NoNewline
}

function Parse-InstallerManifestPackages {
    param([string]$Content)

    $packages = [System.Collections.Generic.List[object]]::new()
    $lines = $Content -split "`r?`n"
    $index = 0
    while ($index -lt $lines.Count) {
        if ($lines[$index] -notmatch '^\s*-\s*Version:\s*(.+?)\s*$') {
            $index++
            continue
        }

        $version = $Matches[1].Trim().Trim('"')
        $package = [ordered]@{
            Version = $version
            RequiredApiVersion = $null
            ReleaseDate = $null
            PackageUrl = $null
            Changelog = [System.Collections.Generic.List[string]]::new()
        }
        $index++

        while ($index -lt $lines.Count -and $lines[$index] -notmatch '^\s*-\s*Version:\s*') {
            $line = $lines[$index]
            if ($line -match '^\s*RequiredApiVersion:\s*(.+?)\s*$') {
                $package.RequiredApiVersion = $Matches[1].Trim()
            }
            elseif ($line -match '^\s*ReleaseDate:\s*(.+?)\s*$') {
                $package.ReleaseDate = $Matches[1].Trim()
            }
            elseif ($line -match '^\s*PackageUrl:\s*(.+?)\s*$') {
                $package.PackageUrl = $Matches[1].Trim()
            }
            elseif ($line -match '^\s*-\s+(.+?)\s*$' -and $line -notmatch '^\s*-\s*Version:') {
                $null = $package.Changelog.Add((ConvertTo-NormalizedChangelogText $Matches[1]))
            }
            $index++
        }

        $packages.Add([pscustomobject]$package)
    }

    return @($packages)
}

function Format-InstallerManifestPackages {
    param(
        [object[]]$Packages
    )

    $builder = New-Object System.Text.StringBuilder
    $null = $builder.AppendLine("AddonId: Playlist_b0313f81-2b86-4eba-9f24-1a727dedbd45")
    $null = $builder.AppendLine("Packages:")

    foreach ($package in $Packages) {
        $null = $builder.AppendLine("  - Version: $($package.Version)")
        if ($package.RequiredApiVersion) {
            $null = $builder.AppendLine("    RequiredApiVersion: $($package.RequiredApiVersion)")
        }
        if ($package.ReleaseDate) {
            $null = $builder.AppendLine("    ReleaseDate: $($package.ReleaseDate)")
        }
        if ($package.PackageUrl) {
            $null = $builder.AppendLine("    PackageUrl: $($package.PackageUrl)")
        }
        $null = $builder.AppendLine("    Changelog:")
        foreach ($entry in $package.Changelog) {
            $escaped = ($entry -replace '\\', '\\\\' -replace '"', '\"')
            $null = $builder.AppendLine("      - `"$escaped`"")
        }
    }

    return $builder.ToString().TrimEnd()
}

function Get-CurrentInstallerManifestPackage {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    $packages = Parse-InstallerManifestPackages (Read-InstallerManifestText -ProjectDir $ProjectDir)
    if ($packages.Count -eq 0) {
        throw "Installer_Manifest.yaml has no package entries."
    }
    return $packages[0]
}

function Get-ExtensionVersion {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    $path = Join-Path $ProjectDir "extension.yaml"
    $match = (Select-String -Path $path -Pattern '^\s*Version:\s*(.+?)\s*$').Matches[0]
    return $match.Groups[1].Value.Trim()
}

function Set-ProjectVersion {
    param(
        [string]$Version,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    if ($Version -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must be MAJOR.MINOR.PATCH (got '$Version')."
    }

    $extensionPath = Join-Path $ProjectDir "extension.yaml"
    $extension = Get-Content -LiteralPath $extensionPath -Raw -Encoding UTF8
    $extension = $extension -replace '(?m)^(\s*Version:\s*).+$', "`${1}$Version"
    Set-Content -LiteralPath $extensionPath -Value $extension -Encoding UTF8 -NoNewline

    $assemblyPath = Join-Path $ProjectDir "Properties\AssemblyInfo.cs"
    $assembly = Get-Content -LiteralPath $assemblyPath -Raw -Encoding UTF8
    $assemblyVersion = "$Version.0"
    $assembly = $assembly -replace '\[assembly: AssemblyVersion\("[^"]+"\)\]', "[assembly: AssemblyVersion(`"$assemblyVersion`")]"
    $assembly = $assembly -replace '\[assembly: AssemblyFileVersion\("[^"]+"\)\]', "[assembly: AssemblyFileVersion(`"$assemblyVersion`")]"
    Set-Content -LiteralPath $assemblyPath -Value $assembly -Encoding UTF8 -NoNewline
}

function Get-LastVersionBumpCommit {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))
    Push-Location $ProjectDir
    try {
        $hash = git log -1 --format=%H --grep='^Bump version to v'
        if (-not $hash) {
            return $null
        }
        return $hash.Trim()
    }
    finally {
        Pop-Location
    }
}

function Parse-InstallerManifestPackagesFromGit {
    param([string]$CommitHash)
    Push-Location (Get-ChangelogProjectRoot)
    try {
        $content = git show "${CommitHash}:Installer_Manifest.yaml" 2>$null | Out-String
        if ([string]::IsNullOrWhiteSpace($content)) {
            return @()
        }
        return (Parse-InstallerManifestPackages $content)
    }
    finally {
        Pop-Location
    }
}

function Get-HistoricalInstallerManifestVersions {
    param([string]$ProjectDir = (Get-ChangelogProjectRoot))

    Push-Location $ProjectDir
    try {
        $commits = @(git log --all --reverse --format=%H -- Installer_Manifest.yaml)
        $byVersion = [ordered]@{}
        foreach ($commit in $commits) {
            $packages = Parse-InstallerManifestPackagesFromGit -CommitHash $commit
            if ($packages.Count -eq 0) { continue }
            $package = $packages[0]
            $byVersion[$package.Version] = [pscustomobject]@{
                Version = $package.Version
                Commit = $commit
                ReleaseDate = $package.ReleaseDate
                Changelog = [string[]](@($package.Changelog))
                RequiredApiVersion = $package.RequiredApiVersion
            }
        }
        return $byVersion
    }
    finally {
        Pop-Location
    }
}

function Get-VersionReleaseNotes {
    param(
        [object]$HistoricalByVersion,
        [string]$Version,
        [hashtable]$ReleasedSections = @{},
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    if ($ReleasedSections.ContainsKey($Version) -and $ReleasedSections[$Version].Count -gt 0) {
        return @($ReleasedSections[$Version])
    }

    $versions = @($HistoricalByVersion.Keys | Sort-Object { [version]($_ -replace '^(\d+\.\d+\.\d+).*$', '$1') })
    $index = [array]::IndexOf($versions, $Version)
    if ($index -lt 0) {
        return @()
    }

    $current = $HistoricalByVersion[$Version].Changelog

    $previous = @()
    if ($index -gt 0) {
        $previousVersion = $versions[$index - 1]
        $previous = @($HistoricalByVersion[$previousVersion].Changelog)
    }

    $previousSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $previous) { $null = $previousSet.Add($item) }

    $delta = @($current | Where-Object { -not $previousSet.Contains($_) })
    if ($delta.Count -eq 0 -and $Version -eq '1.5.1') {
        $delta = @("Update add-on package URL for the playnite-playlist-reborn repository")
    }
    if ($delta.Count -eq 0 -and $Version -eq '1.5.0') {
        $delta = @("First release under Kinland as primary maintainer (same feature set as v1.4.3)")
    }
    return @(Sort-ChangelogEntries $delta)
}

function Test-ChangelogEntryExists {
    param(
        [string[]]$Entries,
        [string]$Candidate
    )
    $normalized = ConvertTo-NormalizedChangelogText $Candidate
    foreach ($entry in $Entries) {
        if ((ConvertTo-NormalizedChangelogText $entry) -eq $normalized) {
            return $true
        }
    }
    return $false
}

function Add-InstallerManifestChangelogEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Entry,
        [string]$OperationId,
        [string]$ProjectDir = (Get-ChangelogProjectRoot),
        [switch]$Force
    )

    $entry = ConvertTo-NormalizedChangelogText $Entry
    if ([string]::IsNullOrWhiteSpace($entry)) {
        return $false
    }

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $currentVersion = Get-ExtensionVersion -ProjectDir $ProjectDir

    if ($OperationId) {
        $recorded = $state.recordedSyncOperations.$OperationId
        if (-not $recorded -and $state.syncOperations) {
            $recorded = $state.syncOperations.$OperationId
        }
        if (-not $Force -and $recorded -and $recorded.version -eq $currentVersion) {
            Write-Host "Changelog: skipping '$OperationId' (already recorded for v$currentVersion)."
            return $false
        }
    }

    $content = Read-InstallerManifestText -ProjectDir $ProjectDir
    $packages = @(Parse-InstallerManifestPackages $content)
    if ($packages.Count -eq 0) {
        throw "Installer_Manifest.yaml has no package entries."
    }

    if (Test-ChangelogEntryExists -Entries $packages[0].Changelog -Candidate $entry) {
        Write-Host "Changelog: entry already present in Installer_Manifest for v$($packages[0].Version)."
        if ($OperationId) {
            if (-not $state.recordedSyncOperations) {
                $state | Add-Member -NotePropertyName recordedSyncOperations -NotePropertyValue ([pscustomobject]@{}) -Force
            }
            $state.recordedSyncOperations | Add-Member -NotePropertyName $OperationId -NotePropertyValue ([pscustomobject]@{
                version = $currentVersion
                entry = $entry
            }) -Force
            Save-ChangelogState -State $state -ProjectDir $ProjectDir
        }
        return $false
    }

    $packages[0].Changelog.Insert(0, $entry)
    Write-InstallerManifestText -Content (Format-InstallerManifestPackages $packages) -ProjectDir $ProjectDir
    Write-Host "Changelog: added Installer_Manifest entry for v$($packages[0].Version): $entry"

    if ($OperationId) {
        if (-not $state.recordedSyncOperations) {
            $state | Add-Member -NotePropertyName recordedSyncOperations -NotePropertyValue ([pscustomobject]@{}) -Force
        }
        $state.recordedSyncOperations | Add-Member -NotePropertyName $OperationId -NotePropertyValue ([pscustomobject]@{
            version = $currentVersion
            entry = $entry
        }) -Force
        Save-ChangelogState -State $state -ProjectDir $ProjectDir
    }

    return $true
}

function Sync-InstallerManifestChangelogEntries {
    param(
        [string[]]$Entries,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    if (-not $Entries -or $Entries.Count -eq 0) {
        return 0
    }

    $extensionVersion = Get-ExtensionVersion -ProjectDir $ProjectDir
    $packages = @(Parse-InstallerManifestPackages (Read-InstallerManifestText -ProjectDir $ProjectDir))
    if ($packages.Count -eq 0) {
        throw "Installer_Manifest.yaml has no package entries."
    }

    $current = $packages[0]
    if ($current.Version -ne $extensionVersion) {
        Write-Host "Changelog: skipping manifest append (manifest top is v$($current.Version), extension is v$extensionVersion; run bump-version first)."
        return 0
    }

    $added = 0
    foreach ($entry in $Entries) {
        if (Test-ChangelogEntryExists -Entries @($current.Changelog) -Candidate $entry) {
            continue
        }

        $current.Changelog.Insert(0, (ConvertTo-NormalizedChangelogText $entry))
        $added++
    }

    if ($added -gt 0) {
        Write-InstallerManifestText -Content (Format-InstallerManifestPackages $packages) -ProjectDir $ProjectDir
        Write-Host "Changelog: appended $added entr$(if ($added -eq 1) { 'y' } else { 'ies' }) to Installer_Manifest v$($current.Version)."
    }

    return $added
}

function Register-SyncChangelogOperation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OperationId,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $entry = $state.syncOperationMessages.$OperationId
    if (-not $entry) {
        throw "Unknown sync operation id '$OperationId'. Add it to scripts/data/changelog-state.json under syncOperationMessages."
    }
    Add-InstallerManifestChangelogEntry -Entry $entry -OperationId $OperationId -ProjectDir $ProjectDir | Out-Null
}

function Test-CommitSubjectIsUserFacing {
    param(
        [string]$Subject,
        [object]$Config
    )
    foreach ($pattern in $Config.commitSubjectSkipPatterns) {
        if ($Subject -match $pattern) {
            return $false
        }
    }
    return $true
}

function Convert-CommitSubjectToChangelogEntry {
    param([string]$Subject)
    $entry = $Subject.Trim()
    if ($entry.Length -gt 0) {
        $entry = $entry.Substring(0, 1).ToUpper() + $entry.Substring(1)
    }
    if (-not $entry.EndsWith('.')) {
        $entry += '.'
    }
    return $entry
}

function Test-CommitMentionedInEntries {
    param(
        [string]$Subject,
        [string[]]$Entries
    )

    $subject = $Subject.ToLowerInvariant()
    $tokens = @($subject -split '[^a-z0-9]+' | Where-Object { $_.Length -ge 4 })
    foreach ($entry in $Entries) {
        $normalized = (ConvertTo-NormalizedChangelogText $entry).ToLowerInvariant()
        $matchCount = @($tokens | Where-Object { $normalized.Contains($_) }).Count
        if ($matchCount -ge [math]::Min(3, $tokens.Count)) {
            return $true
        }
    }
    return $false
}

function Get-NewCommitChangelogCandidates {
    param(
        [string]$ProjectDir = (Get-ChangelogProjectRoot),
        [string[]]$ExistingEntries,
        [string]$SinceCommit
    )

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    if (-not $SinceCommit) {
        $SinceCommit = Get-ChangelogProcessedThroughCommit -ProjectDir $ProjectDir
    }
    if (-not $SinceCommit) {
        return @()
    }

    Push-Location $ProjectDir
    try {
        $lines = @(git log --format=%H%x09%s "$SinceCommit..HEAD" --no-merges)
    }
    finally {
        Pop-Location
    }

    if ($lines.Count -eq 0) {
        return @()
    }

    $commits = foreach ($line in $lines) {
        $parts = $line -split "`t", 2
        [pscustomobject]@{ Hash = $parts[0]; Subject = $parts[1] }
    }

    $mentioned = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($commit in $commits) {
        if (Test-CommitMentionedInEntries -Subject $commit.Subject -Entries $ExistingEntries) {
            $null = $mentioned.Add($commit.Hash)
        }
    }

    $candidates = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $commits.Count; $i++) {
        $commit = $commits[$i]
        if (-not (Test-CommitSubjectIsUserFacing -Subject $commit.Subject -Config $state)) {
            continue
        }
        if ($mentioned.Contains($commit.Hash)) {
            continue
        }
        if (Test-CommitMentionedInEntries -Subject $commit.Subject -Entries $ExistingEntries) {
            continue
        }

        $beforeMentioned = ($i -gt 0) -and $mentioned.Contains($commits[$i - 1].Hash)
        $afterMentioned = ($i -lt ($commits.Count - 1)) -and $mentioned.Contains($commits[$i + 1].Hash)
        if ($beforeMentioned -and $afterMentioned) {
            continue
        }

        $entry = Convert-CommitSubjectToChangelogEntry $commit.Subject
        if (-not (Test-ChangelogEntryExists -Entries $ExistingEntries -Candidate $entry) -and
            -not (Test-ChangelogEntryExists -Entries $candidates -Candidate $entry)) {
            $null = $candidates.Add($entry)
        }
    }

    return @(Sort-ChangelogEntries @($candidates))
}

function Get-InstallerManifestReleaseNotesMarkdown {
    param(
        [string]$Version,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    $packages = Parse-InstallerManifestPackages (Read-InstallerManifestText -ProjectDir $ProjectDir)
    $package = $packages | Where-Object { $_.Version -eq $Version } | Select-Object -First 1
    if (-not $package) {
        throw "Installer_Manifest.yaml has no package entry for version $Version."
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $null = $lines.Add("# Playlist v$Version")
    $null = $lines.Add("")
    foreach ($entry in (Sort-ChangelogEntries @($package.Changelog))) {
        $null = $lines.Add("- $entry")
    }
    return ($lines -join "`n")
}

function Get-ExtensionVersionAtCommit {
    param(
        [string]$CommitHash,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    if ([string]::IsNullOrWhiteSpace($CommitHash)) {
        return $null
    }

    Push-Location $ProjectDir
    try {
        $line = git show "${CommitHash}:extension.yaml" 2>$null | Select-String -Pattern '^\s*Version:\s*(.+?)\s*$' | Select-Object -First 1
        if (-not $line) {
            return $null
        }
        return $line.Matches[0].Groups[1].Value.Trim()
    }
    finally {
        Pop-Location
    }
}

function Get-ChangelogPendingReleaseEntries {
    param(
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    $sinceBump = Get-LastVersionBumpCommit -ProjectDir $ProjectDir
    if (-not $sinceBump) {
        return @()
    }

    $extensionVersion = Get-ExtensionVersion -ProjectDir $ProjectDir
    $versionAtBump = Get-ExtensionVersionAtCommit -CommitHash $sinceBump -ProjectDir $ProjectDir
    if ($versionAtBump -and (Compare-SemVer $extensionVersion $versionAtBump) -gt 0) {
        # extension.yaml was bumped (e.g. to v1.7.1) but those changes are not released until
        # bump-version finalizes the manifest package; nothing is pending for [Unreleased].
        return @()
    }

    return @(Get-NewCommitChangelogCandidates -ProjectDir $ProjectDir -ExistingEntries @() -SinceCommit $sinceBump)
}

function Format-ChangelogMarkdown {
    param(
        [object]$HistoricalByVersion,
        [string[]]$UnreleasedEntries,
        [string]$DocumentedThroughVersion,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $releasedSections = Get-ChangelogReleasedSectionsFromFile -ProjectDir $ProjectDir
    $builder = New-Object System.Text.StringBuilder
    $null = $builder.AppendLine("# Changelog")
    $null = $builder.AppendLine("")
    $null = $builder.AppendLine("All notable changes to the Playlist Playnite extension are documented here.")
    $null = $builder.AppendLine("")
    $null = $builder.AppendLine("Release notes are mirrored in ``Installer_Manifest.yaml`` for the Playnite add-on catalog.")
    $null = $builder.AppendLine("Regenerate with ``pwsh ./scripts/sync-changelog.ps1`` (appends new commit summaries to the manifest and refreshes ``[Unreleased]``).")
    $null = $builder.AppendLine("")

    $null = $builder.AppendLine("## [Unreleased]")
    $null = $builder.AppendLine("")
    if ($UnreleasedEntries.Count -eq 0) {
        $null = $builder.AppendLine("_No unreleased changes recorded yet._")
    }
    else {
        foreach ($entry in $UnreleasedEntries) {
            if ($entry -match '^\s*- ') {
                $null = $builder.AppendLine($entry)
            }
            else {
                $null = $builder.AppendLine("- $entry")
            }
        }
    }
    $null = $builder.AppendLine("")

    $versions = Sort-SemVerDescending @($HistoricalByVersion.Keys)
    foreach ($version in $versions) {
        if ($DocumentedThroughVersion -and (Compare-SemVer $version $DocumentedThroughVersion) -gt 0) {
            continue
        }

        $meta = $HistoricalByVersion[$version]
        $entries = Get-VersionReleaseNotes -HistoricalByVersion $HistoricalByVersion -Version $version -ReleasedSections $releasedSections -ProjectDir $ProjectDir
        $dateSuffix = ""
        if ($meta.ReleaseDate) {
            $dateSuffix = " - $($meta.ReleaseDate)"
        }
        $null = $builder.AppendLine("## [$version]$dateSuffix")
        $null = $builder.AppendLine("")
        if ($entries.Count -eq 0) {
            $null = $builder.AppendLine("_No user-facing changes recorded for this release._")
        }
        else {
            foreach ($entry in $entries) {
                if ($entry -match '^\s*- ') {
                    $null = $builder.AppendLine($entry)
                }
                else {
                    $null = $builder.AppendLine("- $entry")
                }
            }
        }
        $null = $builder.AppendLine("")

        if ($version -eq $state.maintainerChange.firstMaintainerRelease) {
            $null = $builder.AppendLine($state.maintainerChange.markdown)
            $null = $builder.AppendLine("")
        }
    }

    return $builder.ToString().TrimEnd() + "`n"
}

function Sync-ChangelogMarkdown {
    param(
        [string]$ProjectDir = (Get-ChangelogProjectRoot),
        [string]$DocumentedThroughVersion = (Get-ExtensionVersion -ProjectDir $ProjectDir),
        [switch]$IncludeCommitCandidates,
        [switch]$SkipManifestAppend,
        [string]$ProcessedThroughCommit
    )

    $historical = Get-HistoricalInstallerManifestVersions -ProjectDir $ProjectDir
    $currentPackage = Get-CurrentInstallerManifestPackage -ProjectDir $ProjectDir
    $existing = @($currentPackage.Changelog)

    $manifestCandidates = @()
    if ($IncludeCommitCandidates) {
        $sinceSync = Get-ChangelogProcessedThroughCommit -ProjectDir $ProjectDir
        $manifestCandidates = @(Get-NewCommitChangelogCandidates -ProjectDir $ProjectDir -ExistingEntries $existing -SinceCommit $sinceSync)
        if (-not $SkipManifestAppend) {
            Sync-InstallerManifestChangelogEntries -Entries $manifestCandidates -ProjectDir $ProjectDir | Out-Null
        }
    }

    $unreleased = if ($IncludeCommitCandidates) {
        @(Get-ChangelogPendingReleaseEntries -ProjectDir $ProjectDir)
    }
    else {
        @(Get-ChangelogUnreleasedEntriesFromFile -ProjectDir $ProjectDir)
    }

    if (-not $ProcessedThroughCommit) {
        if ($IncludeCommitCandidates) {
            $ProcessedThroughCommit = Resolve-ChangelogProcessedThroughCommit -ProjectDir $ProjectDir
        }
        else {
            $ProcessedThroughCommit = Get-ChangelogProcessedThroughCommit -ProjectDir $ProjectDir
        }
    }

    $markdown = Format-ChangelogMarkdown `
        -HistoricalByVersion $historical `
        -UnreleasedEntries $unreleased `
        -DocumentedThroughVersion $DocumentedThroughVersion `
        -ProjectDir $ProjectDir

    $path = Get-ChangelogMarkdownPath -ProjectDir $ProjectDir
    Set-Content -LiteralPath $path -Value $markdown -Encoding UTF8 -NoNewline
    Add-Content -LiteralPath $path -Value "" -Encoding UTF8 -NoNewline:$false

    if ($ProcessedThroughCommit) {
        Save-ChangelogProcessedThroughCommit -CommitHash $ProcessedThroughCommit -ProjectDir $ProjectDir
    }

    $manifestNote = if ($IncludeCommitCandidates -and -not $SkipManifestAppend) { "; manifest append enabled" } else { "" }
    Write-Host "Wrote $path (documented through v$DocumentedThroughVersion; processed through $ProcessedThroughCommit$manifestNote)."
    return $markdown
}

function New-InstallerManifestPackageEntry {
    param(
        [string]$Version,
        [string]$RequiredApiVersion,
        [string]$ReleaseDate,
        [string]$PackageUrl,
        [string[]]$Changelog,
        [string]$ProjectDir = (Get-ChangelogProjectRoot)
    )

    $content = Read-InstallerManifestText -ProjectDir $ProjectDir
    $packages = [System.Collections.Generic.List[object]]::new()
    $packages.Add([pscustomobject][ordered]@{
        Version = $Version
        RequiredApiVersion = $RequiredApiVersion
        ReleaseDate = $ReleaseDate
        PackageUrl = $PackageUrl
        Changelog = [System.Collections.Generic.List[string]]::new(@($Changelog))
    })
    foreach ($existing in (Parse-InstallerManifestPackages $content)) {
        if ($existing.Version -ne $Version) {
            $packages.Add($existing)
        }
    }
    Write-InstallerManifestText -Content (Format-InstallerManifestPackages @($packages)) -ProjectDir $ProjectDir
}

function Invoke-ProjectVersionBump {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Version,
        [string]$ReleaseDate = (Get-Date -Format "yyyy-MM-dd"),
        [string]$ProjectDir = (Get-ChangelogProjectRoot),
        [switch]$PromoteUnreleasedOnly
    )

    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $current = Get-ExtensionVersion -ProjectDir $ProjectDir
    if ((Compare-SemVer $Version $current) -le 0) {
        throw "New version ($Version) must be greater than current version ($current)."
    }

    $currentPackage = Get-CurrentInstallerManifestPackage -ProjectDir $ProjectDir
    $changelog = [System.Collections.Generic.List[string]]::new()
    if ($PromoteUnreleasedOnly) {
        $changelog.AddRange((Get-NewCommitChangelogCandidates -ProjectDir $ProjectDir -ExistingEntries @($currentPackage.Changelog)))
    }
    else {
        $changelog.AddRange(@($currentPackage.Changelog))
        foreach ($candidate in (Get-NewCommitChangelogCandidates -ProjectDir $ProjectDir -ExistingEntries @($currentPackage.Changelog))) {
            if (-not (Test-ChangelogEntryExists -Entries @($changelog) -Candidate $candidate)) {
                $null = $changelog.Add($candidate)
            }
        }
    }

    if ($changelog.Count -eq 0) {
        throw "No changelog entries to carry into v$Version. Update Installer_Manifest.yaml or make user-facing commits first."
    }

    $requiredApi = $currentPackage.RequiredApiVersion
    if (-not $requiredApi) { $requiredApi = "6.5.0" }
    $packageUrl = "{0}/releases/download/v{1}/{2}" -f $state.repositoryUrl.TrimEnd('/'), $Version, ($state.packageFilePattern -replace '\{version\}', $Version)

    Set-ProjectVersion -Version $Version -ProjectDir $ProjectDir
    New-InstallerManifestPackageEntry `
        -Version $Version `
        -RequiredApiVersion $requiredApi `
        -ReleaseDate $ReleaseDate `
        -PackageUrl $packageUrl `
        -Changelog (Sort-ChangelogEntries @($changelog)) `
        -ProjectDir $ProjectDir

    Clear-ChangelogSyncWatermark -ProjectDir $ProjectDir
    $state = Get-ChangelogState -ProjectDir $ProjectDir
    $state.recordedSyncOperations = [pscustomobject]@{}
    Save-ChangelogState -State $state -ProjectDir $ProjectDir

    Sync-ChangelogMarkdown -ProjectDir $ProjectDir -DocumentedThroughVersion $Version -IncludeCommitCandidates
    Write-Host "Bumped project version to v$Version."
}
