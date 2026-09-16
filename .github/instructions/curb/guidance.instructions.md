---
applyTo: "AGENTS.md,CLAUDE.md,.github/copilot-instructions.md,.github/guidance*.json,.github/instructions/**,.github/skills/**,.claude/skills/**,build/guidance/**,tests/Nullean.Curb.Tests/Guidance/**,docs/adr/**,docs/workflow/agent-guidance.md"
scope: "agent guidance ownership, discovery and structural validation"
---

# Guidance ownership

- Keep shared imports unchanged under `genesis/` and Curb-owned rules under `curb/`.
  Conflicting shared files are absent, not shadowed by contradictory local wording.
- The import manifest is an exact target-path/hash allowlist. An extra shared file,
  missing file or changed imported byte is a failure, not an automatic refresh.
- Never run unfiltered synchronization. Review a new source snapshot and its exact
  selection before refreshing; do not introduce a second synchronization framework.
- Public metadata contains only target-relative paths and approved content hashes.
  Keep source-private identifiers, issue links, workstation paths and audit ledgers out.
- Give every rule, rationale and procedure one owner before reducing root guidance.
  Root files route; scoped files govern edits; docs explain; skills own procedures.
- Preserve accepted ADR reasoning. Use the declared ADR index and lifecycle for a
  significant new ownership decision rather than modifying an accepted decision.
- Resolve and measure shared plus local instructions together. Use the declared
  individual and matching-context budgets; do not claim a custom-only measurement.
- Keep review and command discovery tied to actual manifests, F# targets and workflows.
  Include F# projects and build sources; exclude outputs rather than the build tree.
- Structural checks protect ownership, paths, hashes, scope and budgets, not literal
  prose. Cover malformed metadata, unexpected imports, changed bytes, bad globs and
  missing links/owners with failing examples.
- Treat existing unrelated code divergence separately from the current review verdict.
  Do not silently expand modernization into formatter fixes or CI enablement.
