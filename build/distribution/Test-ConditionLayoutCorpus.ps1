#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Binary,
    [Parameter(Mandatory)][string]$BaselineCli,
    [Parameter(Mandatory)][string]$Corpus,
    [Parameter(Mandatory)][string]$TestDirectory
)

$ErrorActionPreference = 'Stop'
$dotnet = (Get-Command dotnet -CommandType Application | Select-Object -First 1).Source
New-Item -ItemType Directory -Path $TestDirectory -ErrorAction Stop | Out-Null

function Copy-Corpus([string]$source, [string]$destination) {
    New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null
    foreach ($entry in Get-ChildItem -LiteralPath $source -Force) {
        if ($entry.Name -in @('.git', 'bin', 'obj')) { continue }
        $target = Join-Path $destination $entry.Name
        if ($entry.PSIsContainer) { Copy-Corpus $entry.FullName $target }
        else { Copy-Item -LiteralPath $entry.FullName -Destination $target }
    }
}

function Assert-DefaultBaseline([string]$candidate, [string]$baseline, [string]$mode) {
    $files = @(Get-ChildItem -LiteralPath $candidate -Recurse -File -Filter '*.cs')
    if ($files.Count -lt 100) { throw 'The corpus contains too few C# files.' }
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($candidate, $file.FullName)
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $baseline $relative)).Hash) {
            throw "The default layout changed against the base commit in ${mode}: $relative"
        }
    }
    "Disabled policy matches the base commit in ${mode}: $($files.Count) files."
}

$disabled = Join-Path $TestDirectory 'disabled'
$baseline = Join-Path $TestDirectory 'baseline'
$selected = Join-Path $TestDirectory 'selected'
$reference = Join-Path $TestDirectory 'reference'
Copy-Corpus $Corpus $disabled
foreach ($configuration in Get-ChildItem -LiteralPath $disabled -Recurse -Force -File -Filter '.editorconfig') {
    [IO.File]::AppendAllText($configuration.FullName, "`n[*.cs]`nmax_line_length = 120`ncsharp_keep_existing_linebreaks = false`ncsharp_space_around_binary_operators = before_and_after`n")
}
Copy-Corpus $disabled $baseline
& $dotnet $BaselineCli format $baseline --layout-rules none
if ($LASTEXITCODE -ne 0) { throw 'The default baseline did not format the corpus safely.' }
& $Binary format $disabled --layout-rules none
if ($LASTEXITCODE -ne 0) { throw 'The disabled policy did not format the corpus safely.' }
Assert-DefaultBaseline $disabled $baseline 'deterministic width 120'
$preserved = Join-Path $TestDirectory 'preserved'
$preservedBaseline = Join-Path $TestDirectory 'preserved-baseline'
Copy-Corpus $Corpus $preserved
foreach ($configuration in Get-ChildItem -LiteralPath $preserved -Recurse -Force -File -Filter '.editorconfig') {
    [IO.File]::AppendAllText($configuration.FullName, "`n[*.cs]`nmax_line_length = off`ncsharp_keep_existing_linebreaks = true`n")
}
Copy-Corpus $preserved $preservedBaseline
& $dotnet $BaselineCli format $preservedBaseline --layout-rules none
if ($LASTEXITCODE -ne 0) { throw 'The preservation baseline did not format the corpus safely.' }
& $Binary format $preserved --layout-rules none
if ($LASTEXITCODE -ne 0) { throw 'The disabled preservation policy did not format the corpus safely.' }
Assert-DefaultBaseline $preserved $preservedBaseline 'preservation without width'

Copy-Corpus $disabled $selected
$files = @(Get-ChildItem -LiteralPath $disabled -Recurse -File -Filter '*.cs')
$policy = Join-Path $selected '.condition-layout.json'
[IO.File]::WriteAllText($policy, @'
{
  "schemaVersion": 1,
  "rules": [{
    "id": "corpus-conditions",
    "files": ["**/*.cs"],
    "match": { "kind": "condition", "owner": "if-statement" },
    "layout": {
      "recipe": "attached-condition",
      "wrap": "if-long",
      "header": "inline-if-fits",
      "continuation": "one-indent",
      "closeParen": "with-condition"
    }
  }]
}
'@)
$refusalReasons = @(
    'unsupported condition-boundary trivia',
    'cannot move an is-pattern boundary around content trivia',
    'cannot move a property-pattern boundary around content trivia',
    'cannot move a positional-pattern boundary around content trivia',
    'cannot move pattern operators around content trivia',
    'cannot move a break around content trivia',
    'exceeds the condition-header budget',
    'exceeds the 511-node budget'
)
$accepted = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
$refused = [Collections.Generic.List[string]]::new()
$changed = 0
$explained = $false
foreach ($file in $files) {
    $relative = [IO.Path]::GetRelativePath($disabled, $file.FullName)
    $target = Join-Path $selected $relative
    $before = (Get-FileHash -LiteralPath $target).Hash
    $output = @(& $Binary format --files $target --layout-rules $policy 2>&1)
    $exitCode = $LASTEXITCODE
    $after = (Get-FileHash -LiteralPath $target).Hash
    if ($exitCode -eq 0) {
        $accepted.Add($relative, $after)
        if ($before -ne $after) {
            $changed++
            if (-not $explained) {
                $explanation = @(& $Binary explain-layout $target --layout-rules $policy)
                if ($LASTEXITCODE -ne 0 -or -not ($explanation -match 'rule = corpus-conditions;.*recipe = attached-condition')) {
                    throw 'A changed corpus file did not explain its selected condition policy.'
                }
                $explained = $true
            }
        }
    } elseif ($exitCode -eq 3) {
        $message = $output -join "`n"
        $known = @($refusalReasons | Where-Object { $message.Contains($_, [StringComparison]::Ordinal) })
        if ($known.Count -eq 0 -or $before -ne $after) {
            throw "Unclassified failure or modified refused file: $relative`n$message"
        }
        $refused.Add($relative)
        "Explicit, unchanged refusal: $relative ($($known[0]))"
    } else {
        throw "Unexpected corpus formatter exit $exitCode."
    }
}
if ($accepted.Count -lt 100 -or $changed -eq 0 -or -not $explained) {
    throw 'The selected corpus policy did not exercise sufficient successful input and actual layout changes.'
}

# Refused inputs are accounted for separately; only successfully formatted files enter fixed-point checks.
foreach ($relative in $refused) {
    Remove-Item -LiteralPath (Join-Path $selected $relative)
}
& $Binary format $selected --layout-rules $policy
if ($LASTEXITCODE -ne 0) { throw 'The second corpus pass failed.' }
foreach ($entry in $accepted.GetEnumerator()) {
    if ((Get-FileHash -LiteralPath (Join-Path $selected $entry.Key)).Hash -ne $entry.Value) {
        throw "A successful first pass was not a fixed point: $($entry.Key)"
    }
}
& $Binary check $selected --layout-rules $policy
if ($LASTEXITCODE -ne 0) { throw 'The selected corpus did not pass an uncached check.' }
Copy-Corpus $selected $reference
& $dotnet format whitespace $reference --folder
if ($LASTEXITCODE -ne 0) { throw 'The corpus reference formatter failed.' }
foreach ($entry in $accepted.GetEnumerator()) {
    if ((Get-FileHash -LiteralPath (Join-Path $reference $entry.Key)).Hash -ne $entry.Value) {
        throw "The selected output was not a stock formatter fixed point: $($entry.Key)"
    }
}
"Selected corpus: $($accepted.Count) successful files, $changed changed files, $($refused.Count) explicit unchanged refusals."
"First-pass idempotency and stock formatting conformance: $($accepted.Count)/$($accepted.Count)."
