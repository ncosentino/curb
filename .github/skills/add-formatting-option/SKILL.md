---
name: add-formatting-option
description: Add or change a Curb formatting option, including binding, layout behavior, expectations and reference documentation.
---

# Add a formatting option

Keep one option isolated. Resolve applicable instructions and read the existing
[layout-mode contract](../../../docs/design-principles/reflow.md) before changing breaks.

## Establish the contract

Find the existing ecosystem key, values and reference behavior. Do not invent another
spelling. State whether it applies in preservation mode, deterministic mode or both.
Classify reference behavior from evidence rather than assuming a formatter honors a key.

Locate prior helpers and similar tests before adding logic. Read the actual catalog,
descriptors and binder; do not rely on a copied supported-option count.

## Implement the whole path

1. Update `OptionCatalog`, the descriptor entry and `FormatOptions` as needed.
2. Bind values through `EditorConfigOptionsBinder`. Preserve width/mode resolution order.
3. Put the policy in the existing spacing, brace or layout helper.
4. Wire every affected printer through that helper.
5. Report invalid or inapplicable modes/values through the existing diagnostics.

Mark a key implemented only when its behavior exists. A catalog declaration alone is
not support.

## Prove the behavior

Use the `FormattingTest` harness with EditorConfig text so the real binder is exercised.
Cover each allowed value, the default/opt-in distinction, invalid input and applicable
layout modes. Include cases that remain unchanged when the option is absent.

Expected text should come from the reference formatter where it defines the behavior.
For wrapping or blank-line choices it does not define, derive and justify the shape.
Never accept Curb output as its own evidence.

Require content/token preservation and a first-pass fixed point. For line-break changes,
exercise nesting, comments/directives and output-based group decisions.

Generate the reference through the existing `options` target. Do not edit generated
examples or indexes.

## Readiness

Discover commands from the F# runner and workflows. Run the narrow tests while iterating.
The existing expectation, conformance and churn checks own broader evidence; compare
both modes and a measured baseline for layout changes.

Complete corpus and AOT work belongs to approved fork CI. Record missing evidence and
do not declare the option ready while required checks are unavailable.
