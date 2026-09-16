---
applyTo: "docs/**"
---

# Curb documentation

- Keep the docs-builder site, `docs/index.md` entrypoint and `_docset.yml` navigation.
  Make every maintained page discoverable; do not create a parallel documentation map.
- State current behavior or an explicit target. Put known gaps in a concise
  `Current divergence` note, not a rollout diary or a promise that tests cannot prove.
- Start with the page's purpose. Use active voice, concrete words, one idea per
  sentence, short paragraphs and contractions where natural.
- Use real tables or lists for parallel items, not repeated bold-term mini-sections.
  Avoid scene-setting introductions, wrap-up conclusions and decorative claims.
- Limit em dashes. Avoid "not just X, but Y", "not only X but Y", "No X. No Y.
  Just Z.", "This is where X comes in" and "Worth noting that".
- Avoid delve, leverage, robust, comprehensive, seamless, transformative, holistic,
  realm and figurative landscape. Prefer specific technical descriptions.
- Use `{{product}}` in prose and literal Curb in headings so generated anchors remain stable.
- Preserve truthful public project, tool and attribution URLs. Sample deployments use
  reserved domains/placeholders; never publish credentials, private infrastructure,
  private repository context or a developer's filesystem paths.
- `docs/reference/**` is generated. Change descriptors/snippets and run the owner;
  do not edit generated pages, counts, indexes or the CRLF example by hand.
- `curb-landing.html` is hand-maintained. Its base URL, the build prefix and the
  workflow prefix must agree. Build before previewing; agents use headless mode.
- Do not change hosting, crawler/training policy or publishing as a documentation
  cleanup. Inherited automation stays disabled until separately authorized.

Details: [documentation workflow](../../../docs/workflow/documentation.md).
