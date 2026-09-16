---
navigation_title: Known limitations
description: Current limitations in Curb's formatting output and what to expect on first adoption.
---

# Known limitations

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

## Anchor columns feed back into the next run

The argument-list initializer case now anchors to the output group's line decision rather than the
argument's original inline placement. It reaches the same indentation on the first and second pass,
including when preservation is combined with a finite width.

The broader historical report also mentions comment alignment without an isolated example.
That comment-only case has not been established as fixed. New failures need a specific input,
configuration and first/second output; do not assume that one corrected initializer covers all trivia.
