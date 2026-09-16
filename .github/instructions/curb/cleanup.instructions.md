---
applyTo: "src/Nullean.Curb.Cleanup/**/*.cs,src/Nullean.Curb.Core/Options/RuleCatalog.cs"
scope: "diagnostic-driven cleanup and rule ownership"
---

# Cleanup contracts

- Admit a fix only when the diagnostic's rule ID and span plus syntax determine it.
  Do not load a compilation, resolve a type, rename symbols or delete declarations.
- Confirm a new rule's actual diagnostic span in a real analyzer-enabled build.
  Do not infer the reported token from the rule's name.
- Declare `NeedsSpan` and enforce node-kind/shape gates before every rewrite.
  A stale or unrelated location must be refused with a reason, not guessed.
- Preserve the CLI freshness gate, generated-file exclusions and conditional-symbol
  safety. A verdict for one build configuration does not authorize changes to others.
- Keep rule registration and `RuleCatalog` ownership/delta declarations in the same
  change. Derive supported rules from those owners, not a second manual list.
- Declare exact inserted, removed and reordered spans. Keep both content and token
  verification strict outside those deltas.
- Test both sides of a new permitted delta: valid rewrites and nearby invalid changes
  that the verifier must still reject.
- Every supported shape needs a positive case and a refusal case. Preserve comments,
  directives and unrelated tokens; discard overlapping fixes together.
- Use both cleanup corpus targets before declaring a token-changing rule ready.
  Compilation and fixed-point checks complement syntax guards; passing a build alone
  is not proof that semantics are unchanged.

Procedure: [adding a cleanup rule](../../skills/add-cleanup-rule/SKILL.md).
