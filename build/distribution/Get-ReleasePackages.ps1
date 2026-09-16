#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [ValidateSet('Base', 'Native', 'Local', 'Complete')][string]$Set = 'Complete',
    [ValidateSet('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-arm64')]
    [string]$RuntimeIdentifier = 'linux-x64',
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$baseIds = @('ncosentino.curb', 'ncosentino.curb-cli', 'ncosentino.curb-cli.any')
$nativeIds = @('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-arm64') |
    ForEach-Object { "ncosentino.curb-cli.$_" }
$expected = switch ($Set) {
    'Base' { $baseIds }
    'Native' { "ncosentino.curb-cli.$RuntimeIdentifier" }
    'Local' { $baseIds; "ncosentino.curb-cli.$RuntimeIdentifier" }
    'Complete' { $baseIds; $nativeIds }
}
$files = @(Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg' | Sort-Object Name)
if ($files.Count -ne @($expected).Count) { throw "Unexpected package count for the $Set set." }

$records = [Collections.Generic.List[object]]::new()
foreach ($file in $files) {
    if ($file.Length -ge 2GB) { throw 'A package exceeds the release asset limit.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($file.FullName)
    try {
        $entries = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
        if ($entries.Count -ne 1 -or $entries[0].Length -gt 1MB) {
            throw 'Package metadata is missing, ambiguous or oversized.'
        }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $id = $manifest.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='id']").InnerText
        $packageVersion = $manifest.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='version']").InnerText
        if ($id -cnotin $expected -or $packageVersion -cnotmatch '\A\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z') {
            throw 'A package has an unexpected identity or version.'
        }
        if (-not $Version) { $Version = $packageVersion }
        if ($packageVersion -cne $Version -or $file.Name -cne "$id.$Version.nupkg") {
            throw 'Package versions and filenames must match the release exactly.'
        }
        $records.Add([pscustomobject]@{
            Id = $id
            Version = $Version
            Name = $file.Name
            Path = $file.FullName
            Size = $file.Length
            Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }
    finally { $archive.Dispose() }
}
if (@($records.Id | Sort-Object -Unique).Count -ne @($expected).Count) {
    throw 'The package set contains a duplicate identity.'
}
$records.ToArray()
