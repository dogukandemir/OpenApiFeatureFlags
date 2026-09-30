<#
.SYNOPSIS
    Builds the Release packages into the local feed folder described in docs/trying-locally.md.

.DESCRIPTION
    Nothing is published to nuget.org yet, so this is how the packages get consumed locally. The
    output folder is gitignored: it is build output, not source.

    NuGet caches by version, so re-packing the same version after a code change may leave a consumer
    resolving the previous copy. Use -VersionSuffix to produce a distinctly-versioned set instead,
    which avoids both that cache and any later clash with a real release of the same version.

    The version comes from VersionPrefix in Directory.Build.props, so this script never has to be
    edited when the version moves.

.EXAMPLE
    ./scripts/pack-local.ps1
    Produces the packages at the version in Directory.Build.props in ./local-packages.

.EXAMPLE
    ./scripts/pack-local.ps1 -VersionSuffix local
    Produces the same packages with a -local suffix, for example 0.1.0-local.
#>
param(
    [string]$Configuration = 'Release',
    [string]$Output = 'local-packages',
    [string]$VersionSuffix
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoRoot $Output

# Single source of truth for the version: the packages are built from this value, so it is also what
# the consumer has to reference. Read, never duplicated.
[xml]$buildProps = Get-Content (Join-Path $repoRoot 'Directory.Build.props')
$versionPrefix = @($buildProps.Project.PropertyGroup.VersionPrefix | Where-Object { $_ }) | Select-Object -First 1

if (-not $versionPrefix) {
    throw 'Could not read VersionPrefix from Directory.Build.props.'
}

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

$packArgs = @('pack', '-c', $Configuration, '-o', $outputPath, '--nologo')

if ($VersionSuffix) {
    $packArgs += "-p:VersionSuffix=$VersionSuffix"
}

Push-Location $repoRoot
try {
    # Pack the projects under src/ rather than the solution: the solution also contains the sample,
    # which is deliberately not packable and would emit a nuisance warning for every run.
    $projects = Get-ChildItem (Join-Path $repoRoot 'src') -Directory |
        ForEach-Object { Get-ChildItem $_.FullName -Filter '*.csproj' } |
        Sort-Object FullName

    foreach ($project in $projects) {
        & dotnet @packArgs $project.FullName | Out-Null

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed for $($project.Name) with exit code $LASTEXITCODE"
        }

        Write-Host "  packed $($project.Name)"
    }
}
finally {
    Pop-Location
}

$feedPath = (Resolve-Path $outputPath).Path
$packages = Get-ChildItem $outputPath -Filter *.nupkg | Where-Object { $_.Name -notlike '*.snupkg' } | Sort-Object Name

Write-Host ''
Write-Host "Feed: $feedPath"
Write-Host ''
foreach ($package in $packages) {
    Write-Host ("  {0}" -f $package.Name)
}

$version = if ($VersionSuffix) { "$versionPrefix-$VersionSuffix" } else { $versionPrefix }

Write-Host ''
Write-Host 'To consume it, copy this next to the consuming solution:'
Write-Host ''
Write-Host '  <?xml version="1.0" encoding="utf-8"?>'
Write-Host '  <configuration>'
Write-Host '    <packageSources>'
Write-Host '      <clear />'
Write-Host ("      <add key=`"local`" value=`"{0}`" />" -f $feedPath)
Write-Host '      <!-- Keep nuget.org: the packages'' own dependencies live there. -->'
Write-Host '      <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />'
Write-Host '    </packageSources>'
Write-Host '  </configuration>'
Write-Host ''
Write-Host ("Then reference OpenApiFeatureFlags*.{0} and restore." -f $version)
