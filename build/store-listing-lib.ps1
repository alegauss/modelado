<#
.SYNOPSIS
    MOD92: reads build\msix\store-listing.md, the source of the Store listing's text in every language.

.DESCRIPTION
    Dot-sourced by store-shots-vm.ps1 and store-listing-csv.ps1, so both read the same text the same
    way. After claude-tray's T412. Each "## <Language> (xx-yy)" section becomes one entry: the short
    description, the description, the features, the screenshots (headline with its stressed words in
    [brackets], the line under it, and the Store's caption, which is the headline without brackets),
    the keywords, the copyright line and the label under the photo card. A section out of shape stops
    with its language named.
#>

function Read-StoreListing {
    $utf8 = New-Object Text.UTF8Encoding $false
    $md = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'msix\store-listing.md'), $utf8) -replace "`r`n", "`n"
    $langs = [ordered]@{}
    foreach ($m in [regex]::Matches($md, '(?ms)^## [^\n]*\(([a-z]{2}-[a-z]{2})\)\n(.*?)(?=^## |\z)')) {
        $code = $m.Groups[1].Value
        $body = $m.Groups[2].Value
        # Blocks under a bold-only heading, in this order: short description, description, features,
        # screenshot captions.
        $blocks = @([regex]::Matches($body, '(?ms)^\*\*[^*\n:]+\*\*\n\n(.*?)(?=^\*\*|\z)') | ForEach-Object { $_.Groups[1].Value.Trim() })
        # "**Label:** value" lines, in this order: keywords, copyright, photo label, and the trailer's
        # (MOD84) line over the viewer's modes and closing line.
        $inline = @([regex]::Matches($body, '(?m)^\*\*[^*\n]+?:\*\* (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() })
        if ($blocks.Count -ne 4 -or $inline.Count -ne 5) { throw "store-listing.md: section $code is out of shape ($($blocks.Count) headed blocks of 4, $($inline.Count) labels of 5)" }
        $views = $inline[3] -split ' \| ', 2
        if ($views.Count -ne 2) { throw "store-listing.md: $code's trailer views line has no ' | ' before its smaller line" }

        $shots = @(($blocks[3] -split "\n") | Where-Object { $_ -match '^\d+\. ' } | ForEach-Object {
                $line = $_ -replace '^\d+\. ', ''
                $parts = $line -split ' \| ', 2
                if ($parts.Count -ne 2) { throw "store-listing.md: $code caption '$line' has no ' | ' before its smaller line" }
                [pscustomobject]@{
                    Headline = $parts[0].Trim()
                    Subline  = $parts[1].Trim()
                    Caption  = ($parts[0] -replace '[\[\]]', '').Trim()
                }
            })

        # The file wraps paragraphs at 100 columns; on the Store each paragraph is one line. A line of
        # Chinese has no spaces to join with.
        $join = if ($code -like 'zh-*') { '' } else { ' ' }
        $paras = ($blocks[1] -split "\n\n") | ForEach-Object { ($_ -split "\n") -join $join }
        $langs[$code] = [pscustomobject]@{
            Short       = (($blocks[0] -split "\n") -join $join)
            Description = ($paras -join "`r`n`r`n")
            Features    = @(($blocks[2] -split "\n") | Where-Object { $_ -match '^- ' } | ForEach-Object { $_.Substring(2).Trim() })
            Shots       = $shots
            Keywords    = @($inline[0] -split ',\s*')
            Copyright   = $inline[1]
            PhotoLabel  = $inline[2]
            Views       = [pscustomobject]@{ Headline = $views[0].Trim(); Subline = $views[1].Trim() }
            TrailerEnd  = $inline[4]
        }
    }
    if ($langs.Count -eq 0) { throw 'store-listing.md has no language section' }
    return $langs
}
