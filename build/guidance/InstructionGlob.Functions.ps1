#Requires -Version 7.0

function Split-InstructionGlobPatterns {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ApplyTo)

    if ([string]::IsNullOrWhiteSpace($ApplyTo) -or $ApplyTo.Length -gt 4096) {
        throw 'An instruction glob must contain between 1 and 4096 characters.'
    }
    $patterns = [Collections.Generic.List[string]]::new()
    $current = [Text.StringBuilder]::new()
    $depth = 0
    foreach ($character in $ApplyTo.ToCharArray()) {
        if ($character -ceq '{') {
            $depth++
            if ($depth -gt 8) { throw 'Instruction glob nesting exceeds eight levels.' }
        } elseif ($character -ceq '}') {
            $depth--
            if ($depth -lt 0) { throw 'Instruction glob has an unmatched closing brace.' }
        }
        if ($character -ceq ',' -and $depth -eq 0) {
            $part = $current.ToString().Trim()
            if ($part.Length -eq 0) { throw 'Instruction glob has an empty alternative.' }
            $patterns.Add($part)
            [void]$current.Clear()
        } else {
            [void]$current.Append($character)
        }
    }
    if ($depth -ne 0) { throw 'Instruction glob has an unmatched opening brace.' }
    $last = $current.ToString().Trim()
    if ($last.Length -eq 0) { throw 'Instruction glob has an empty alternative.' }
    $patterns.Add($last)
    return @($patterns)
}

function Expand-InstructionGlobPattern {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Pattern)

    $open = $Pattern.IndexOf('{')
    if ($open -lt 0) { return @($Pattern) }
    $depth = 0
    $close = -1
    for ($index = $open; $index -lt $Pattern.Length; $index++) {
        if ($Pattern[$index] -ceq '{') { $depth++ }
        elseif ($Pattern[$index] -ceq '}') {
            $depth--
            if ($depth -eq 0) { $close = $index; break }
        }
    }
    if ($close -lt 0) { throw 'Instruction glob has an unmatched opening brace.' }
    $expanded = [Collections.Generic.List[string]]::new()
    $prefix = $Pattern.Substring(0, $open)
    $suffix = $Pattern.Substring($close + 1)
    foreach ($alternative in @(Split-InstructionGlobPatterns $Pattern.Substring($open + 1, $close - $open - 1))) {
        foreach ($value in @(Expand-InstructionGlobPattern ($prefix + $alternative + $suffix))) {
            $expanded.Add($value)
            if ($expanded.Count -gt 256) { throw 'Instruction glob expands to more than 256 alternatives.' }
        }
    }
    return @($expanded)
}

function Get-InstructionGlobRegexes {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ApplyTo)

    $result = [Collections.Generic.List[regex]]::new()
    foreach ($part in @(Split-InstructionGlobPatterns $ApplyTo)) {
        foreach ($pattern in @(Expand-InstructionGlobPattern $part)) {
            if ($pattern -match '(^/|\\|:|\[|\])' -or $pattern.Split('/') -contains '..') {
                throw "Instruction glob is not a supported repository-relative pattern: $pattern"
            }
            $expression = [regex]::Escape($pattern)
            $expression = $expression -replace '\\\*\\\*/', '(?:.*/)?'
            $expression = $expression -replace '\\\*\\\*', '.*'
            $expression = $expression -replace '\\\*', '[^/]*'
            $expression = $expression -replace '\\\?', '[^/]'
            $result.Add([regex]::new(
                "^$expression$",
                [Text.RegularExpressions.RegexOptions]::CultureInvariant,
                [TimeSpan]::FromMilliseconds(250)))
            if ($result.Count -gt 256) { throw 'Instruction glob expands to more than 256 alternatives.' }
        }
    }
    return @($result)
}

function Test-InstructionGlobMatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ApplyTo,
        [Parameter(Mandatory)][string]$RelativePath
    )
    $normalized = $RelativePath.Replace('\', '/')
    foreach ($expression in @(Get-InstructionGlobRegexes $ApplyTo)) {
        if ($expression.IsMatch($normalized)) { return $true }
    }
    return $false
}

function Get-InstructionMetadata {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$FilePath)

    $text = Get-Content -LiteralPath $FilePath -Raw -Encoding utf8
    $frontmatter = [regex]::Match(
        $text, '\A(?:\uFEFF)?---\r?\n(?<metadata>.*?)\r?\n---(?:\r?\n|\z)',
        [Text.RegularExpressions.RegexOptions]::Singleline, [TimeSpan]::FromSeconds(1))
    if (-not $frontmatter.Success) { throw "Instruction lacks YAML frontmatter: $FilePath" }
    $matches = [regex]::Matches($frontmatter.Groups['metadata'].Value, '(?m)^applyTo:\s*(.+?)\s*$')
    if ($matches.Count -ne 1) { throw "Instruction must declare one applyTo scalar: $FilePath" }
    $pattern = $matches[0].Groups[1].Value.Trim()
    if ($pattern.Length -ge 2 -and $pattern[0] -in @([char]34, [char]39)) {
        if ($pattern[-1] -cne $pattern[0]) { throw "Instruction applyTo quotes do not match: $FilePath" }
        $pattern = $pattern.Substring(1, $pattern.Length - 2)
    }
    [PSCustomObject]@{
        ApplyTo = $pattern
        Regexes = @(Get-InstructionGlobRegexes $pattern)
        Lines = @(Get-Content -LiteralPath $FilePath -Encoding utf8).Count
        Bytes = [Text.Encoding]::UTF8.GetByteCount($text)
    }
}
