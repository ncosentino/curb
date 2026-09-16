# Curb agent guidance

This is the independently maintained `ncosentino/curb` fork.
Preserve Nullean and contributor attribution, the MIT license, and `NOTICE`.

## Read before editing

- Start at [the documentation map](docs/index.md) and follow relevant design and workflow pages.
- Resolve matching rules with `build\guidance\Get-ApplicableInstructions.ps1`;
  read every returned instruction before editing its matching files.
- Keep unchanged shared imports in `.github/instructions/genesis/` and local rules in
  `.github/instructions/curb/`. Exclude conflicting imports; do not layer contradictions.
- Refresh only the reviewed import set; never run an unfiltered instruction sync.
- Code, manifests, tests, and workflows expose stale prose. Investigate contradictions.
- Consult `docs/adr/` for architectural decisions; do not rewrite accepted reasoning.

## Boundaries

- Keep the engine parser-only and Native-AOT-compatible. Never add Workspaces,
  `CSharpCompilation`, semantic models, reflection, or dynamic code generation.
- Never weaken content verification, safe-write handling, or idempotency requirements.
- Preserve the boundaries: Core has no IO or configuration source; Cleanup consumes build verdicts.
- Keep credentials, private repository information, and local machine context out of
  tracked files, commits, issues, comments, and pull-request text.

## Fork operations

- Scope every GitHub write to `ncosentino/curb` and verify the destination explicitly.
- Do not open or comment on upstream issues or PRs, request upstream reviews,
  mention maintainers, or run automation that notifies them.
- Use `redirect.github.com` for necessary upstream issue or PR links in our conversations
  to avoid creating backlinks. Preserve fork-only Git and GitHub CLI defaults.
- Leave inherited publishing disabled unless separately authorized.
  [Fork maintenance](docs/workflow/fork-maintenance.md) owns the operational details.

## Working and delivery

- Start worktrees and feature branches from freshly fetched `origin/main`.
  Never push directly to the remote default branch or discard someone else's changes.
- Use headless commands and targeted checks. Complete suites, corpus runs, and AOT
  matrices belong to explicitly approved fork CI. Disabled CI is not passing evidence.
- Delegate only independent work, with at most two agents and ten minutes per agent.
  Set exact deadlines, retain results, and take over overdue work.
- Run the [review procedure](.github/skills/review-changes/SKILL.md) before delivery.
  Report unverified work. Commit, push, and PR creation require explicit authorization.
