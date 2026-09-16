#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$TestDirectory)

$ErrorActionPreference = 'Stop'
$validator = Join-Path $PSScriptRoot 'Get-ReleasePackages.ps1'
$ids = @(
    'ncosentino.curb', 'ncosentino.curb-cli', 'ncosentino.curb-cli.any',
    'ncosentino.curb-cli.linux-x64', 'ncosentino.curb-cli.linux-arm64',
    'ncosentino.curb-cli.win-x64', 'ncosentino.curb-cli.win-arm64',
    'ncosentino.curb-cli.osx-arm64'
)
$root = [IO.Path]::GetFullPath($TestDirectory)
New-Item -ItemType Directory -Path $root -ErrorAction Stop | Out-Null

function Write-TestPackage([string]$Path, [string]$Id, [string]$Version) {
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entry = $archive.CreateEntry('package.nuspec')
        $writer = [IO.StreamWriter]::new($entry.Open())
        try { $writer.Write("<package><metadata><id>$Id</id><version>$Version</version></metadata></package>") }
        finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Assert-Rejected([scriptblock]$Action, [string]$Reason) {
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notmatch $Reason) { throw }
        return
    }
    throw "Expected package rejection: $Reason"
}

try {
    foreach ($id in $ids) { Write-TestPackage (Join-Path $root "$id.1.2.3-canary.1.nupkg") $id '1.2.3-canary.1' }
    $packages = @(& $validator -PackageDirectory $root -Version '1.2.3-canary.1')
    if ($packages.Count -ne 8 -or @($packages | Where-Object { $_.Sha256 -cnotmatch '\A[0-9a-f]{64}\z' }).Count -ne 0) {
        throw 'The complete package set was not validated and hashed.'
    }
    Assert-Rejected { & $validator -PackageDirectory $root -Version '9.9.9' } 'versions and filenames'
    Assert-Rejected { & $validator -PackageDirectory $root -Set Base } 'package count'
    $file = Join-Path $root 'ncosentino.curb.1.2.3-canary.1.nupkg'
    Remove-Item -LiteralPath $file
    Assert-Rejected { & $validator -PackageDirectory $root } 'package count'
    Write-TestPackage $file 'another.package' '1.2.3-canary.1'
    Assert-Rejected { & $validator -PackageDirectory $root } 'unexpected identity'
    Remove-Item -LiteralPath $file
    Write-TestPackage $file 'ncosentino.curb' "1.2.3-canary.1`n"
    Assert-Rejected { & $validator -PackageDirectory $root } 'unexpected identity'
    Remove-Item -LiteralPath $file
    Write-TestPackage $file 'ncosentino.curb' '1.2.3-canary.2'
    Assert-Rejected { & $validator -PackageDirectory $root } 'versions and filenames'
    Remove-Item -LiteralPath $file
    Write-TestPackage $file 'ncosentino.curb' '1.2.3-canary.1'
    $renamed = Join-Path $root 'wrong-name.nupkg'
    Move-Item -LiteralPath $file -Destination $renamed
    Assert-Rejected { & $validator -PackageDirectory $root } 'versions and filenames'
    Move-Item -LiteralPath $renamed -Destination $file
    foreach ($id in $ids | Where-Object { $_ -notin @('ncosentino.curb', 'ncosentino.curb-cli', 'ncosentino.curb-cli.any') }) {
        Remove-Item -LiteralPath (Join-Path $root "$id.1.2.3-canary.1.nupkg")
    }
    if (@(& $validator -PackageDirectory $root -Set Base).Count -ne 3) { throw 'The base set was rejected.' }
    Write-TestPackage (Join-Path $root 'ncosentino.curb-cli.linux-x64.1.2.3-canary.1.nupkg') 'ncosentino.curb-cli.linux-x64' '1.2.3-canary.1'
    if (@(& $validator -PackageDirectory $root -Set Local).Count -ne 4) { throw 'The local installation set was rejected.' }
    foreach ($id in @('ncosentino.curb', 'ncosentino.curb-cli', 'ncosentino.curb-cli.any')) {
        Remove-Item -LiteralPath (Join-Path $root "$id.1.2.3-canary.1.nupkg")
    }
    if (@(& $validator -PackageDirectory $root -Set Native).Count -ne 1) { throw 'The native set was rejected.' }
    Assert-Rejected { & $validator -PackageDirectory $root -Set Native -RuntimeIdentifier win-x64 } 'unexpected identity'

    $workflowDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..' '..' '.github' 'workflows'))
    $artifactUse = [regex]::new('^\s*(?:-\s*)?uses:\s*[''"]?actions/(?:upload|download)-artifact(?:/|@)',
        [Text.RegularExpressions.RegexOptions]::IgnoreCase -bor [Text.RegularExpressions.RegexOptions]::Multiline,
        [TimeSpan]::FromSeconds(1))
    foreach ($workflow in Get-ChildItem -LiteralPath $workflowDirectory -File | Where-Object Extension -In @('.yml', '.yaml')) {
        if ($artifactUse.IsMatch([IO.File]::ReadAllText($workflow.FullName))) {
            throw 'Actions artifacts are not permitted in fork workflows.'
        }
    }
    'Release package validation contracts passed.'
}
finally { Remove-Item -LiteralPath $root -Recurse -Force }
