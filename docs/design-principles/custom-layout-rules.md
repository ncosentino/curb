---
navigation_title: Custom layout rules
description: Repository-owned syntax rules for specialized layouts without source annotations.
---

# Repository-owned layout rules

Version 1 supports wrapper chains, logical condition headers, logical and nonlogical
expression-lambda arguments, multiline raw strings and string-concatenation arguments
as separate typed recipes.
Rules are repository data, not executable plugins or source annotations.

Allow a repository to define a canonical layout for selected syntax without putting pragmas,
marker comments or application-specific names into {{product}}. Keep ordinary formatting,
token safety, Native-AOT support and one-pass idempotency intact.

## Establish the actual compatibility requirement

A neutral, compilable nested-wrapper example was checked with SDK `10.0.303`.
The requested declaration-aligned layout built successfully with `IDE0055` set to error,
and `dotnet format whitespace` preserved it apart from file line-ending normalization.
A malformed-spacing control produced two `IDE0055` errors, confirming that enforcement ran.

The motivating shape is therefore not inherently a Roslyn incompatibility. A formatter can
choose a different canonical layout from the same input while both layouts remain reference
fixed points. The missing feature is selecting a layout by syntax and repository policy.

This evidence covers the measured shape, not every overload, comment arrangement or SDK.
The [reference-conformance contract](conformance.md) remains applicable.

## Configuration owned by the consumer

Keep existing options in `.editorconfig`. Add one explicitly Curb-specific reference to a
versioned, declarative rule pack:

```ini
[*.cs]
curb_layout_rules = .curb-layout.json
```

Resolve a relative path against the `.editorconfig` that supplied the setting, not the
process directory or source file's directory. Nested configuration can select another pack
or explicitly select `none`. A referenced file must stay inside that configuration directory;
symbolic policy files and directories are refused. `--layout-rules` overrides EditorConfig.
`Curb_LayoutRules` supplies the same override from MSBuild.

This is an explicit exception to the standard-key-only convention. It does not
pretend that another formatter understands the new key.

The following pack uses neutral application names:

```json
{
  "schemaVersion": 1,
  "rules": [
    {
      "id": "stacked-wrapper-calls",
      "files": ["**/*.cs"],
      "match": {
        "kind": "lambda-wrapper-chain",
        "owner": "expression-bodied-method",
        "calleeSyntax": [
          "TraceScope.RunAsync",
          "Outcome.CaptureAsync",
          "Outcome.Capture"
        ],
        "callbackArgument": "last",
        "callbackParameters": "empty",
        "terminalBody": "block",
        "awaitTokens": "preserve"
      },
      "layout": {
        "recipe": "vertical-wrapper-chain",
        "anchor": "declaration",
        "parameterClose": "with-last-parameter",
        "arrowAndRootAwait": "with-header",
        "wrapperIndent": 0,
        "lambdaBraceIndent": 0,
        "bodyIndent": 1,
        "closeInvocations": "compact"
      }
    }
  ]
}
```

Rule packs are UTF-8 JSON, optionally with a UTF-8 byte-order mark. All shown properties are
required for the wrapper recipe. Version 1 accepts the shown matcher and recipe values:
wrapper and brace indentation are zero, and body indentation is one level. Unsupported
values fail rather than being accepted without behavior. Limits are 1 MiB per pack,
256 rules, 64 callees per rule, 32 file filters per rule and 16 wrappers per chain.
File filters support relative literal paths, `*`, `**` and `?`; brace expansion, character
classes, negation and parent-directory traversal are refused.

The repository substitutes its own invocation spellings. No such names, return types,
business namespaces or file paths belong in {{product}}'s built-in printers.

For a matching method, the target shape is:

```csharp
    public async Task<Result<Value>> GetAsync(
        int number,
        CancellationToken ct) => await
    TraceScope.RunAsync(async () =>
    Outcome.CaptureAsync(async () =>
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        return new Value(number);
    }));
```

### Postfix calls on selected wrappers

The optional `match.postfixCalleeSyntax` list allows simple method names after each
selected wrapper call. For example, add this property beside `awaitTokens`:

```json
"postfixCalleeSyntax": ["ConfigureAwait"]
```

The matcher looks through those configured invocations to find the wrapper. The renderer
keeps each postfix call attached to its original wrapper, including arguments, named
arguments and generic type arguments. It does not add or remove `await` or change any
argument value.

```csharp
    public async Task<Result<Value>> GetAsync(
        int number,
        CancellationToken ct) => await
    TraceScope.RunAsync(async () =>
    Outcome.CaptureAsync(async () =>
    {
        return new Value(number);
    })).ConfigureAwait(false);
```

Omitting the property retains the original match contract. Unconfigured postfix methods,
property tails and conditional-access tails do not match. Names are syntax selectors, not
claims about which overload or symbol an invocation resolves to. The list accepts 1 to 64
simple identifiers; a stage can carry at most 16 postfix calls. Content trivia on postfix
boundaries is refused rather than moved across the compact closing delimiters.

## Logical condition headers

The `logical-condition` matcher selects an `if` header with a top-level `&&` or `||` chain.
Its `hanging-logical-condition` recipe keeps the first operand beside the opening parenthesis,
aligns subsequent operands with its actual output column and places the closing parenthesis
at the statement indentation only when the header breaks.

Use this pack on its own, or append its rule object to an existing pack's `rules` array.
Existing wrapper rules remain unchanged; both recipes share file filtering, cache identity,
MSBuild dependencies and diagnostics.

```json
{
  "schemaVersion": 1,
  "rules": [{
    "id": "logical-headers",
    "files": ["**/*.cs"],
    "match": {
      "kind": "logical-condition",
      "owner": "if-statement"
    },
    "layout": {
      "recipe": "hanging-logical-condition",
      "wrap": "if-long",
      "firstOperand": "with-open",
      "continuation": "align-first-operand",
      "operators": "trailing",
      "closeParen": "own-line-when-broken"
    }
  }]
}
```

The condition recipe requires a finite `max_line_length`, deterministic layout and
non-ignored binary spacing. Unsupported combinations fail explicitly. A short header remains
inline even if the global binary style is `chop_always`; the selected recipe owns its logical
breaks rather than inheriting blanket binary chopping.

```csharp
if (candidate.CategoryId == RequiredCategoryIdentifier &&
    candidate.SegmentId == RequiredSegmentIdentifier &&
    candidate.State == "Ready"
)
{
    Accept();
}
```

Only chains of the same logical operator are collected together. Explicit parentheses and
different-precedence logical subtrees remain structured operands. No tokens, grouping or
operator order change. Comparisons that fit stay intact; an oversized operand uses its own
ordinary argument/member/binary break opportunities instead of forcing all comparisons apart.
Indivisible tokens may still exceed the configured width.

The opening seam has no width-driven break. Logical separators and the closing seam follow
one named header group whose measurement includes the closing delimiter. Continuation anchors
are allocated per header, so nested conditions cannot overwrite each other's columns.
Tabs and configured keyword spacing participate in the captured output column.

`while`, `do`, `lock`, nonlogical conditions and ordinary expressions are not selected.
In an `else if` chain, continuations follow the first operand's actual column; the closing
delimiter uses the enclosing statement indentation.

Comments inside operands and comments following logical operators use the ordinary trivia
printer. Content trivia on delimiter seams, comments that prevent moving an operator to a
trailing position, and directives in a selected condition are currently refused rather than
moved across operators. Refusal leaves the source unchanged. Collection is bounded to
511 syntax nodes per chain.

## Logical lambda arguments

The `logical-lambda-argument` matcher selects a simple or parenthesized expression-bodied
lambda when it is the sole argument of an invocation and its body is a top-level `&&` or
`||` chain. Selection is syntactic and independent of the callee's name.

```json
{
  "schemaVersion": 1,
  "rules": [{
    "id": "logical-lambdas",
    "files": ["**/*.cs"],
    "match": {
      "kind": "logical-lambda-argument",
      "owner": "sole-invocation-argument"
    },
    "layout": {
      "recipe": "hanging-logical-lambda",
      "wrap": "if-long",
      "header": "inline-if-fits",
      "continuation": "one-indent",
      "operators": "trailing",
      "closeParen": "with-final-operand"
    }
  }]
}
```

A fitting predicate stays inline. When the complete predicate exceeds the width, the
call and lambda header stay inline if that header fits independently:

```csharp
entries.Any(entry =>
    entry.IsEnabled &&
    (entry.MatchesPrimaryCategory || entry.MatchesSecondaryCategory) &&
    entry.HasRequiredPermission);
```

Only an oversized header breaks after the invocation's opening parenthesis. In that
case the lambda header receives one indent and its body receives one further indent.
Indentation follows the actual output line, including aligned `if` operands and `else if`
columns that fall between ordinary indentation levels.
Selected parenthesized lambda headers wrap by width, not forced parameter chopping or
parameter-count limits. Ordinary declaration parameter lists retain their normal policy.
Logical operators trail their operands, and the closing parenthesis remains beside
the final operand. Explicit parentheses and precedence remain unchanged. Short operand
calls remain intact; genuinely oversized operands use ordinary internal wrapping.

The recipe requires a finite width and deterministic layout. It composes with logical
`if` headers and wrapper delegated bodies. Nested lambdas do not inherit the outer
predicate's logical layout context. String-concatenation selection remains restricted
to actual arguments, not lambda bodies. Raw-string recipes can compose inside operands;
overlapping operand-opening ownership fails explicitly.

Source suppression takes precedence. Duplicate rules, named or ref lambda arguments,
directives and content trivia on moved header/operator boundaries fail explicitly.
Interior operand trivia uses normal printing. Collection uses the existing 511-node
chain budget. There is no token rewrite or verifier exemption.

## Expression lambda arguments

The `expression-lambda-argument` matcher selects an expression-bodied lambda when it
is the sole argument of an invocation and its body is not a top-level `&&` or `||`
expression. It covers pattern predicates, projections and other expression bodies
without interpreting return types or selecting by method name.

```json
{
  "schemaVersion": 1,
  "rules": [{
    "id": "expression-lambdas",
    "files": ["**/*.cs"],
    "match": {
      "kind": "expression-lambda-argument",
      "owner": "sole-invocation-argument",
      "body": "nonlogical-expression"
    },
    "layout": {
      "recipe": "attached-expression-lambda",
      "wrap": "if-long",
      "header": "inline-if-fits",
      "continuation": "one-indent",
      "closeParen": "with-body"
    }
  }]
}
```

A fitting callback remains inline. A longer pattern predicate breaks after the arrow
while retaining a fitting call header and compact closing delimiters:

```csharp
if (entries.Any(entry =>
    entry.Status is not (ResultStatus.Unavailable or ResultStatus.NotApplicable)))
{
    return false;
}
```

An expression with its own wrapping opportunities uses normal internal formatting.
A fitting constructor introducer stays attached while its arguments wrap:

```csharp
return entries.Select(entry => new SearchResult(
    entry.Id,
    Map(entry.CurrentValue),
    Map(entry.PreviousValue)
));
```

An oversized body introducer can break after the arrow. Only an oversized callback
header breaks after the invocation opener. Constructor and nested invocation arguments
retain their configured wrapping policy. Selected parenthesized lambda headers use width
rather than forced parameter chopping; ordinary declarations remain unchanged.

The rule coordinates enclosing `if` and `else if` parentheses for a selected invocation,
including parenthesized and negated forms. Other control-flow keywords keep their existing
header layout. A logical condition chain still uses the logical-condition recipe.
Continuation indentation follows the actual output line, including aligned operands,
tabs and `else if` columns.

The logical-lambda and expression-lambda matchers have disjoint body selectors. Configure
both to cover top-level logical predicates and other expression callbacks; there is no
implicit precedence between overlapping rules. Nested callbacks select their own rules.
Multiple arguments and block bodies do not match. Source suppression wins.

The recipe requires a finite width, deterministic layout and canonical binary spacing.
Named or ref lambda arguments, conflicting rules, directives and content trivia on moved
header or closing boundaries fail explicitly without output. Interior body trivia uses
normal printing. The shared callback renderer preserves tokens, normal body formatting
and first-pass idempotency without a semantic model or verifier exemption.

## Standalone multiline raw strings

The `multiline-raw-string` matcher selects plain, interpolated and UTF-8 multiline raw-string
expressions. Its `standalone-raw-string` recipe always places the opener on its own line.
Attached and already-detached input converge to the same layout; this is not preservation
of an optional source break.

Append this rule to an existing pack, or use it as the only rule:

```json
{
  "schemaVersion": 1,
  "rules": [{
    "id": "raw-openers",
    "files": ["**/*.cs"],
    "match": {
      "kind": "multiline-raw-string",
      "owner": "expression"
    },
    "layout": {
      "recipe": "standalone-raw-string",
      "openingDelimiter": "own-line",
      "indentation": "preserve-closing",
      "contents": "preserve"
    }
  }]
}
```

Before:

```csharp
var value = """
    first
        indented second
    """;
```

After:

```csharp
var value =
    """
    first
        indented second
    """;
```

Only the external opening boundary changes. The opener uses the exact whitespace preceding
the preserved closing delimiter. Payload text, meaningful indentation, internal line endings,
quote/dollar counts, interpolation text and UTF-8 suffixes remain unchanged.

One selected-literal path handles initializers, arguments, returns, expression bodies and
conditional branches. It represents literal newlines explicitly in the document arena and
reuses a line the parent already opened, including inside forced-flat layout.

The policy works with or without a width and in both layout modes. Ordinary strings, verbatim
strings, single-line raw strings and unselected formatting retain their existing behavior.
String/interpolation interiors remain opaque, as they are in ordinary formatting; this rule
does not reformat nested expressions inside an enclosing verbatim interpolation span.

Existing source suppression still takes precedence. Duplicate raw rules and incompatible
ownership fail explicitly. For example, a raw opener that is itself the start of a logical
operand cannot also satisfy an enabled condition recipe's operand-start alignment.
A raw value nested inside an operand or a wrapper's delegated argument/body can compose.

## String-concatenation arguments

The `string-concatenation` matcher selects a `+` chain that contains at least one string or
interpolated-string operand and whose outermost link is itself an argument of an invocation,
object creation, element access or constructor initializer. Its
`argument-string-concatenation` recipe keeps every continuation line at the argument's
indentation instead of the ordinary hanging continuation indent.

```json
{
  "schemaVersion": 1,
  "rules": [{
    "id": "argument-strings",
    "files": ["**/*.cs"],
    "match": {
      "kind": "string-concatenation",
      "owner": "argument"
    },
    "layout": {
      "recipe": "argument-string-concatenation",
      "wrap": "if-long",
      "operators": "trailing",
      "continuation": "argument-indent"
    }
  }]
}
```

Before:

```csharp
logger.Warn(
    "Scheduler configuration supplied {UnboundCount} setting(s) that never reached the " +
        "scheduler and are therefore ignored: {UnboundKeys}.",
    count,
    keys
);
```

After:

```csharp
logger.Warn(
    "Scheduler configuration supplied {UnboundCount} setting(s) that never reached the " +
    "scheduler and are therefore ignored: {UnboundKeys}.",
    count,
    keys
);
```

Only the continuation indentation is owned. Whether and where the chain breaks stays with the
ordinary per-operator groups: a chain that fits remains on one line, and a wrapped chain keeps
packing as many operands as fit onto each line. Operators remain trailing. A named argument
continues at the argument's indentation, beneath its name.

Numeric `+` chains, other binary operators, chains inside lambda bodies, assignments and
attribute arguments are not selected. The recipe applies in both layout modes; preservation
mode already keeps an author-broken chain at the argument's indentation. Suppressed spans take
precedence, and duplicate string-concatenation rules fail explicitly. A multiline raw-string
operand composes with the raw-string recipe.

The selected layout builds with `IDE0055` enforced and is a `dotnet format whitespace`
fixed point.

## Extensibility contract

The framework is a syntax matcher plus a bounded layout recipe, not a collection of
hardcoded application exceptions. {{product}} supplies the reusable matcher and layout
operations; a repository selects them and supplies its own callee spellings in data.

The named recipe uses a small internal vocabulary over typed captures, not reflection-based
property paths. Version 1 does not expose a free-form operation program:

| Operation | Authority |
|---|---|
| Break at a captured boundary | Choose a line break without moving its adjacent tokens |
| Join a captured boundary | Choose whitespace subject to token and comment safety |
| Align or indent a captured region | Use a declared output anchor and relative indentation |
| Group captured boundaries | Make width decisions together in the existing document IR |
| Delegate a captured subtree | Apply normal formatting at the supplied base indentation |

Capture-list expansion is bounded; programs have no arbitrary loops or executable
predicates. A recipe compiler rejects operations applied to incompatible capture kinds.

The wrapper matcher/recipe pair handles nested invocations whose callback lambdas lead to a
block. It is useful for tracing, result, retry and transaction wrappers. A pack can select
different callees and file scopes without changing the CLI.

This is intentionally not arbitrary executable formatter code. A genuinely new syntax
matcher or layout operation still needs a reviewed engine implementation. Version 1 does
not load managed assemblies, run scripts, evaluate C# predicates, fetch remote policies or
perform regex rewrites over finished C# text.

### Matching

Match Roslyn node kinds and token structure, never raw source lines.

- Match an entire eligible expression-bodied method before normal printing starts, including
  one whose enabled built-in conversion produces an expression body.
- Recognize one or more configured wrapper invocations leading to a terminal block.
- Allow generic invocation spellings while retaining their exact type-argument tokens.
  Configure the callee without type arguments; matching accepts either generic or nongeneric
  invocation names and preserves the source's arguments.
- Locate the callback structurally. A last-argument selector supports preceding context,
  logger or cancellation arguments without assuming the lambda is argument zero.
- Preserve existing `async`, `static`, `await`, argument names and other tokens. A layout
  rule must not repair or invent asynchronous control flow.
- Match callee spellings case-sensitively. This is syntax matching, not symbol resolution:
  aliases, qualification variants and unrelated same-named calls need explicit selectors.
- Reject ambiguous matches and content trivia on compacted wrapper/header boundaries with a reason.

File filters are relative to the rule pack. Existing explicit file selection, generated-file
exclusions and source suppression still take precedence. Text in strings, comments and
inactive conditional branches is not invocation syntax to match.

### Layout ownership

A matched rule produces a typed layout plan. It owns named whitespace boundaries, not an
unrestricted replacement string or an entire file.

For the example recipe, ownership covers:

1. The closing parameter delimiter and the declaration's arrow/root-await seam.
2. The breaks and base indentation before each wrapper invocation.
3. The terminal lambda's opening brace and block base indentation.
4. The compact closing invocation delimiters and final semicolon.

Keep each wrapper's callee path and lambda introducer in its header group instead of sending
that spine through the ordinary fluent-chain wrapping rule. Preceding argument expressions
still use normal formatting. This prevents a global receiver-on-own-line preference from
splitting the very wrapper header the custom rule is supposed to align.

The terminal block's statements are still printed by the normal formatter at the supplied
base indentation. Ordinary spacing, nested statements, comments and other configured
formatting inside the block do not become exempt.

With a declaration indented by four spaces, each wrapper call and the terminal brace start
at column four; the terminal block's statements start at column eight. The parameter close
and arrow/root await remain attached to the last parameter's line. Names and indivisible
tokens must never be shortened to meet a width.

Headers form output-measured groups. When a header does not fit, its preceding arguments
and callback introducer break at normal argument boundaries one level inside the wrapper.
The next wrapper and terminal brace still use the declaration's base indentation.
Indivisible tokens may exceed the width, as in ordinary formatting.

Version 1 rejects overlapping custom ownership rather than choosing a winner by incidental
rule order. Nested custom matches are admitted only where the outer layout explicitly
delegates ownership. Conflicts never leave a partially customized file.

## Engine integration

The existing document arena already supplies groups, hard/soft breaks, indentation and
output anchors. Compile rule data into those existing primitives and a small set of typed
operations such as capture, align, break, indent and delegate.

The implementation belongs at the boundary between `Node.Print`, declaration/arrow printing
and invocation/lambda printing. Select the rule once at the matched owner; emit its wrapper
spine once; delegate the body once. Do not print normally and then dedent or regex-rewrite
the resulting text.

Compose matching with the built-in syntax-rewrite plan. For example, converting a block-bodied
method to an expression body can make a wrapper rule eligible. The converted shape and its
custom layout must be planned together on the first pass, with the built-in token deltas
still declared. A recipe that cannot support such a combination must refuse it explicitly,
not emit ordinary output that only becomes custom-formatted on a second run.

The layout must depend on syntax, options and current output groups. Never match on the
input's columns or line counts to choose a layout that the same rule will change.
This makes a joined input, a previously wrapped input and an already-canonical input reach
the same output under deterministic mode.

Both preservation and deterministic modes need explicit semantics. The selected custom
wrapper and raw-string boundaries are canonical in either mode; preservation remains in
effect only for delegated, unowned layout. The condition recipe is restricted to deterministic mode.
Explain those deliberate local overrides to the user.

## Safety and failure behavior

- Custom layout operations may rearrange whitespace only. Token insertion, deletion,
  reordering and duplication are outside their authority.
- Every matched file receives strict content verification and a forced token reparse.
  Existing legitimate built-in rewrites retain their own exact delta declarations.
- Comments, directives, literals and disabled text retain existing protection. A seam
  blocked by unsupported trivia is refused, not flattened through.
- Rule files have a strict schema, unique IDs and bounded size, nesting, rule count and
  matching work. Unknown fields and unsupported schema versions are errors.
- Missing, invalid, ambiguous or conflicting policy fails explicitly before a write and
  cannot produce a cache hit or an MSBuild success stamp.
- No remote imports or executable plugins are accepted in version 1.
- A successful first pass must be a fixed point. Repeating formatting until it converges
  is not an implementation technique or a recovery policy.

The default path without custom rules must retain existing output and conditional
verification behavior.

Use the existing CLI outcomes: clean output is exit `0`, check-only drift is `1`, and
invalid policy, unsafe layout or conflicting ownership is failure exit `3`.

## Configuration, caching and build integration

The CLI/configuration layer loads files through `IFileSystem`, validates them once and
creates an immutable compiled rule set. Core receives typed data; it performs no file
discovery, JSON loading, assembly loading or semantic analysis.

The cache combines resolved EditorConfig values with the policy bytes and logical base
directory. The tool version already pins the compiler/renderer implementation. A policy edit
invalidates the cache even when its path, file timestamp and source bytes do not change.

MSBuild adds selected rule files through `Curb_LayoutDependenciesFile`. The CLI records their
paths; later builds read that manifest before checking target incrementality. A separate
settings file tracks explicit policy/base overrides. Missing dependency files invalidate the
stamp instead of making a missing policy look like an up-to-date build.

Freeze rule data for a formatting invocation. Worker threads share immutable compiled
plans, not mutable per-match state. Keep rule lookup indexed by root syntax kind and
callee tokens; do not scan every rule at every syntax node.

Repository hooks that format staged content must use the staged policy snapshot too.
Pass `--layout-rules` for the snapshot file and `--layout-rules-base` for its original logical
directory. The base controls relative file filters, so a temporary snapshot location does not
change which source files match. `Curb_LayoutRulesBase` is the corresponding MSBuild property.

No-rule performance needs a no-extra-traversal control. Enabled-rule cost needs Native-AOT
measurements for matching and nonmatching files on the existing corpus, not JIT timings
or an assumed percentage budget.

## Reference formatters and diagnostics

Do not add a diagnostic suppressor for the motivating example: the measured layout already
satisfies stock formatting and `IDE0055`. Keep exact reference tests for the initial recipe.

A general custom policy can nevertheless ask for output a different formatter changes.
Such a policy needs explicit consumer acceptance and separately scoped compatibility
evidence; the extension mechanism cannot make stock Roslyn understand arbitrary rule packs.

Diagnostic severity and formatter ownership are different controls. Setting `IDE0055` to
`silent` does not teach an IDE's Format Document command a new layout. Setting it to `none`
is especially unsuitable as a shortcut: current {{product}} binding excludes the whole file.
Do not silently change that contract.

If a future consumer intentionally needs a Roslyn-incompatible layout while retaining
`IDE0055` enforcement elsewhere, evaluate a separately packaged diagnostic adapter.
It must suppress only demonstrated custom-owned whitespace diagnostics on fresh,
already-canonical source. Broad method-span or file-wide suppression is unacceptable.
Diagnostic span granularity, mixed owned/unowned changes and IDE-host compatibility are
feasibility gates, not assumptions. It must not move compilation or Workspaces into Core.

Even a diagnostic adapter does not change the stock formatter's output. Consumers must
choose an authoritative formatter for custom-owned layout rather than run competing tools
until one wins.

## Explainability and validation

Use `curb explain-layout` to report the resolved policy path, logical base, fingerprint,
matched rule ID, original owner/header span and actual selected recipe without writing source.
Application spans are explanatory, not exclusive ownership regions: a wrapper's delegated
body can contain independently owned condition headers.
Refusals and configuration errors use `CURB1008`; normal format failures retain their
existing failure summary and exit code. `print-config` and `doc-tree` resolve the same policy.
Do not log source bodies or unbounded private configuration payloads by default.

The acceptance matrix must include:

| Area | Required proof |
|---|---|
| Requested shape | Exact declaration, parameter close, arrow/await, wrapper, brace and closing-delimiter columns |
| Enforcement | Poorly formatted matching input becomes canonical; this is not verbatim preservation |
| Body delegation | Incorrect spacing and nested statements inside the callback are still corrected |
| Variants | One/multiple wrappers, generic calls, leading non-lambda arguments and preserved optional awaits |
| No-match controls | Unrelated calls, different callback positions and unsupported shapes stay under ordinary formatting |
| Rewrite composition | Built-in conversions that change rule eligibility compose in one plan or fail explicitly, never require another format pass |
| Safety | Comments, directives, literals, conditional text and invalid output retain fail-closed handling |
| Stability | LF/CRLF, tabs/spaces, width modes and a first-pass fixed point |
| Conflicts | Overlapping ownership and malformed configuration fail without writes or cache entries |
| Configuration | Nested EditorConfig origin, explicit overrides and staged policy snapshots resolve consistently |
| Cache/build | Policy-content-only edits invalidate both cache layers and MSBuild stamps |
| Reference | Initial recipe remains a stock formatter fixed point and builds with `IDE0055` enforced |
| Performance | Native no-rule, no-match and matching costs stay within explicitly measured release gates |
| Raw literals | Exact token/value text, closing indentation, LF/CRLF content, quote/dollar counts and UTF-8 suffixes survive every supported parent context |

## Validation ownership

The focused layout tests exercise the rule compiler, configuration, cache and source
contracts. The native workflow runs the standalone layout smoke on every supported RID.
The package job additionally exercises actual MSBuild package imports and rule-only edits.
Complete conformance, idempotency, churn and AOT gates remain required for release.

The original architecture choice is recorded in
[ADR-0003](../adr/0003-repository-owned-layout-rules.md).
