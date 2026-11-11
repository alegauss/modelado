<#
.SYNOPSIS
    MOD81, MOD82, MOD84, MOD92: sends the listings, screenshots, hero art and trailers to the Microsoft
    Store through the submission API.

.DESCRIPTION
    After claude-tray's build\store-submit.ps1 (T412). The first submission of an app is made on the
    Partner Center page; later ones can be made here, the API cloning the last published. This:
      1) takes a Microsoft Entra token for the Entra app with the Manager role in Partner Center, from
         STORE_TENANT_ID, STORE_CLIENT_ID and STORE_CLIENT_SECRET (user variables, never the repository);
      2) creates a submission (or reuses a pending one the API itself created);
      3) writes every language of build\msix\store-listing.md (store-listing-lib.ps1) into its listing,
         adding the languages the package does not declare; replaces each listing's screenshots with
         artifacts\store-shots\<language>, captioned; puts the 16:9 Super hero art in every listing and
         the poster, box art and tile in the new ones; adds one trailer per language from
         artifacts\store-trailer\<language>, with its thumbnail;
      4) uploads the files as one ZIP to the URL the submission gives;
      5) with -Commit only, commits the submission, which sends it to certification, and follows it.

    Without -Commit the submission stays pending in Partner Center, where it can be read before it is
    sent, and a later run reuses it.

      powershell -NoProfile -File build\store-submit.ps1 -DryRun   # reads only: credentials, app, submissions
      powershell -NoProfile -File build\store-submit.ps1           # prepares and uploads, does not send
      powershell -NoProfile -File build\store-submit.ps1 -Commit   # and sends it to certification
#>
[CmdletBinding()]
param(
    [string]$AppId = '9PB86WW5NRR2',
    [switch]$DryRun,
    [switch]$Commit,
    # Only commits the pending submission as it is, after a run without -Commit was read and approved.
    [switch]$CommitOnly
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'store-listing-lib.ps1')
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Env([string]$name) {
    $v = [Environment]::GetEnvironmentVariable($name, 'Process')
    if (-not $v) { $v = [Environment]::GetEnvironmentVariable($name, 'User') }
    if (-not $v) { throw "$name is not set (see the top of build\store-submit.ps1)" }
    return $v
}

# --- 1) Token --------------------------------------------------------------------------------------------
$token = (Invoke-RestMethod -Method Post -Uri "https://login.microsoftonline.com/$(Env 'STORE_TENANT_ID')/oauth2/token" -Body @{
        grant_type    = 'client_credentials'
        client_id     = (Env 'STORE_CLIENT_ID')
        client_secret = (Env 'STORE_CLIENT_SECRET')
        resource      = 'https://manage.devcenter.microsoft.com'
    }).access_token
$headers = @{ Authorization = "Bearer $token" }
$base = "https://manage.devcenter.microsoft.com/v1.0/my/applications/$AppId"

function Api([string]$method, [string]$uri, $body) {
    $req = @{ Method = $method; Uri = $uri; Headers = $headers }
    # The API refuses even a bodiless POST that does not say JSON (claude-tray, measured), so every POST
    # and PUT goes as JSON, empty when there is nothing to send.
    if ($method -in 'Post', 'Put') {
        $json = if ($null -ne $body) { $body | ConvertTo-Json -Depth 50 } else { '' }
        $req.Body = [Text.Encoding]::UTF8.GetBytes($json)
        $req.ContentType = 'application/json; charset=utf-8'
    }
    return Invoke-RestMethod @req
}

function Follow([string]$id) {
    do {
        Start-Sleep -Seconds 20
        $status = Api 'Get' "$base/submissions/$id/status"
        Write-Host "  $($status.status)"
    } while ($status.status -eq 'CommitStarted')
    if ($status.status -eq 'CommitFailed') {
        $status.statusDetails.errors | ForEach-Object { Write-Host "  error: $($_.code) - $($_.details)" }
        throw 'The commit failed; the submission stays pending in Partner Center.'
    }
    $status.statusDetails.warnings | ForEach-Object { if ($_) { Write-Host "  warning: $($_.code) - $($_.details)" } }
    Write-Host "status: $($status.status) - certification goes on in Partner Center"
}

# --- 2) Submission ---------------------------------------------------------------------------------------
$app = Api 'Get' $base
Write-Host "app: $($app.primaryName) ($AppId)"
$pendingId = $app.pendingApplicationSubmission.id
if ($pendingId) { Write-Host "pending submission: $pendingId" } else { Write-Host 'no pending submission' }

if ($DryRun) {
    $published = Api 'Get' "$base/submissions/$($app.lastPublishedApplicationSubmission.id)"
    Write-Host "last published: $($published.id), status $($published.status)"
    foreach ($p in $published.listings.PSObject.Properties) {
        $images = @($p.Value.baseListing.images)
        $types = ($images | Group-Object imageType | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ', '
        Write-Host "  $($p.Name): $types"
    }
    Write-Host "  trailers: $(@($published.trailers).Count)"
    if ($pendingId) { Write-Host "  pending status: $((Api 'Get' "$base/submissions/$pendingId").status)" }
    Write-Host 'DryRun: nothing was changed.'
    return
}

if ($CommitOnly) {
    if (-not $pendingId) { throw 'No pending submission to commit.' }
    Api 'Post' "$base/submissions/$pendingId/commit" $null | Out-Null
    Write-Host "commit asked for $pendingId; following..."
    Follow $pendingId
    return
}

if ($pendingId) {
    $sub = Api 'Get' "$base/submissions/$pendingId"
    if ($sub.status -ne 'PendingCommit') { throw "The pending submission is '$($sub.status)'; only one in PendingCommit can be edited." }
    Write-Host 'reusing it: a pending submission made on the Partner Center page is refused by the API; delete it there and run again'
} else {
    $sub = Api 'Post' "$base/submissions"
    Write-Host "submission created: $($sub.id)"
}

# --- 3) Listings, images and trailers --------------------------------------------------------------------
$langs = Read-StoreListing
$template = $sub.listings.'en-us'.baseListing
if (-not $template) { throw 'The submission has no en-us listing to take the URLs and contacts from.' }

$stage = Join-Path $repo 'artifacts\store-submit'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
$zipPath = Join-Path $stage 'upload.zip'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($zipPath, 'Create')
function Pack([string]$file, [string]$entry) {
    if (-not (Test-Path $file)) { throw "No $file" }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $entry, 'NoCompression')
    return $entry
}
function Image([string]$entry, [string]$type, [string]$description) {
    [pscustomobject]@{ fileName = $entry; fileStatus = 'PendingUpload'; imageType = $type; description = $description }
}

$art = Join-Path $repo 'artifacts\store-art'
$trailers = @()
try {
    foreach ($code in $langs.Keys) {
        $l = $langs[$code]
        $listing = $sub.listings.PSObject.Properties[$code]
        $isNew = -not $listing
        if ($isNew) {
            $listing = [pscustomobject]@{ baseListing = [pscustomobject]@{}; platformOverrides = [pscustomobject]@{} }
            $sub.listings | Add-Member -NotePropertyName $code -NotePropertyValue $listing
            $bl = $listing.baseListing
            foreach ($field in 'privacyPolicy', 'websiteUrl', 'supportContact', 'licenseTerms') {
                $bl | Add-Member -NotePropertyName $field -NotePropertyValue $template.$field -Force
            }
            $bl | Add-Member -NotePropertyName images -NotePropertyValue @() -Force
        } else {
            $bl = $listing.Value.baseListing
        }

        foreach ($pair in @(
                @('title', 'Modelado'),
                @('description', $l.Description),
                @('shortDescription', $l.Short),
                @('features', [object[]]@($l.Features)),
                @('keywords', [object[]]@($l.Keywords)),
                @('copyrightAndTrademarkInfo', $l.Copyright),
                @('devStudio', 'Alegauss'))) {
            $bl | Add-Member -NotePropertyName $pair[0] -NotePropertyValue $pair[1] -Force
        }

        # Every screenshot and every Super hero art already there goes; the logos of an existing listing stay.
        $kept = @($bl.images | Where-Object { $_ } | ForEach-Object {
                if ($_.imageType -in 'Screenshot', 'PromotionalArt16x9') { $_.fileStatus = 'PendingDelete' }
                $_
            })
        $added = @()
        for ($i = 0; $i -lt $l.Shots.Count; $i++) {
            $entry = Pack (Join-Path $repo "artifacts\store-shots\$code\screenshot-$($i + 1).png") "$code/screenshot-$($i + 1).png"
            $added += Image $entry 'Screenshot' $l.Shots[$i].Caption
        }
        $added += Image (Pack (Join-Path $art 'hero-16x9-3840x2160.png') "$code/hero-16x9.png") 'PromotionalArt16x9' ''
        if ($isNew) {
            $added += Image (Pack (Join-Path $art 'poster-9x16-1440x2160.png') "$code/poster-9x16.png") 'StoreLogo9x16' ''
            $added += Image (Pack (Join-Path $art 'box-1x1-2160x2160.png') "$code/box-1x1.png") 'StoreLogoSquare' ''
            $added += Image (Pack (Join-Path $art 'tile-300x300.png') "$code/tile-300.png") 'Icon' ''
        }
        $bl | Add-Member -NotePropertyName images -NotePropertyValue ([object[]](@($kept) + @($added))) -Force

        # One trailer per language: its own video, its thumbnail, shown only in that language's listing.
        $video = Pack (Join-Path $repo "artifacts\store-trailer\$code\trailer.mp4") "trailers/$code.mp4"
        $thumb = Pack (Join-Path $repo "artifacts\store-trailer\$code\trailer.png") "trailers/$code.png"
        $assets = [pscustomobject]@{}
        $assets | Add-Member -NotePropertyName $code -NotePropertyValue ([pscustomobject]@{
                title     = 'Modelado'
                imageList = [object[]]@([pscustomobject]@{ fileName = $thumb; description = 'Modelado' })
            })
        $trailers += [pscustomobject]@{ videoFileName = $video; trailerAssets = $assets }

        Write-Host ("  {0}: {1} listing, {2} screenshots, hero art{3}, trailer" -f $code, $(if ($isNew) { 'new' } else { 'updated' }), $l.Shots.Count, $(if ($isNew) { ', logos' } else { '' }))
    }
}
finally { $zip.Dispose() }

# The trailers the clone carried are replaced by these; a trailer is removed by leaving it out.
$sub | Add-Member -NotePropertyName trailers -NotePropertyValue ([object[]]$trailers) -Force
$sub = Api 'Put' "$base/submissions/$($sub.id)" $sub
Write-Host "submission $($sub.id) updated: $(@($sub.listings.PSObject.Properties).Count) listings, $(@($sub.trailers).Count) trailers"

# --- 4) Files --------------------------------------------------------------------------------------------
# x-ms-version raises the single-request blob limit past the 64 MB of the oldest service version.
$size = [int]((Get-Item $zipPath).Length / 1MB)
Write-Host "uploading $size MB..."
Invoke-WebRequest -Method Put -Uri $sub.fileUploadUrl -InFile $zipPath -UseBasicParsing `
    -Headers @{ 'x-ms-blob-type' = 'BlockBlob'; 'x-ms-version' = '2020-04-08' } | Out-Null
Write-Host 'files uploaded'

# --- 5) Commit -------------------------------------------------------------------------------------------
if (-not $Commit) {
    Write-Host "Not sent: the submission waits in Partner Center as $($sub.id). Read it there, then run with -CommitOnly."
    return
}
Api 'Post' "$base/submissions/$($sub.id)/commit" $null | Out-Null
Write-Host 'commit asked; following...'
Follow $sub.id
