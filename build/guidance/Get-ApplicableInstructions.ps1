#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Paths')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Paths')]
    [ValidateNotNullOrEmpty()][string[]]$Path,
    [Parameter(Mandatory, ParameterSetName = 'File')]
    [string]$PathsFile,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [string]$InstructionsRoot,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
. (Join-Path $PSScriptRoot 'InstructionGlob.Functions.ps1')

if ($PSCmdlet.ParameterSetName -eq 'File') {
    $inputPaths = Get-Content -LiteralPath $PathsFile -Raw -Encoding utf8 | ConvertFrom-Json -NoEnumerate
    if ($inputPaths -isnot [array] -or @($inputPaths | Where-Object { $_ -isnot [string] }).Count -gt 0) {
        throw 'PathsFile must contain a JSON array of repository-relative strings.'
    }
    $Path = [string[]]$inputPaths
}
if ($Path.Count -eq 0) { throw 'At least one repository-relative path is required.' }
if (-not $InstructionsRoot) {
    $contract = Get-Content -LiteralPath (Join-Path $ProjectRoot '.github\guidance.json') -Raw | ConvertFrom-Json
    $InstructionsRoot = Join-Path $ProjectRoot $contract.instructions.root.Replace('/', [IO.Path]::DirectorySeparatorChar)
}
$InstructionsRoot = (Resolve-Path -LiteralPath $InstructionsRoot).Path
$instructions = @(Get-ChildItem -LiteralPath $InstructionsRoot -Recurse -File -Filter '*.instructions.md' |
    Sort-Object FullName | ForEach-Object {
        $metadata = Get-InstructionMetadata $_.FullName
        [PSCustomObject]@{
            RelativePath = [IO.Path]::GetRelativePath($ProjectRoot, $_.FullName).Replace('\', '/')
            Metadata = $metadata
        }
    })
$results = [Collections.Generic.List[object]]::new()
foreach ($candidate in @($Path | Sort-Object -CaseSensitive -Unique)) {
    if ([string]::IsNullOrWhiteSpace($candidate) -or [IO.Path]::IsPathRooted($candidate) -or $candidate -match '^(?:[\\/]|[A-Za-z][A-Za-z0-9+.-]*:)') {
        throw 'Instruction lookup paths must be nonempty and repository-relative.'
    }
    $normalized = $candidate.Replace('\', '/')
    while ($normalized.StartsWith('./', [StringComparison]::Ordinal)) { $normalized = $normalized.Substring(2) }
    if ($normalized -eq '' -or $normalized -eq '.' -or $normalized.Split('/') -contains '..') {
        throw 'Instruction lookup paths must name files inside the repository.'
    }
    foreach ($instruction in $instructions) {
        $matches = $false
        foreach ($expression in $instruction.Metadata.Regexes) {
            if ($expression.IsMatch($normalized)) { $matches = $true; break }
        }
        if ($matches) {
            $results.Add([PSCustomObject]@{
                Path = $normalized
                InstructionPath = $instruction.RelativePath
                Lines = $instruction.Metadata.Lines
                Bytes = $instruction.Metadata.Bytes
            })
        }
    }
}
if ($Json) {
    ConvertTo-Json -InputObject @($results.ToArray()) -Depth 4
} else {
    $results.ToArray()
}
