---
navigation_title: Repository guidance
description: Where Curb's agent rules, rationale and procedures live, and how their ownership is enforced.
---

# Repository agent guidance

{{product}} keeps recurring agent context small while retaining its engineering constraints. Each guidance surface has one responsibility.

| Surface | Owner and purpose |
|---|---|
| `AGENTS.md` | Project identity, trusted routing and cross-cutting safeguards |
| `CLAUDE.md` and Copilot root pointer | Redirect to the root guidance |
| `.github/instructions/genesis` | Reviewed shared imports, unchanged |
| `.github/instructions/curb` | Curb-specific rules and replacements for conflicting defaults |
| `.github/instructions/imports.json` | Exact imported-path and content-hash allowlist |
| `docs/index.md` and `_docset.yml` | Documentation entrypoint and navigation |
| Design/workflow docs | Architecture, rationale and operational detail |
| `docs/adr` | Significant accepted decisions and their lifecycle |
| Project-local skills | On-demand procedures, not another standards corpus |
| Code, manifests and tests | Executable behavior and structural enforcement |

## Read the rules that match

Resolve instructions before editing. The resolver considers both namespaces and returns the applicable paths.

```powershell
$repo = (git rev-parse --show-toplevel)
pwsh -NoProfile -NonInteractive -File (Join-Path $repo 'build\guidance\Get-ApplicableInstructions.ps1') -Path 'src\Nullean.Curb.Core\CSharpFormatter.cs' -Json
```

Read every returned file. A Markdown link does not guarantee that a client loads mandatory rules. Shared requirements reach multiple populations through appropriate globs instead.

Curb-specific constraints must not contradict an imported rule. Omit an incompatible import and place the required contract in the Curb namespace. Do not edit imported wording or rely on file order to settle conflicts.

## Refreshing shared guidance

The shared folder is a curated snapshot, not an installation of every available default. The allowlist records public target paths and approved content hashes only.

A refresh needs a reviewed source snapshot, applicability/conflict assessment and exact file diff. Keep source-private provenance and detailed exclusion records outside the public repository. Verify that no unapproved file appears and that Curb-owned files remain untouched.

Do not run whole-tree synchronization. It can restore deliberately omitted rules. A future profile-based workflow is usable only if it preserves the approved selection exactly.

## Context and structure

The guidance contract declares root, individual-file and combined matching-context budgets. Measure shared and local files together, including populations introduced by a change.

The structural gate checks ownership, allowlists, hashes, redirects, matching scopes, links, navigation and ADR metadata. It does not freeze paragraphs as literal snapshots.

Review remains bounded to changed lines and their direct invariant impact. Pre-existing unrelated divergence does not become a blocker for another change.

See [ADR-0001](../adr/0001-agent-guidance-ownership.md) for the decision and [contributing](contributing.md) for the local gate.
