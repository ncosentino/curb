---
applyTo: "src/Nullean.Curb.Core/**/*.cs"
---

# Formatter engine

- Emit source spans, not `token.Text`, `node.ToString()`, `ToFullString()`, or copied
  syntax-tree text. Prefer typed Roslyn properties over allocating traversal APIs.
- Do not use LINQ or `DescendantNodes()` in the printer hot path. Budget lazy red-node
  materialization and measure allocation changes, not only elapsed time.
- Keep arena and output buffers reusable across files. Every rented buffer has one
  owner, a bounded lifetime and a return path to its original pool. Never assume the
  rented capacity equals the requested length or retain spans after return.
- Retain existing `ArrayPool`-based ownership; do not add pooling libraries to Core.
  Clear sensitive contents before return and reset reused state between files.
- Content verification always protects writes. Preserve conditional token reparse
  risk detection and the ability to force it in tests; neither proves idempotency.
- Unknown syntax and disabled conditional-compilation text must remain safe.
  Preserve literal/comment/directive content and report unsupported or unsafe cases.
- A successful first pass must be a fixed point. Do not hide instability with cache
  hits, repeated formatting, or relaxed expected output.
- Width selects layout mode unless the explicit line-break option overrides it.
  Use `AuthorBroke`/`AuthorJoined` only for source properties genuinely preserved.
- If Curb can change a property, decide from the current output group instead of
  reading that property back from source. Keep related decisions in one group when
  their measurements depend on each other; only reference groups already resolved.
- Do not use raw same-line checks to choose breaks or read options directly from
  syntax printers where an existing spacing/brace/layout helper owns the decision.
- Cover preservation and width-driven behavior, nested constructs, and a second pass.
  Use the existing reference-conformance and churn targets for layout changes.
- Measure production performance with the native-AOT binary. JIT microbenchmarks
  explain components but do not establish the CLI's cold latency or allocation cost.

Rationale: [layout modes](../../../docs/design-principles/reflow.md) and
[performance](../../../docs/design-principles/performance.md).
