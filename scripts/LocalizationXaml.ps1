# Shared helpers for Playlist Localization/*.xaml files.
# Preserves multiline sys:String entries (e.g. LOCPlaylist_SearchTooltip with xml:space="preserve").

$script:LocalizationEntryPattern = '(?s)[ \t]*<sys:String\s+x:Key="([^"]+)"([^>]*)>(.*?)</sys:String>'

function Get-LocalizationEntries {
    param(
        [string]$Content
    )

    $entries = [ordered]@{}
    foreach ($match in [regex]::Matches($Content, $script:LocalizationEntryPattern)) {
        $key = $match.Groups[1].Value
        if ($entries.Contains($key)) {
            throw "Duplicate localization key: $key"
        }

        $entries[$key] = [pscustomobject]@{
            Key = $key
            Attributes = $match.Groups[2].Value
            Value = $match.Groups[3].Value
        }
    }

    return $entries
}

function Format-LocalizationEntry {
    param(
        [string]$Key,
        [string]$Attributes,
        [string]$Value
    )

    return '    <sys:String x:Key="' + $Key + '"' + $Attributes + '>' + $Value + '</sys:String>'
}

function Write-LocalizationFile {
    param(
        [string]$FilePath,
        [System.Collections.IDictionary]$Entries,
        [System.Text.UTF8Encoding]$Utf8
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $Utf8)
    $firstEntry = [regex]::Match($content, '<sys:String\s+x:Key=')
    if (-not $firstEntry.Success) {
        throw "No sys:String entries found in $FilePath"
    }

    $header = $content.Substring(0, $firstEntry.Index).TrimEnd()
    $sortedKeys = $Entries.Keys | Sort-Object
    $body = foreach ($key in $sortedKeys) {
        $entry = $Entries[$key]
        Format-LocalizationEntry -Key $entry.Key -Attributes $entry.Attributes -Value $entry.Value
    }

    $updated = $header + [Environment]::NewLine + ($body -join [Environment]::NewLine) + [Environment]::NewLine + '</ResourceDictionary>' + [Environment]::NewLine
    [System.IO.File]::WriteAllText($FilePath, $updated, $Utf8)
}

function Get-LocValue {
    param(
        [string]$FilePath,
        [string]$Key,
        [System.Text.UTF8Encoding]$Utf8
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $null
    }

    $entries = Get-LocalizationEntries -Content ([System.IO.File]::ReadAllText($FilePath, $Utf8))
    if (-not $entries.Contains($Key)) {
        return $null
    }

    return $entries[$Key].Value
}

function Set-LocValue {
    param(
        [string]$FilePath,
        [string]$Key,
        [string]$Value,
        [System.Text.UTF8Encoding]$Utf8
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $Utf8)
    $entries = Get-LocalizationEntries -Content $content
    if ($entries.Contains($Key)) {
        $entry = $entries[$Key]
        if ($entry.Value -ceq $Value) {
            return $false
        }

        $entries[$Key] = [pscustomobject]@{
            Key = $Key
            Attributes = $entry.Attributes
            Value = $Value
        }
    }
    else {
        $entries[$Key] = [pscustomobject]@{
            Key = $Key
            Attributes = ''
            Value = $Value
        }
    }

    Write-LocalizationFile -FilePath $FilePath -Entries $entries -Utf8 $Utf8
    return $true
}

function Remove-LocKey {
    param(
        [string]$FilePath,
        [string]$Key,
        [System.Text.UTF8Encoding]$Utf8
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $Utf8)
    $entries = Get-LocalizationEntries -Content $content
    if (-not $entries.Contains($Key)) {
        return $false
    }

    $entries.Remove($Key)
    Write-LocalizationFile -FilePath $FilePath -Entries $entries -Utf8 $Utf8
    return $true
}

function Get-SupplementalLocaleNames {
    param(
        [string]$LocalizationDir
    )

    $markerPath = Join-Path $LocalizationDir ".supplemental-locales"
    if (-not (Test-Path -LiteralPath $markerPath)) {
        return @()
    }

    return @(Get-Content -LiteralPath $markerPath -Encoding UTF8 |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ })
}

function Test-SupplementalLocale {
    param(
        [string]$Locale,
        [string]$LocalizationDir
    )

    $supplemental = Get-SupplementalLocaleNames -LocalizationDir $LocalizationDir
    return $supplemental -contains $Locale
}

function Sort-LocalizationFileByKey {
    param(
        [string]$FilePath,
        [System.Text.UTF8Encoding]$Utf8
    )

    $content = [System.IO.File]::ReadAllText($FilePath, $Utf8)
    $entries = Get-LocalizationEntries -Content $content
    $sortedKeys = $entries.Keys | Sort-Object
    $alreadySorted = $true
    for ($index = 0; $index -lt $sortedKeys.Count; $index++) {
        if ($entries.Keys[$index] -cne $sortedKeys[$index]) {
            $alreadySorted = $false
            break
        }
    }

    if ($alreadySorted) {
        return $false
    }

    Write-LocalizationFile -FilePath $FilePath -Entries $entries -Utf8 $Utf8
    return $true
}
