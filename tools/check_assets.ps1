param(
    [string]$Manifest = "docs/resource_manifest.tsv",
    [string]$GameRoot = "game"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Manifest)) {
    throw "Missing manifest: $Manifest"
}

$rows = Import-Csv -LiteralPath $Manifest -Delimiter "`t"
$missing = @()

foreach ($row in $rows) {
    if (-not $row.path -or $row.path -eq "path") {
        continue
    }

    $assetPath = Join-Path $GameRoot $row.path
    if (-not (Test-Path -LiteralPath $assetPath)) {
        $missing += $assetPath
    }
}

if ($missing.Count -gt 0) {
    Write-Error ("Missing asset paths:`n" + ($missing -join "`n"))
    exit 1
}

$active = $rows | Where-Object { $_.status -eq "active" }
Write-Host ("Checked {0} manifest rows; {1} active assets exist." -f $rows.Count, $active.Count)
