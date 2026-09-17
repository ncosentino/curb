---
navigation_title: Known limitations
description: Current limitations in Curb's formatting output and what to expect on first adoption.
---

# Known limitations

## Preview union syntax

Union declarations and their members use the type and member printers rather than verbatim fallback.
The implementation follows the pinned parser's experimental syntax representation. The separate
preview smoke project checks wrapping and analyzer conformance with its pinned .NET 11 SDK; this is
not a guarantee of compatibility with every later preview grammar.

## Expression-body conversions do not compose in one pass

With both `csharp_style_expression_bodied_properties = true` and
`csharp_style_expression_bodied_accessors = true`, the two conversions can produce different output
on consecutive passes:

```csharp
public int Count { get { throw new NotImplementedException(); } }   // source
public int Count { get => throw new NotImplementedException(); }    // run 1
public int Count => throw new NotImplementedException();            // run 2 — different
```

The accessor-level conversion fires on run 1. The property-level conversion only recognises an
accessor list that already has an arrow getter, so it fires on run 2. Two rules that should compose
in one pass currently do not.

## Anchor regression boundaries

The argument-list initializer case now anchors to the output group's line decision rather than the
argument's original inline placement. It reaches the same indentation on the first and second pass,
including when preservation is combined with a finite width.

Trailing-comment anchors now reset to the enclosing indentation when the output introduces a
blank line. A section comment after commented attributes no longer inherits the previous
trailing-comment column on its first pass. Continuous comment runs retain their alignment.

These fixes cover the reproduced initializer and attribute-comment cases, not every possible
trivia arrangement. New failures still need a specific input, configuration and first/second output.
