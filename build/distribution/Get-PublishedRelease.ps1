#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('\Av\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z')][string]$Tag,
    [Parameter(Mandatory)][string]$PackageDirectory
)

$ErrorActionPreference = 'Stop'
$gh = (Get-Command gh -CommandType Application | Select-Object -First 1).Source
$raw = & $gh release view $Tag --repo ncosentino/curb --json tagName,isDraft,targetCommitish,assets
if ($LASTEXITCODE -ne 0) { throw "Cannot inspect the fork release: exit $LASTEXITCODE." }
$release = $raw | ConvertFrom-Json
if ($release.isDraft -or $release.tagName -cne $Tag -or $release.targetCommitish -cnotmatch '\A[0-9a-f]{40}\z' -or $release.assets.Count -ne 9) {
    throw 'Promotion requires a published fork release with an exact commit and complete assets.'
}
$directory = [IO.Path]::GetFullPath($PackageDirectory)
New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
& $gh release download $Tag --repo ncosentino/curb --pattern '*.nupkg' --pattern SHA256SUMS --dir $directory
if ($LASTEXITCODE -ne 0) { throw "Release download failed with exit $LASTEXITCODE." }
$version = $Tag.Substring(1)
$packages = @(& (Join-Path $PSScriptRoot 'Get-ReleasePackages.ps1') -PackageDirectory $directory -Set Complete -Version $version)
foreach ($package in $packages) {
    $asset = @($release.assets | Where-Object name -CEQ $package.Name)
    if ($asset.Count -ne 1 -or $asset[0].size -ne $package.Size -or $asset[0].digest -cne "sha256:$($package.Sha256)") {
        throw 'A release package failed identity or digest verification.'
    }
}
$checksumFile = Join-Path $directory 'SHA256SUMS'
$checksumAsset = @($release.assets | Where-Object name -CEQ 'SHA256SUMS')
$checksumHash = (Get-FileHash -LiteralPath $checksumFile).Hash.ToLowerInvariant()
if ($checksumAsset.Count -ne 1 -or $checksumAsset[0].digest -cne "sha256:$checksumHash") {
    throw 'The release checksum manifest failed digest verification.'
}
$expected = [string]::Join("`n", [string[]]@($packages | ForEach-Object { "$($_.Sha256)  $($_.Name)" })) + "`n"
if ([IO.File]::ReadAllText($checksumFile).ReplaceLineEndings("`n") -cne $expected) {
    throw 'The checksum manifest does not describe the downloaded package set.'
}
[pscustomobject]@{
    Tag = $Tag
    Version = $version
    Commit = $release.targetCommitish
    Packages = $packages
}
