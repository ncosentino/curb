---
navigation_title: Design principles
description: The decisions that shaped Curb's architecture and where each came from.
---

# Design principles

## Parser-only, no compilation

{{product}} uses Roslyn's parser. It does not create a workspace, load project files, or resolve references. Nothing here touches a symbol table.

That constraint is the whole design. A formatter that needs a compilation needs a build first, which means it cannot run inside a build. It also means it takes 40-odd seconds to start on a large solution, where a parser-only pass takes milliseconds.

The tradeoff is explicit: semantic code style — unused variable detection, naming convention checks, `var` decisions — needs to know what a name means. {{product}} deliberately leaves that to the tools that already do it. What a formatter needs to decide layout and fix syntax-level code style, it can read from tokens and the parse tree.

## Full reprint, not a trivia rewriter

Many formatters keep the AST and patch the trivia — the whitespace and comments that sit around tokens. It sounds cheaper. It is not: a trivia rewriter cannot change line breaks, and changing line breaks is the point.

{{product}} reprints every file from scratch. The printer walks the parse tree and emits every token into an output buffer, inserting exactly the whitespace the configuration says it should. That is the only way reflow works.

## Document IR in a pooled arena

Between the parse tree and the printer sits a document intermediate representation: a Prettier-style tree of `Text`, `Group`, `Indent`, `IfBreak` and `Line` nodes. The printer uses Wadler-Lindig's algorithm to decide where to break each group.

The IR lives in a pooled struct array rather than a class-per-node graph. Format runs reuse the arena between files. The debug document validator checks structural properties of that IR. It does not prove that formatting twice produces the same text; formatting tests and corpus checks measure that separate property.

## Conditional round-trip reparse

After printing, {{product}} re-parses its own output and compares the token streams. This catches the one class of printer bug that a content check misses: closing the gap between two tokens and accidentally welding them into something new.

The reparse is conditional: the printer tracks whether it did anything capable of moving a token boundary — closing a gap, or emitting content after a line comment on the same line — and only triggers the reparse when it did. For code that never closes a gap, the check is provably redundant and is skipped. See [Safety](safety.md) for what is always checked and what the reparse covers.

## Unknown syntax is emitted verbatim

{{product}} does not have a dedicated printer for every construct in C#. Anything it has not learned yet is passed through exactly as it was in the source. The printer's coverage grows without ever putting code at risk — a file that exercises an unrecognised construct is formatted everywhere else and left alone there.

You can see current coverage with `curb check ./src --coverage`.

## Deliberately non-canonicalising

Preservation mode intentionally permits two files with the same tokens to retain different layouts. Both layout modes require idempotency: `format(format(x)) == format(x)`. Known failures remain defects rather than evidence that the requirement is optional.

The reason is `csharp_preserve_single_line_blocks` and `csharp_preserve_single_line_statements`, which are on by default. A one-liner block stays a one-liner because that is what the author chose, and `dotnet format` leaves it there too — formatting it differently would rewrite 11,000 files on roslyn alone on the strength of a style choice the repository never asked for.

With `max_line_length` set and `csharp_keep_existing_linebreaks = false`, layout is intended to depend on tokens, options and width rather than inherited breaks. Implementation mistakes and composing syntax rewrites can still violate a fixed point. Measure the output and use [output-based layout decisions](reflow.md#writing-layout-rules) rather than treating the design as proof.

## No invented keys

{{product}} reads configuration from your `.editorconfig` and invents no keys. See [Working with existing tooling](existing-tooling.md) for the full key surface and how unrecognised keys are handled.
