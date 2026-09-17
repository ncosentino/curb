#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Binary,
    [string[]]$PrefixArguments = @(),
    [Parameter(Mandatory)][string]$TestDirectory,
    [string]$PackageVersion,
    [string]$NuGetConfig
)

$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$fixture = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..' '..' 'examples' 'curb-layout-smoketest'))
New-Item -ItemType Directory -Path $TestDirectory -ErrorAction Stop | Out-Null
Get-ChildItem -LiteralPath $fixture -File -Force | Copy-Item -Destination $TestDirectory
$source = Join-Path $TestDirectory 'LayoutSample.cs'
$policy = Join-Path $TestDirectory '.curb-layout.json'
$project = Join-Path $TestDirectory 'Probe.csproj'
$original = [IO.File]::ReadAllText($source)
$policyText = [IO.File]::ReadAllText($policy)

if ($PackageVersion) {
    if (-not $NuGetConfig -or $PackageVersion -cnotmatch '\A\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z') {
        throw 'Package smoke requires a valid version and explicit NuGet configuration.'
    }
    [xml]$xml = [IO.File]::ReadAllText($project)
    $group = $xml.CreateElement('ItemGroup')
    $reference = $xml.CreateElement('PackageReference')
    $reference.SetAttribute('Include', 'ncosentino.curb')
    $reference.SetAttribute('Version', $PackageVersion)
    $reference.SetAttribute('PrivateAssets', 'all')
    $group.AppendChild($reference) | Out-Null
    $xml.DocumentElement.AppendChild($group) | Out-Null
    $xml.Save($project)
    & $dotnet restore $project --configfile $NuGetConfig
    if ($LASTEXITCODE -ne 0) { throw "Layout consumer restore failed: $LASTEXITCODE" }
    & $dotnet build $project --no-restore --configuration Debug -p:Curb_LogLevel=high
    if ($LASTEXITCODE -ne 0) { throw "Layout consumer build failed: $LASTEXITCODE" }
} else {
    & $Binary @PrefixArguments format --files $source
    if ($LASTEXITCODE -ne 0) { throw "Custom rule formatting failed: $LASTEXITCODE" }
}
$formatted = [IO.File]::ReadAllText($source)
if ($formatted -ceq $original -or -not $formatted.Contains("CancellationToken ct) => await`n    TraceScope.RunAsync(async () =>`n    Outcome.CaptureAsync(async () =>`n    {")) {
    throw 'The repository rule did not produce declaration-aligned wrapper headers.'
}
if (-not $formatted.Contains('        var doubled = number * 2;')) {
    throw 'The callback body did not receive normal formatting.'
}
& $Binary @PrefixArguments check --files $source
if ($LASTEXITCODE -ne 0) { throw "The first custom output was not a fixed point: $LASTEXITCODE" }
$hash = (Get-FileHash -LiteralPath $source).Hash
& $Binary @PrefixArguments format --files $source
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $source).Hash -ne $hash) { throw 'Custom formatting changed on the second pass.' }
$explanation = & $Binary @PrefixArguments explain-layout $source
if ($LASTEXITCODE -ne 0 -or -not ($explanation -match 'rule = smoke-wrappers;')) { throw 'The custom rule was not explained.' }
$explanation
& $dotnet build $project --configuration Release
if ($LASTEXITCODE -ne 0) { throw "The custom layout failed compiler/reference enforcement: $LASTEXITCODE" }
& $dotnet format whitespace $project --no-restore --verify-no-changes
if ($LASTEXITCODE -ne 0) { throw "The custom layout was not a reference fixed point: $LASTEXITCODE" }

if ($PackageVersion) {
    [IO.File]::WriteAllText($policy, $policyText.Replace('TraceScope.RunAsync', 'OtherScope.RunAsync'))
    $stamp = Join-Path $TestDirectory 'obj' 'Debug' 'net10.0' 'curb.stamp'
    [IO.File]::SetLastWriteTimeUtc($policy, [IO.File]::GetLastWriteTimeUtc($stamp).AddSeconds(2))
    & $dotnet build $project --no-restore --configuration Debug -p:Curb_LogLevel=high
    if ($LASTEXITCODE -ne 0) { throw "Rule-only edit did not rebuild the consumer: $LASTEXITCODE" }
    if ([IO.File]::ReadAllText($source) -ceq $formatted) { throw 'A rule-only edit did not invalidate MSBuild and formatter caches.' }
    [IO.File]::WriteAllText($policy, $policyText)
}
$beforeInvalid = [IO.File]::ReadAllText($source)
[IO.File]::WriteAllText($policy, '{}')
& $Binary @PrefixArguments format --files $source
if ($LASTEXITCODE -ne 3 -or [IO.File]::ReadAllText($source) -cne $beforeInvalid) {
    throw 'An invalid rule file did not fail without changing source.'
}
[IO.File]::WriteAllText($policy, $policyText)
'Custom layout installation and safety checks passed.'
