---
navigation_title: Reflow
description: What max_line_length does — it sets a width and it selects how Curb decides line breaks — and what each mode costs.
---

# Reflow

Reflow is the one layout decision
[IDE0055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0055) has no
opinion about: where to break a line that is too long. `dotnet format` never wraps. Prettier-style
formatters always do, and decide everything else themselves.

`max_line_length` is where {{product}} splits the difference:

```ini
[*.cs]
max_line_length = 120
```

## The two modes

| | **No `max_line_length`** | **A width** |
|---|---|---|
| Line lengths | never changed | wrapped to the width |
| Where breaks come from | yours, reproduced | the tokens and the width |
| Formatting twice | required to be idempotent | required to be idempotent |
| Fixed point of `dotnet format` | 1196/1196 | **1196/1196** |

With no width, {{product}} fixes indentation, spacing, brace placement and blank lines without
width-driven reflow. Those formatting changes can still change a line's character count.

With a width, {{product}} owns the layout. Line breaks become a function of your tokens and your
width, not of where the previous author happened to press return. A construct either fits on one line
or breaks; there is no third state carried over from the file's history.

## Formal properties

Two properties guide the design, and they are different properties.

*Idempotency* — `f(f(x)) = f(x)`. The first output must already be a fixed point. A file that needs a
second formatting pass violates this requirement even if it eventually settles. Tests and corpus
runs measure the property; the design alone does not prove that every implementation or combination
of syntax rewrites satisfies it.

*Input-independence* — `f(x) = f(y)` when `tokens(x) = tokens(y)`. This is the stronger property,
and it only holds in the width mode. Two files with different whitespace but identical tokens produce
identical formatted output. In formal-methods terms it is closest to *confluence* — all starting
points reach the same normal form — or *canonicality*. Without a width, `csharp_keep_existing_linebreaks`
reproduces the breaks the author chose, so the property does not hold; that is deliberate.

**Current divergence:** some initializer and comment patterns can still feed output layout back into
the next run. See [known limitations](../known-limitations.md). This is not a reason to weaken the
first-pass fixed-point requirement.

## Writing layout rules

Read a source-layout property only when the formatter preserves that property. A break decision
must not depend on an input column or line count that the same formatting pass can change.

Use `PrintContext.AuthorBroke` and `AuthorJoined` for preserved source breaks. Their mode gate is
necessary, but preservation combined with a finite width still needs care where reflow moves a
construct. A raw same-line check is not a substitute.

When a decision depends on Curb's output, aim it at the current named group. Put coupled constructs
in one measurement when joining one changes whether the other fits. Reference a group's state only
after the printer has resolved it.

Test the first and second output, nesting, trivia, both modes and relevant option combinations.
Compare reference conformance and churn against a measured baseline. Keep reference copies outside
the tree being formatted so the formatter cannot modify both sides of a comparison.

## What it costs

See [Churn](../benchmarks/churn.md) for the file and line counts across configurations, and a worked
example of what {{product}}'s first run looks like on a repository that is already `dotnet format`-clean.

## Keeping your own line breaks

```ini
[*.cs]
max_line_length = 120
csharp_keep_existing_linebreaks = true
```

Now {{product}} only breaks lines that are too long, and reproduces the breaks you already have. This is
the closest thing to "wrap, but do not touch anything else".

Preservation needs additional care where width-driven reflow changes a source property another rule
reads. Several ReSharper keys are unavailable in that mode and report why; see
[what needs a width](#keys-that-need-a-width).

`csharp_keep_existing_linebreaks = false` without a `max_line_length` is refused (**CURB1007**). With no
width nothing is ever too long, so it would join every construct in the file onto one line.

## Adopting a width

Setting a width on an existing repository is one large commit. `curb check` will report most of your
files on the first run, and [the build integration](../workflow/msbuild.md) will rewrite them on the
first compile.

Nothing forces you to take it in one step. A repository can adopt {{product}} with no `max_line_length`
— where it is a `dotnet format whitespace` equivalent — and add a width later, as its own commit, when
the churn is convenient.

`curb print-config Foo.cs` prints every resolved option and says which mode you are in and what selected
it. Worth running before the reformatting commit rather than after.

## Keys that need a width

Some ReSharper wrapping keys are only honoured under deterministic layout, and setting one without a
width reports **CURB1005** rather than going quiet:

| Key | What it does |
|---|---|
| `csharp_wrap_arguments_style = chop_always` | every argument on its own line, fit or not |
| `csharp_wrap_object_and_collection_initializer_style = chop_always` | the same for initializers |
| `csharp_place_method_attribute_on_same_line = if_owner_is_single_line` | join an attribute to a member that fits on one line — also `_field_`, `_property_`, `_event_` |

Forcing a construct to wrap also moves nested constructs. A rule that reads their old indentation can
then choose differently on the next pass. The deterministic implementation uses output decisions
instead, which makes these keys admissible there. Both modes still need measured fixed-point coverage.

Two IDE0055 keys go the other way. `csharp_preserve_single_line_blocks` and
`csharp_preserve_single_line_statements` ask {{product}} to keep what *you* put on one line, which a
width takes out of their hands; both report **CURB1004** with what they still do. An empty `{ }` stays
collapsed either way.
