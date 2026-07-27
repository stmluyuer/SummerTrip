param(
    [string]$Manifest = (Join-Path $PSScriptRoot "..\migration\manifests\assets.tsv"),
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Manifest)) {
    throw "Missing manifest: $Manifest"
}

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Missing repository root: $RepositoryRoot"
}

$rows = @(Import-Csv -LiteralPath $Manifest -Delimiter "`t")
if ($rows.Count -eq 0) {
    throw "Asset manifest has no data rows: $Manifest"
}

$expectedHeaders = @(
    "source_path",
    "target_path",
    "category",
    "priority",
    "action",
    "status",
    "notes"
)
$actualHeaders = @($rows[0].PSObject.Properties.Name)
$missingHeaders = @($expectedHeaders | Where-Object { $_ -notin $actualHeaders })
if ($missingHeaders.Count -gt 0) {
    throw "Asset manifest is missing required columns: $($missingHeaders -join ', ')"
}

$allowedPriorities = @("P0", "P1", "P2", "-")
$allowedActions = @("copy", "review", "defer", "exclude")
$allowedStatuses = @("active", "unreferenced", "optional", "superseded", "platform", "source", "unused")
$errors = @()
$repositoryRootPath = [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $RepositoryRoot).Path)
$repositoryRootPrefix = $repositoryRootPath.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar

foreach ($row in $rows) {
    if ($row.priority -notin $allowedPriorities) {
        $errors += "Invalid priority '$($row.priority)' for '$($row.source_path)'."
    }
    if ($row.action -notin $allowedActions) {
        $errors += "Invalid action '$($row.action)' for '$($row.source_path)'."
    }
    if ($row.status -notin $allowedStatuses) {
        $errors += "Invalid status '$($row.status)' for '$($row.source_path)'."
    }

    if ($row.priority -eq "P0" -and ($row.action -ne "copy" -or $row.status -ne "active")) {
        $errors += "P0 row must be an active copy: '$($row.source_path)'."
    }

    if ($row.action -ne "copy") {
        continue
    }

    if ([string]::IsNullOrWhiteSpace($row.source_path)) {
        $errors += "Copy row has an empty source_path."
        continue
    }
    if ($row.source_path -match "[*?;]") {
        $errors += "Copy row must use one exact source_path: '$($row.source_path)'."
        continue
    }
    if ([string]::IsNullOrWhiteSpace($row.target_path) -or -not $row.target_path.StartsWith("res://")) {
        $errors += "Copy row must use a res:// target_path: '$($row.source_path)'."
    }

    $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $repositoryRootPath $row.source_path))
    if (-not $sourcePath.StartsWith($repositoryRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $errors += "Copy source escapes the repository root: '$($row.source_path)'."
        continue
    }
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        $errors += "Missing copy source: '$($row.source_path)'."
    }
}

$duplicateSources = @(
    $rows |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.source_path) } |
        Group-Object source_path |
        Where-Object { $_.Count -gt 1 }
)
foreach ($duplicate in $duplicateSources) {
    $errors += "Duplicate source_path: '$($duplicate.Name)'."
}

$duplicateTargets = @(
    $rows |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.target_path) } |
        Group-Object target_path |
        Where-Object { $_.Count -gt 1 }
)
foreach ($duplicate in $duplicateTargets) {
    $errors += "Duplicate target_path: '$($duplicate.Name)'."
}

if ($errors.Count -gt 0) {
    throw ("Asset manifest validation failed:`n" + ($errors -join "`n"))
}

$copyRows = @($rows | Where-Object { $_.action -eq "copy" })
$p0Rows = @($rows | Where-Object { $_.priority -eq "P0" })
Write-Host (
    "Validated {0} manifest rows; {1} exact copy sources exist; {2} P0 rows are active copies." -f
        $rows.Count,
        $copyRows.Count,
        $p0Rows.Count
)
