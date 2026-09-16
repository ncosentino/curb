---
name: review-changes
description: Review the current Curb diff for applicable guidance, formatter contracts, documentation ownership and delivery readiness.
---

# Review Curb changes

Review is read-only unless fixes are requested. Judge changed lines and their direct
invariant impact; report pre-existing unrelated divergence separately.

## Resolve the scope

Use explicit refs/paths when supplied. Otherwise include all unstaged, staged and
untracked changes. If the worktree is clean, compare the branch to its merge base with
`origin/main`.

Confirm the actual repository and branch. All GitHub requests for this review target
`ncosentino/curb`; a PR's base and head must belong to the intended fork.

Read the complete selected diff, including untracked files. State the scope before
giving a verdict.

## Resolve governing sources

Use the repository's instruction resolver for every changed population:

```powershell
$repo = (git rev-parse --show-toplevel)
pwsh -NoProfile -NonInteractive -File (Join-Path $repo 'build\guidance\Get-ApplicableInstructions.ps1') -Path '<changed-relative-path>' -Json
```

Read all returned shared and Curb instructions. Read `.github/guidance.json`, its docs
map, relevant design/workflow pages and accepted ADRs.

Check the import allowlist. An unexpected shared file or changed imported byte is a
failure, not a local override. Do not disclose private source provenance in public output.

## Resolve validation

```powershell
pwsh -NoProfile -NonInteractive -File (Join-Path $repo 'build\guidance\Get-ValidationInventory.ps1') -Json
```

Read the actual manifests, F# command/target definitions and workflow steps before
choosing a command. TUnit runs through the existing executable project.

Run the smallest relevant local gate. Complete suites, corpus runs, AOT matrices,
publication and credentialed checks belong to their separately approved owner.

For a PR, inspect evidence explicitly in the fork:

```text
gh pr checks <number> --repo ncosentino/curb
```

Disabled Actions and skipped checks are not passing evidence. Record what ran, failed,
or remains unverified. Never enable inherited workflows merely to obtain a green check.

## Review what the gates do not establish

Inspect correctness, explicit failure handling, parser-only/AOT compatibility, allocation
cost, safe writes and first-pass idempotency where the diff reaches them.

Distinguish token/content verification from semantic correctness and reference
compatibility from identical raw-input output. Keep byte-exact fixture and generated
reference ownership intact.

Check docs and instructions for lost rules, stale claims, contradictory layers, broken
routing and excess matched context. Do not invent findings for a clean diff.

Before public delivery, inspect tracked changes and the proposed commit/PR text for
credentials, private repository information, local machine paths and upstream mentions
or issue references that could generate unwanted activity.

## Reflect and report

Guidance changes need a demonstrated recurring problem or one significant mistake with
material risk. Prefer executable protection when possible. Do not expand guidance for
speculative or already-covered concerns.

Report:

- **Scope:** exact diff or paths.
- **Verdict:** approve, approve with nits, or request changes.
- **Findings:** introduced defects with file/line, evidence and a concrete correction.
- **Evidence:** observed checks and missing required evidence.
- **Guidance:** no change warranted, or the supported lesson and its proper owner.

Before a ready PR, resolve blockers or keep it draft. Commit, push, publication and
merge remain explicitly authorized operations; this review does not perform them.
