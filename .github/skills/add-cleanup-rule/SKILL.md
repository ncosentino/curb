---
name: add-cleanup-rule
description: Add or change a diagnostic-driven Curb cleanup rule with verified spans, safe rewrites, refusal cases and corpus evidence.
---

# Add a cleanup rule

Read the applicable instructions and [cleanup contract](../../../docs/workflow/cleanup.md).
Stop if the fix cannot be derived from the rule ID, diagnostic span and syntax alone.

## Confirm the diagnostic

Use a disposable project and an actual analyzer-enabled build with a SARIF 2.1 error log.
Read the reported span before implementing the fixer. Field identifiers, type names,
keywords and runs of usings are not interchangeable diagnostic locations.

Do not construct a compilation or semantic model inside Curb or its tests. An external
build supplies the verdict; the cleanup engine must not recreate it.

## Implement the safe rewrite

1. Add an `ICleanupRule` implementation and declare whether it needs a complete span.
2. Resolve the span and enforce the exact node-kind and shape gates.
3. Refuse stale, unrelated, ambiguous or unsupported shapes with a reason.
4. Register the rule and update its `RuleCatalog` owner/delta in the same change.
5. Record the exact inserted, removed or reordered spans without excusing unrelated text.

Do not rename symbols, delete declarations or guess a missing type. Preserve comments,
directives, generated-file exclusions, freshness checks and conditional-symbol safety.
Overlapping edits must not leave a partially applied diagnostic.

## Test both outcomes

Add a positive case for every supported shape and a refusal case for every nearby
unsafe shape. Use the existing cleanup expectation dump.

When a token delta is new, update both verifiers and add negative tests from both sides.
A verifier that accepts the intended rewrite and unintended damage is not sufficient.

The existing cleanup-expectation check establishes a fixed point of the reference style
formatter. A genuine incompatibility belongs in the existing divergence registry with
an explanation.

## Corpus readiness

Both cleanup corpus checks are required for a token-changing rule:

- the adversarial safety corpus supplies wrong verdicts and exercises refusal/verification;
- the build-clean-rebuild corpus checks compilation and the reference-style fixed point.

Neither a successful compile nor token equality alone proves semantic correctness.
Record measured results in the maintained cleanup documentation, not merely intent.
Use approved fork CI for these complete checks and keep the change unready if the
required evidence is unavailable.
