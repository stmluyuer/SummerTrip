param(
    [string]$Manifest = (Join-Path $PSScriptRoot "..\migration\manifests\assets.tsv"),
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot ".."),
    [string]$LicenseManifest = (Join-Path $PSScriptRoot "..\migration\manifests\asset_licenses.tsv"),
    [switch]$RequireVerifiedLicenses
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Manifest)) {
    throw "Missing manifest: $Manifest"
}

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Missing repository root: $RepositoryRoot"
}

if (-not (Test-Path -LiteralPath $LicenseManifest -PathType Leaf)) {
    throw "Missing asset license manifest: $LicenseManifest"
}

$rows = @(Import-Csv -LiteralPath $Manifest -Delimiter "`t")
if ($rows.Count -eq 0) {
    throw "Asset manifest has no data rows: $Manifest"
}

$licenseRows = @(Import-Csv -LiteralPath $LicenseManifest -Delimiter "`t")
if ($licenseRows.Count -eq 0) {
    throw "Asset license manifest has no data rows: $LicenseManifest"
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

$expectedLicenseHeaders = @(
    "source_path",
    "author",
    "source",
    "license_expression",
    "redistribution_scope",
    "verification_status",
    "evidence",
    "notes"
)
$actualLicenseHeaders = @($licenseRows[0].PSObject.Properties.Name)
$missingLicenseHeaders = @($expectedLicenseHeaders | Where-Object { $_ -notin $actualLicenseHeaders })
if ($missingLicenseHeaders.Count -gt 0) {
    throw "Asset license manifest is missing required columns: $($missingLicenseHeaders -join ', ')"
}

$allowedPriorities = @("P0", "P1", "P2", "-")
$allowedActions = @("copy", "review", "defer", "exclude")
$allowedStatuses = @("active", "unreferenced", "optional", "superseded", "platform", "source", "unused")
$allowedVerificationStatuses = @("unverified", "verified")
$allowedRedistributionScopes = @("local_validation_only", "conditional", "redistributable")
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

$assetRowsBySource = @{}
foreach ($row in $rows) {
    if (
        -not [string]::IsNullOrWhiteSpace($row.source_path) -and
        -not $assetRowsBySource.ContainsKey($row.source_path)
    ) {
        $assetRowsBySource[$row.source_path] = $row
    }
}

$licenseRowsBySource = @{}
foreach ($licenseRow in $licenseRows) {
    $licensePath = $licenseRow.source_path
    if ([string]::IsNullOrWhiteSpace($licensePath)) {
        $errors += "Asset license row has an empty source_path."
        continue
    }
    if ($licensePath -match "[*?;]") {
        $errors += "Asset license row must use one exact source_path: '$licensePath'."
        continue
    }

    foreach ($field in @("author", "source", "license_expression", "redistribution_scope", "verification_status", "evidence")) {
        if ([string]::IsNullOrWhiteSpace($licenseRow.$field)) {
            $errors += "Asset license row '$licensePath' has an empty $field."
        }
    }

    if ($licenseRow.verification_status -notin $allowedVerificationStatuses) {
        $errors += "Invalid verification_status '$($licenseRow.verification_status)' for '$licensePath'."
    }
    if ($licenseRow.redistribution_scope -notin $allowedRedistributionScopes) {
        $errors += "Invalid redistribution_scope '$($licenseRow.redistribution_scope)' for '$licensePath'."
    }
    if (
        $licenseRow.verification_status -eq "unverified" -and
        $licenseRow.redistribution_scope -ne "local_validation_only"
    ) {
        $errors += "Unverified asset must be local_validation_only: '$licensePath'."
    }
    if ($licenseRow.verification_status -eq "verified") {
        if ($licenseRow.author -eq "UNKNOWN" -or $licenseRow.source -eq "UNKNOWN") {
            $errors += "Verified asset must name its author and source: '$licensePath'."
        }
        if ($licenseRow.license_expression -eq "NOASSERTION") {
            $errors += "Verified asset must have a license expression: '$licensePath'."
        }
        if ($licenseRow.evidence -eq "NONE") {
            $errors += "Verified asset must reference evidence: '$licensePath'."
        }
    }

    if (-not $assetRowsBySource.ContainsKey($licensePath)) {
        $errors += "Asset license row does not match assets.tsv: '$licensePath'."
    }
    elseif ($assetRowsBySource[$licensePath].action -ne "copy") {
        $errors += "Asset license row must describe a copy asset: '$licensePath'."
    }

    if (-not $licenseRowsBySource.ContainsKey($licensePath)) {
        $licenseRowsBySource[$licensePath] = $licenseRow
    }
}

$duplicateLicenseSources = @(
    $licenseRows |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.source_path) } |
        Group-Object source_path |
        Where-Object { $_.Count -gt 1 }
)
foreach ($duplicate in $duplicateLicenseSources) {
    $errors += "Duplicate asset license source_path: '$($duplicate.Name)'."
}

$p0Rows = @($rows | Where-Object { $_.priority -eq "P0" })
foreach ($p0Row in $p0Rows) {
    if (-not $licenseRowsBySource.ContainsKey($p0Row.source_path)) {
        $errors += "P0 asset is missing an asset license row: '$($p0Row.source_path)'."
    }
}

$unverifiedLicenseRows = @($licenseRows | Where-Object { $_.verification_status -ne "verified" })
if ($RequireVerifiedLicenses -and $unverifiedLicenseRows.Count -gt 0) {
    $errors += (
        "Release verification requires all asset licenses to be verified; {0} row(s) remain unverified." -f
            $unverifiedLicenseRows.Count
    )
}

if ($errors.Count -gt 0) {
    throw ("Asset manifest validation failed:`n" + ($errors -join "`n"))
}

$copyRows = @($rows | Where-Object { $_.action -eq "copy" })
$verifiedLicenseRows = @($licenseRows | Where-Object { $_.verification_status -eq "verified" })
Write-Host (
    (
        "Validated {0} manifest rows; {1} exact copy sources exist; {2} P0 rows are active copies; " +
        "{3} license rows cover every P0 asset ({4} verified, {5} unverified)."
    ) -f
        $rows.Count,
        $copyRows.Count,
        $p0Rows.Count,
        $licenseRows.Count,
        $verifiedLicenseRows.Count,
        $unverifiedLicenseRows.Count
)
