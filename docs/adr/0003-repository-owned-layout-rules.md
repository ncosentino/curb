---
navigation_title: Repository-owned layout rules
description: Data-only extension boundary for repository-specific syntax layouts.
status: Accepted
date: 2026-09-17
supersedes: ""
superseded_by: ""
---

# ADR-0003: Repository-owned layout rules

## Context

A repository may prefer a specialized layout for selected nested callbacks while retaining
ordinary formatting everywhere else. Current source suppression preserves text but does
not establish that layout as a canonical rule. Application-specific printer branches would
make the independently maintained fork depend on consumer naming and conventions.

A neutral declaration-aligned wrapper example is already accepted by stock formatting with
SDK `10.0.303`. The immediate requirement is configurable selection and rendering, not an
assumed need to suppress Roslyn diagnostics.

## Decision

Introduce repository-owned declarative layout rule packs. Compile bounded syntax selectors
and layout operations into the existing document pipeline. Core remains parser-only,
IO-free and Native-AOT-compatible. The repository supplies invocation spellings; the engine
supplies reusable syntax and layout capabilities.

The first recipe covers vertical lambda-wrapper chains. It controls explicit whitespace
boundaries and delegates normal formatting inside the terminal callback body. It does not
preserve an entire method verbatim or rewrite completed output.

Add an explicitly namespaced EditorConfig reference for the rule pack, with equivalent CLI
and MSBuild routing. This is an explicit, opt-in exception to the standard-key-only
convention; it is not a claim that other formatters understand the new key.

Retain strict content/token verification and first-pass idempotency. Preserve the default
reference-compatible path and require reference evidence for the initial recipe. A future
intentional incompatibility needs explicit acceptance and cannot be hidden by suppression.

The maintained design and acceptance criteria live in
[custom layout rules](../design-principles/custom-layout-rules.md).

## Alternatives

| Choice | Benefit | Cost |
|---|---|---|
| Source pragmas or markers | Existing localized escape hatch | Pollutes source and preserves rather than enforces layout |
| Configuration-selected verbatim regions | No source annotations | Leaves incorrect body formatting untouched and does not canonicalize new input |
| More global formatting options | Smallest implementation for a universal preference | Cannot reliably target repository-specific wrapper shapes |
| Regex postprocessing | Easy demonstration | Ignores syntax/trivia ownership and risks oscillation with normal formatting |
| Runtime managed plugins | Arbitrary custom code | Conflicts with Native AOT, introduces code execution and versioning costs |
| Declarative syntax/layout rules | Repository ownership, bounded execution and normal body formatting | New schema/compiler and explicitly supported operations |

## Consequences

Rule contents become part of formatter configuration and cache/build dependencies.
Matching is syntactic, not symbol-based. Conflicts and unsupported boundaries are explicit
failures rather than implicit rule ordering.

The first version is deliberately bounded. New structural operations need engine work,
but new application-specific names or combinations of supported operations do not.

No diagnostic adapter is required for the measured motivating shape. Any later adapter
must remain separate from the native formatter and prove narrow suppression without
claiming to alter stock IDE formatting.

## Confirmation

The initial release supports the named vertical wrapper recipe with bounded syntax matching.
Its schema, layout, safety, cache, reference and Native-AOT gates are defined in the maintained
design. It adds no runtime managed-plugin loading or diagnostic suppressor.
