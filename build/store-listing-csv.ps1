<#
.SYNOPSIS
    MOD92: fills Partner Center's listing CSV from build\msix\store-listing.md, every language at once.

.DESCRIPTION
    After claude-tray's T412. Partner Center exports the listings as a CSV, one column per language, and
    imports the same shape back. This reads the exported CSV and fills each language's column with the
    title, description, short description, features, keywords, copyright, developer name and the
    caption of each screenshot that language already has. The text lives in the .md alone; the CSV is
    made, never edited by hand.

    Images: the import refuses an image named by file, although its type column says a relative path
    is allowed (measured twice in claude-tray, 30-31/10/2026); a Partner Center URL passes, even one from
    another language. So the screenshots go up on each language's page, and -ImagesFrom copies the
    logo and hero art URLs from the language they were uploaded in to the others.

      powershell -NoProfile -File build\store-listing-csv.ps1 -Template <listingData-...csv> [-ImagesFrom en-us]

    Add the five languages under Store listings > Manage additional languages before exporting, so the
    CSV has a column for each.
#>
param(
    [Parameter(Mandatory = $true)][string]$Template,
    [string]$Out,
    [string]$ImagesFrom
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Out) { $Out = Join-Path $root 'artifacts\store-listing' }
. (Join-Path $PSScriptRoot 'store-listing-lib.ps1')
$langs = Read-StoreListing

$rows = Import-Csv $Template -Encoding UTF8
$columns = $rows[0].PSObject.Properties.Name
if ($ImagesFrom -and -not ($columns -contains $ImagesFrom)) { throw "The CSV has no $ImagesFrom column" }
$missing = @($langs.Keys | Where-Object { $columns -notcontains $_ })
if ($missing.Count) { Write-Host "No column for $($missing -join ', '): add them under Manage additional languages and export again." }

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory -Force $Out | Out-Null

# How many screenshots each language has, so only their captions are filled.
$shotCount = @{}
foreach ($code in $langs.Keys) {
    if ($columns -contains $code) {
        $shotCount[$code] = @($rows | Where-Object { $_.Field -match '^DesktopScreenshot\d+$' -and $_.$code }).Count
    }
}

$shared = '^(StoreLogo\w+|PromoImage\w+|OverrideLogosForWin10)$'
foreach ($row in $rows) {
    $f = $row.Field
    foreach ($code in $langs.Keys) {
        if (-not ($columns -contains $code)) { continue }
        $l = $langs[$code]
        $value = $null
        switch -regex ($f) {
            '^Title$'                         { $value = 'Modelado' }
            '^Description$'                   { $value = $l.Description }
            '^ShortDescription$'              { $value = $l.Short }
            '^DevStudio$'                     { $value = 'Alegauss' }
            '^CopyrightTrademarkInformation$' { $value = $l.Copyright }
            '^Feature(\d+)$'                  { $i = [int]$Matches[1] - 1; $value = if ($i -lt $l.Features.Count) { $l.Features[$i] } else { '' } }
            '^SearchTerm(\d+)$'               { $i = [int]$Matches[1] - 1; $value = if ($i -lt $l.Keywords.Count) { $l.Keywords[$i] } else { '' } }
            '^DesktopScreenshotCaption(\d+)$' { $i = [int]$Matches[1] - 1; $value = if ($i -lt $shotCount[$code] -and $i -lt $l.Shots.Count) { $l.Shots[$i].Caption } else { '' } }
        }
        if ($ImagesFrom -and $code -ne $ImagesFrom -and $f -match $shared) { $value = $row.$ImagesFrom }
        if ($null -ne $value) { $row.$code = $value }
    }
}

# Written in the exact shape Partner Center exports, not with ConvertTo-Csv: the import refused, without
# saying why, the file ConvertTo-Csv writes, which quotes every field and the header. Here: quotes only
# where a field needs them, LF inside a field, CRLF between rows, a BOM, and no line break at the end.
function Cell([string]$v) {
    if ($null -eq $v) { return '' }
    $v = $v -replace "`r`n", "`n"
    if ($v -match '[",\n]') { return '"' + $v.Replace('"', '""') + '"' }
    return $v
}
$lines = @(($columns | ForEach-Object { Cell $_ }) -join ',')
foreach ($row in $rows) { $lines += (($columns | ForEach-Object { Cell $row.$_ }) -join ',') }
$file = Join-Path $Out (Split-Path -Leaf $Template)
[IO.File]::WriteAllText($file, ($lines -join "`r`n"), (New-Object Text.UTF8Encoding $true))
Write-Host "listing: $file"
