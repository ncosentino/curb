#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][ValidateSet('Base', 'Native')][string]$Set,
    [ValidateSet('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-arm64')]
    [string]$RuntimeIdentifier = 'linux-x64',
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$ExpectedCommit
)

$ErrorActionPreference = 'Stop'
$gh = (Get-Command gh -CommandType Application | Select-Object -First 1).Source
$packages = @(& (Join-Path $PSScriptRoot 'Get-ReleasePackages.ps1') -PackageDirectory $PackageDirectory -Set $Set -RuntimeIdentifier $RuntimeIdentifier -Version $Version)
$tag = "v$Version"
$raw = & $gh release view $tag --repo ncosentino/curb --json isDraft,targetCommitish,assets
if ($LASTEXITCODE -ne 0) { throw "Cannot inspect the release: exit $LASTEXITCODE." }
$release = $raw | ConvertFrom-Json
if (-not $release.isDraft -or $release.targetCommitish -cne $ExpectedCommit) {
    throw 'Only the expected commit in a draft fork release can receive packages.'
}
$pending = [Collections.Generic.List[string]]::new()
foreach ($package in $packages) {
    $existing = @($release.assets | Where-Object name -CEQ $package.Name)
    if ($existing.Count -eq 0) { $pending.Add($package.Path); continue }
    if ($existing.Count -ne 1 -or $existing[0].digest -cne "sha256:$($package.Sha256)" -or $existing[0].size -ne $package.Size) {
        throw 'An existing release asset differs; it will not be overwritten.'
    }
}
if ($pending.Count -gt 0) {
    & $gh release upload $tag @($pending.ToArray()) --repo ncosentino/curb
    if ($LASTEXITCODE -ne 0) { throw "Release upload failed with exit $LASTEXITCODE." }
}
$raw = & $gh release view $tag --repo ncosentino/curb --json isDraft,targetCommitish,assets
if ($LASTEXITCODE -ne 0) { throw "Cannot verify the uploaded release: exit $LASTEXITCODE." }
$release = $raw | ConvertFrom-Json
if (-not $release.isDraft -or $release.targetCommitish -cne $ExpectedCommit) {
    throw 'Release identity changed during the upload.'
}
foreach ($package in $packages) {
    $asset = @($release.assets | Where-Object name -CEQ $package.Name)
    if ($asset.Count -ne 1 -or $asset[0].digest -cne "sha256:$($package.Sha256)" -or $asset[0].size -ne $package.Size) {
        throw 'The uploaded release asset failed digest verification.'
    }
}
