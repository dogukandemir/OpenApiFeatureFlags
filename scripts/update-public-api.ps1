<#
.SYNOPSIS
    Keeps a project's PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt in sync with its actual surface.

.DESCRIPTION
    The public API of this repository is a permanent contract, enforced by
    Microsoft.CodeAnalysis.PublicApiAnalyzers. Keeping the file up to date by hand is tedious because
    records alone contribute operators, Deconstruct, <Clone>$ and PrintMembers entries.

    The analyzer diagnostics carry the exact file text, so this script reads them rather than trying
    to format entries itself:

        RS0016 "not part of the declared public API"  -> the reported text is appended
        RS0017 "is part of the declared API, but ..." -> the reported text is removed

    Run it after adding or removing public surface. The resulting diff is part of the change.

.EXAMPLE
    ./scripts/update-public-api.ps1 -ProjectDir src/OpenApiFeatureFlags.Swashbuckle
#>
param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$ProjectDir,
    [int]$MaxPasses = 6
)

$shipped = Join-Path $RepoRoot (Join-Path $ProjectDir 'PublicAPI.Shipped.txt')
$unshipped = Join-Path $RepoRoot (Join-Path $ProjectDir 'PublicAPI.Unshipped.txt')

foreach ($file in @($shipped, $unshipped)) {
    if (-not (Test-Path $file)) {
        # RS0037: nullability annotations are only recorded when this directive is present.
        Set-Content -Path $file -Value '#nullable enable' -Encoding utf8
    }
}

for ($pass = 1; $pass -le $MaxPasses; $pass++) {
    $output = (& dotnet build (Join-Path $RepoRoot $ProjectDir) --nologo -v q 2>&1 | Out-String)

    $missing = [regex]::Matches($output, "error RS0016: Symbol '(.*?)' is not part of the declared public API") |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

    $stale = [regex]::Matches($output, "error RS0017: Symbol '(.*?)' is part of the declared API") |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

    if ((-not $missing -or $missing.Count -eq 0) -and (-not $stale -or $stale.Count -eq 0)) {
        $other = [regex]::Matches($output, 'error [A-Z]+\d+') | ForEach-Object { $_.Value } | Sort-Object -Unique
        Write-Host "Pass $pass : $ProjectDir is in sync. Other errors: $($other -join ', ')"
        break
    }

    if ($stale -and $stale.Count -gt 0) {
        Write-Host "Pass $pass : removing $($stale.Count) stale entr(ies)"
        $kept = Get-Content -Path $unshipped | Where-Object { $stale -notcontains $_ }
        Set-Content -Path $unshipped -Value $kept -Encoding utf8
    }

    if ($missing -and $missing.Count -gt 0) {
        Write-Host "Pass $pass : appending $($missing.Count) entr(ies)"
        Add-Content -Path $unshipped -Value $missing -Encoding utf8
    }
}
