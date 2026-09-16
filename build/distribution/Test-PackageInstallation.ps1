#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Local')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Local')][string]$PackageDirectory,
    [Parameter(Mandatory, ParameterSetName = 'NuGetOrg')][switch]$NuGetOrg,
    [Parameter(Mandatory)][ValidatePattern('\A\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?\z')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('\A[0-9a-f]{40}\z')][string]$ExpectedCommit,
    [Parameter(Mandatory)][string]$TestDirectory
)

$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
$feed = if ($PSCmdlet.ParameterSetName -eq 'NuGetOrg') {
    'https://api.nuget.org/v3/index.json'
} else {
    [Security.SecurityElement]::Escape((Resolve-Path -LiteralPath $PackageDirectory).Path)
}
New-Item -ItemType Directory -Path $TestDirectory -ErrorAction Stop | Out-Null
$config = Join-Path $TestDirectory 'NuGet.Config'
[IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='fork' value='$feed'/></packageSources></configuration>")
$tool = Join-Path $TestDirectory 'tool'
& $dotnet tool install ncosentino.curb-cli --version $Version --tool-path $tool --configfile $config
if ($LASTEXITCODE -ne 0) { throw "Tool installation failed with exit $LASTEXITCODE." }
$binary = Join-Path $tool 'curb'
$identity = & $binary --version
if ($LASTEXITCODE -ne 0 -or $identity -cne "$Version+$ExpectedCommit") {
    throw 'The installed CLI does not identify the expected version and commit.'
}
$identity
$probe = Join-Path $TestDirectory 'Probe.cs'
[IO.File]::WriteAllText($probe, 'class Probe{int Value=>1;}')
& $binary format $probe
if ($LASTEXITCODE -ne 0) { throw "CLI formatting failed with exit $LASTEXITCODE." }
& $binary check $probe
if ($LASTEXITCODE -ne 0) { throw "CLI check failed with exit $LASTEXITCODE." }

$consumer = Join-Path $TestDirectory 'consumer'
New-Item -ItemType Directory -Path $consumer | Out-Null
$project = Join-Path $consumer 'Consumer.csproj'
[IO.File]::WriteAllText($project, "<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include='ncosentino.curb' Version='$Version' PrivateAssets='all'/></ItemGroup></Project>")
$source = Join-Path $consumer 'Probe.cs'
$inputText = 'class Probe{int Value=>1;}'
[IO.File]::WriteAllText($source, $inputText)
& $dotnet restore $project --configfile $config
if ($LASTEXITCODE -ne 0) { throw "Consumer restore failed with exit $LASTEXITCODE." }
& $dotnet build $project --no-restore --configuration Debug -p:Curb_Cache=false
if ($LASTEXITCODE -ne 0) { throw "Consumer formatting build failed with exit $LASTEXITCODE." }
if ([IO.File]::ReadAllText($source) -ceq $inputText) { throw 'The MSBuild package did not format its consumer.' }
& $dotnet build $project --no-restore --configuration Release -p:Curb_Cache=false
if ($LASTEXITCODE -ne 0) { throw "Consumer check build failed with exit $LASTEXITCODE." }
