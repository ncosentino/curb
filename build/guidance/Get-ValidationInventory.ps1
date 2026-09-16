#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$files = @(& git -C $ProjectRoot ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository validation sources.' }
$files = @($files | ForEach-Object { $_.Replace('\', '/') } | Sort-Object -CaseSensitive -Unique)
$contractPath = Join-Path $ProjectRoot '.github\guidance.json'
$schemaPath = Join-Path $ProjectRoot '.github\guidance.schema.json'
$contractText = Get-Content -LiteralPath $contractPath -Raw -Encoding utf8
if (-not (Test-Json -Json $contractText -SchemaFile $schemaPath -ErrorAction Stop)) {
    throw 'The guidance contract does not match its schema.'
}
$contract = $contractText | ConvertFrom-Json
$commandPath = Join-Path $ProjectRoot $contract.validation.commandDefinition.Replace('/', [IO.Path]::DirectorySeparatorChar)
$commands = Get-Content -LiteralPath $commandPath -Raw -Encoding utf8
$targets = @([regex]::Matches(
    $commands, '(?m)^\s*\|\s*\[<[^\r\n]*\bSubCommand[^\r\n]*>\]\s*(?<name>[A-Za-z]\w*)',
    [Text.RegularExpressions.RegexOptions]::None, [TimeSpan]::FromSeconds(1)) |
    ForEach-Object { $_.Groups['name'].Value.ToLowerInvariant() } | Sort-Object -Unique)
if ($targets.Count -eq 0) { throw 'No build targets were found in the declared command definition.' }
$inventory = [PSCustomObject]@{
    projectRoot = $ProjectRoot
    dotnetSolutions = @($files | Where-Object { $_ -match '\.slnx?$' })
    dotnetProjects = @($files | Where-Object { $_ -match '\.(?:cs|fs)proj$' })
    buildManifests = @($files | Where-Object { $_ -match '(^|/)Directory\.(?:Build|Packages)\.(?:props|targets)$|(^|/)global\.json$|(^|/)nuget\.config$|^\.config/dotnet-tools\.json$' })
    buildEntrypoints = @($files | Where-Object { $_ -in @('build.sh', 'build.bat') })
    buildSources = @($files | Where-Object { $_ -match '^build/scripts/.+\.(?:fs|fsproj)$' })
    buildTargets = $targets
    workflows = @($files | Where-Object { $_ -match '^\.github/workflows/.+\.ya?ml$' })
    skills = @($files | Where-Object { $_ -match '^(?:\.github|\.claude)/skills/.+/SKILL\.md$' })
    guidance = $contract.validation
}
if ($Json) { $inventory | ConvertTo-Json -Depth 6 } else { $inventory }
