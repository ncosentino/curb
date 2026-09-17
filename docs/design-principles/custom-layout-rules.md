---
navigation_title: Custom layout rules
description: Repository-owned syntax rules for specialized layouts without source annotations.
---

# Repository-owned layout rules

Version 1 supports the `lambda-wrapper-chain` matcher and `vertical-wrapper-chain` recipe.
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
required. Version 1 accepts the shown matcher and recipe values:
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

The matcher/recipe pair handles nested invocations whose callback lambdas lead to a
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
boundaries are canonical in either mode; preservation remains in effect only for delegated,
unowned layout. Explain those deliberate local overrides to the user.

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
matched rule ID, original owner span and selected recipe without writing source.
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

## Validation ownership

The focused layout tests exercise the rule compiler, configuration, cache and source
contracts. The native workflow runs the standalone layout smoke on every supported RID.
The package job additionally exercises actual MSBuild package imports and rule-only edits.
Complete conformance, idempotency, churn and AOT gates remain required for release.

The original architecture choice is recorded in
[ADR-0003](../adr/0003-repository-owned-layout-rules.md).
