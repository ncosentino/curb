---
applyTo: "src/Nullean.Curb.Core/FormatOptions.cs,src/Nullean.Curb.Core/Options/**/*.cs,src/Nullean.Curb.EditorConfig/**/*.cs,tools/Nullean.Curb.OptionDocs/**"
scope: "formatting options, binding and reference generation"
---

# Formatting options

- Preserve value-oriented `FormatOptions` and the dedicated EditorConfig binder.
  Do not replace them with DI options classes, injected monitors or startup annotations.
- Support existing ecosystem keys rather than inventing equivalent preferences.
  Read implemented coverage from the catalogs and executable option listing.
- Keep catalog declarations, allowed/default values, bit slots, descriptors, binding,
  helper behavior and test cases consistent. Do not mark an unimplemented key complete.
- Bind the existing-line-break override immediately after width. Diagnostics that
  depend on the resolved mode must see the final mode, not an intermediate default.
- Use the existing diagnostic family for unsupported mode/value combinations. State
  the actual degradation per construct; do not silently accept a partly honored value.
- Keep each option's policy in a helper and have affected printers call it.
- Test every supported value through the real binder, including opt-in/default,
  invalid-value and relevant mode behavior. A catalog entry without exercised cases
  is not implemented coverage.
- Generate the option reference through its owning tool and build target.
  Never hand-edit generated examples, indexes or their line-ending bytes.

Procedure: [adding an option](../../skills/add-formatting-option/SKILL.md).
