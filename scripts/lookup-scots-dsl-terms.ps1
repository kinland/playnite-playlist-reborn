#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Batch-lookup Scots vocabulary from DSL (dsl.ac.uk) for localization work.

.DESCRIPTION
    For each English or Scots search term:
      1. Queries DSL quick search (https://dsl.ac.uk/results?search=...)
      2. Fetches the best matching entry page (prefers SND headword matches)
      3. Extracts headword, Scots form cross-references, and sense snippets

    Uses exponential backoff with jitter on transient HTTP failures and rate limits.
    Resumes from an existing output file unless -Force is passed.

    Primary source: Dictionaries of the Scots Language (DSL)
    https://dsl.ac.uk/

.PARAMETER TermsFile
    JSON array of terms to look up. Defaults to scripts/data/sco-gb-lookup-terms.json.

.PARAMETER OutputFile
    JSON results written here. Defaults to scripts/data/sco-gb-dsl-lookups.json.

.PARAMETER Force
    Re-fetch all terms even when already present in the output file.

.EXAMPLE
    pwsh -File scripts/lookup-scots-dsl-terms.ps1

.EXAMPLE
    pwsh -File scripts/lookup-scots-dsl-terms.ps1 -TermsFile scripts/data/sco-gb-lookup-terms.json -Force
#>
[CmdletBinding()]
param(
    [string]$ProjectDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$TermsFile,
    [string]$OutputFile,
    [int]$MaxRetries = 6,
    [int]$InitialBackoffMs = 1000,
    [double]$BackoffMultiplier = 2,
    [int]$MaxBackoffMs = 60000,
    [int]$RequestTimeoutSec = 30,
    [int]$DelayBetweenRequestsMs = 400,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not $TermsFile) {
    $TermsFile = Join-Path $PSScriptRoot "data/sco-gb-lookup-terms.json"
}
if (-not $OutputFile) {
    $OutputFile = Join-Path $PSScriptRoot "data/sco-gb-dsl-lookups.json"
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$script:UserAgent = "playnite-playlist-locale-lookup/1.0 (localization research; +https://github.com/kinland/playnite-playlist)"

function Get-RandomJitterMs {
    param([int]$MaxJitterMs = 250)
    return Get-Random -Minimum 0 -Maximum ($MaxJitterMs + 1)
}

function Test-TransientWebError {
    param([System.Exception]$Exception)
    $ex = $Exception
    while ($null -ne $ex) {
        if ($ex -is [System.Net.Http.HttpRequestException]) { return $true }
        if ($ex -is [System.IO.IOException]) { return $true }
        if ($ex -is [System.TimeoutException]) { return $true }
        if ($ex -is [System.OperationCanceledException]) { return $true }
        if ($ex.Message -match 'timed out|timeout|connection|actively refused|no such host') { return $true }
        $ex = $ex.InnerException
    }
    return $false
}

function Get-HttpStatusCode {
    param([System.Exception]$Exception)
    $ex = $Exception
    while ($null -ne $ex) {
        if ($ex.PSObject.Properties.Name -contains 'Response' -and $null -ne $ex.Response) {
            return [int]$ex.Response.StatusCode
        }
        if ($ex -is [System.Net.WebException] -and $null -ne $ex.Response) {
            return [int]$ex.Response.StatusCode
        }
        $ex = $ex.InnerException
    }
    return $null
}

function Test-ShouldRetry {
    param(
        [System.Exception]$Exception,
        [int]$Attempt,
        [int]$MaxAttempts
    )
    if ($Attempt -ge $MaxAttempts) { return $false }
    $status = Get-HttpStatusCode -Exception $Exception
    if ($status -in 408, 425, 429, 500, 502, 503, 504) { return $true }
    return (Test-TransientWebError -Exception $Exception)
}

function Invoke-WithExponentialBackoff {
    param(
        [string]$Label,
        [scriptblock]$Action
    )
    $attempt = 0
    $delayMs = $InitialBackoffMs
    while ($true) {
        $attempt++
        try {
            return & $Action
        }
        catch {
            if (-not (Test-ShouldRetry -Exception $_ -Attempt $attempt -MaxAttempts $MaxRetries)) {
                throw
            }
            $status = Get-HttpStatusCode -Exception $_
            $statusText = if ($status) { " HTTP $status" } else { "" }
            $sleepMs = [Math]::Min($MaxBackoffMs, $delayMs) + (Get-RandomJitterMs)
            Write-Warning "$Label failed on attempt $attempt/$MaxRetries$statusText. Retrying in ${sleepMs}ms. $($_.Exception.Message)"
            Start-Sleep -Milliseconds $sleepMs
            $delayMs = [Math]::Min($MaxBackoffMs, [int][Math]::Round($delayMs * $BackoffMultiplier))
        }
    }
}

function Invoke-DslGet {
    param([string]$Uri)
    return Invoke-WithExponentialBackoff -Label "GET $Uri" -Action {
        Invoke-WebRequest -Uri $Uri -Method GET -TimeoutSec $RequestTimeoutSec -UseBasicParsing -Headers @{
            "User-Agent" = $script:UserAgent
            "Accept" = "text/html,application/xhtml+xml"
        }
    }
}

function ConvertFrom-HtmlText {
    param([string]$Html)
    if ([string]::IsNullOrWhiteSpace($Html)) { return "" }
    $text = $Html
    $text = $text -replace '(?s)<script.*?</script>', ''
    $text = $text -replace '(?s)<style.*?</style>', ''
    $text = $text -replace '<br\s*/?>', "`n"
    $text = $text -replace '</p>', "`n"
    $text = $text -replace '<[^>]+>', ' '
    $text = $text -replace '&nbsp;', ' '
    $text = $text -replace '&ndash;', '-'
    $text = $text -replace '&mdash;', '-'
    $text = $text -replace '&quot;', '"'
    $text = $text -replace '&rsquo;', "'"
    $text = $text -replace '&lsquo;', "'"
    $text = $text -replace '&amp;', '&'
    $text = $text -replace '\s+', ' '
    return $text.Trim()
}

function Get-DslSearchResults {
    param([string]$Term)
    $encoded = [Uri]::EscapeDataString($Term)
    $uri = "https://dsl.ac.uk/results?search=$encoded"
    $response = Invoke-DslGet -Uri $uri
    $results = @()
    foreach ($match in [regex]::Matches($response.Content, 'href="(/entry/[^"]+)"')) {
        $path = $match.Groups[1].Value
        if ($path -match '^/entry/(snd|dost)/(.+)$') {
            $results += [pscustomobject]@{
                Source = $Matches[1]
                Slug = $Matches[2]
                Path = $path
                Url = "https://dsl.ac.uk$path"
            }
        }
    }
    return $results | Select-Object -Unique -Property Source, Slug, Path, Url
}

function Select-BestDslResult {
    param(
        [string]$Term,
        [array]$Results
    )
    if (-not $Results -or $Results.Count -eq 0) { return $null }
    $normalized = $Term.Trim().ToLowerInvariant()

    $exactSnd = $Results | Where-Object {
        $_.Source -eq 'snd' -and $_.Slug.ToLowerInvariant() -eq $normalized
    } | Select-Object -First 1
    if ($exactSnd) { return $exactSnd }

    $stemSnd = $Results | Where-Object {
        if ($_.Source -ne 'snd') { return $false }
        $slug = $_.Slug.ToLowerInvariant() -replace '_.*$', ''
        return $normalized.StartsWith($slug) -or $slug.StartsWith($normalized)
    } | Sort-Object { $_.Slug.Length } | Select-Object -First 1
    if ($stemSnd) { return $stemSnd }

    $containsSnd = $Results | Where-Object {
        $_.Source -eq 'snd' -and $_.Slug.ToLowerInvariant().Contains($normalized)
    } | Select-Object -First 1
    if ($containsSnd) { return $containsSnd }

    $firstSnd = $Results | Where-Object { $_.Source -eq 'snd' } | Select-Object -First 1
    if ($firstSnd) { return $firstSnd }

    return ($Results | Select-Object -First 1)
}

function Get-DslEntryBodyText {
    param([string]$Html)
    if (-not ($Html -match '(?s)<div id="entry"[^>]*>(.*)</div>\s*<div id="footer"')) {
        return ""
    }
    $entryText = ConvertFrom-HtmlText -Html $Matches[1]
    if ($entryText -match '(?s)\]\s*([A-Z][A-Z\s,.\-''()]+\s+[a-z]\s*\.)') {
        $entryText = $entryText.Substring($entryText.IndexOf($Matches[1]))
    }
    return $entryText.Trim()
}

function Get-DslEntryDetails {
    param([string]$Url)
    $response = Invoke-DslGet -Uri $Url
    $html = $response.Content
    $hasError = $html.Contains('Unfortunately there is an error in the source file')
    $title = $null
    if ($html -match '<title>[^<]*::\s*([^<]+)</title>') {
        $title = $Matches[1].Trim()
    }
    $headings = @([regex]::Matches($html, '<h3>([^<]+)</h3>') | ForEach-Object {
        ($_.Groups[1].Value -replace '<[^>]+>', '').Trim()
    } | Where-Object { $_ -and $_ -notin @('Browse SND:', 'Share:') })
    $entryText = Get-DslEntryBodyText -Html $html
    $scotsForms = @()
    if ($entryText -match 'For Sc\. forms see\s+([A-Za-z][A-Za-z\-]*)') {
        $scotsForms += $Matches[1]
    }
    foreach ($match in [regex]::Matches($entryText, 'For Sc\. forms see\s+([A-Za-z][A-Za-z\-]*)')) {
        $scotsForms += $match.Groups[1].Value
    }
    $scotsForms = $scotsForms | Select-Object -Unique
    $senseSnippets = @()
    if ($entryText -match 'Sc\. usages:') {
        $afterUsages = $entryText.Split('Sc. usages:', 2)[1]
        if ($afterUsages) {
            $chunks = $afterUsages -split '\.(?=\s*\d+\.|\s*Eng\.)' | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -gt 20 }
            $senseSnippets = @($chunks | Select-Object -First 5)
        }
    }
    if ($senseSnippets.Count -eq 0 -and $entryText.Length -gt 0) {
        $senseSnippets = @(($entryText -split '\.\s+' | Where-Object { $_.Length -gt 25 } | Select-Object -First 3))
    }
    return [pscustomobject]@{
        Url = $Url
        Title = $title
        HasSourceError = $hasError
        Headings = $headings
        ScotsForms = $scotsForms
        SenseSnippets = $senseSnippets
        EntryTextPreview = if ($entryText.Length -gt 500) { $entryText.Substring(0, 500) } else { $entryText }
    }
}

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $raw = [IO.File]::ReadAllText($Path, $utf8)
    if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
    return $raw | ConvertFrom-Json
}

function Write-JsonFile {
    param(
        [string]$Path,
        [object]$Object
    )
    $json = $Object | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText($Path, $json + "`n", $utf8)
}

if (-not (Test-Path -LiteralPath $TermsFile)) {
    throw "Terms file not found: $TermsFile"
}

$terms = @(Read-JsonFile -Path $TermsFile)
if ($terms.Count -eq 0) {
    throw "Terms file is empty: $TermsFile"
}

$existing = Read-JsonFile -Path $OutputFile
if ($null -eq $existing) {
    $existing = [ordered]@{
        generatedAt = (Get-Date).ToString('o')
        source = "https://dsl.ac.uk/"
        terms = @{}
    }
}
elseif ($existing -isnot [System.Collections.IDictionary] -and $existing.PSObject.Properties.Name -notcontains 'terms') {
    $existing = [ordered]@{
        generatedAt = (Get-Date).ToString('o')
        source = "https://dsl.ac.uk/"
        terms = @{}
    }
}

if (-not $existing.terms) {
    $existing.terms = @{}
}

$termMap = @{}
if ($existing.terms -is [System.Collections.IDictionary]) {
    foreach ($key in $existing.terms.Keys) { $termMap[$key] = $existing.terms[$key] }
}
else {
    foreach ($prop in $existing.terms.PSObject.Properties) {
        $termMap[$prop.Name] = $prop.Value
    }
}

$lookedUp = 0
$skipped = 0
$failed = 0

foreach ($term in $terms) {
    $normalizedTerm = "$term".Trim()
    if ($normalizedTerm.Length -eq 0) { continue }
    if (-not $Force -and $termMap.ContainsKey($normalizedTerm)) {
        $skipped++
        Write-Host "Skip $normalizedTerm (cached)"
        continue
    }

    Write-Host "Lookup $normalizedTerm"
    try {
        $searchResults = @(Get-DslSearchResults -Term $normalizedTerm)
        $best = Select-BestDslResult -Term $normalizedTerm -Results $searchResults
        $entry = $null
        if ($best) {
            $entry = Get-DslEntryDetails -Url $best.Url
        }

        $termMap[$normalizedTerm] = [ordered]@{
            queriedAt = (Get-Date).ToString('o')
            searchResultCount = $searchResults.Count
            searchResults = @($searchResults | Select-Object -First 8)
            selectedEntry = if ($best) { $best } else { $null }
            entry = $entry
        }
        $lookedUp++

        $existing.generatedAt = (Get-Date).ToString('o')
        $existing.terms = $termMap
        Write-JsonFile -Path $OutputFile -Object $existing
    }
    catch {
        $failed++
        Write-Warning "Failed to look up '$normalizedTerm': $($_.Exception.Message)"
        $termMap[$normalizedTerm] = [ordered]@{
            queriedAt = (Get-Date).ToString('o')
            error = $_.Exception.Message
        }
        $existing.generatedAt = (Get-Date).ToString('o')
        $existing.terms = $termMap
        Write-JsonFile -Path $OutputFile -Object $existing
    }

    if ($DelayBetweenRequestsMs -gt 0) {
        Start-Sleep -Milliseconds $DelayBetweenRequestsMs
    }
}

Write-Host "DSL lookup complete. Looked up: $lookedUp; skipped: $skipped; failed: $failed."
Write-Host "Output: $OutputFile"
