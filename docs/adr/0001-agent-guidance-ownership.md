---
navigation_title: Guidance ownership
description: Keep shared instructions reusable while preserving Curb's architecture and minimizing recurring context.
status: Accepted
date: 2026-09-15
supersedes: ""
superseded_by: ""
---

# ADR-0001: Agent guidance ownership

## Context

Curb has specialized parser, layout, verification and build contracts. Shared C# guidance is useful, but some defaults assume different dependency, test, options or delivery architectures.

Always-loaded root files are the wrong place for complete onboarding procedures and architectural rationale. Removing that detail without assigning another owner would lose necessary constraints.

## Decision

Keep a small root that routes to scoped instructions, documentation, skills and executable contracts. Claude and Copilot entrypoints redirect to it.

Store reviewed shared instructions unchanged under `.github/instructions/genesis`. Store Curb-owned rules under `.github/instructions/curb`. A target-path/hash allowlist defines the imported tree exactly.

Exclude conflicting imports rather than requiring clients to reconcile contradictory instructions. Keep source-specific private provenance outside the public repository. Unfiltered refresh is prohibited.

Keep the existing documentation map and site builder. Skills own on-demand procedures; they discover standards and validation rather than duplicating them.

Use the existing test project and build runner to check structure, ownership, links, hashes and context budgets. Root reduction happens only after retained guidance has an owner.

## Alternatives

| Choice | Benefit | Cost |
|---|---|---|
| Keep monolithic roots | Familiar and easy to locate | Repeated context and stale duplicate inventories |
| Use only custom instructions | Complete local control | Duplicates useful shared standards and loses their reuse |
| Import every default | Simple wholesale synchronization | Imports assumptions that conflict with Curb |
| Import defaults and override conflicts | Retains every shared file | Loads contradictions and depends on precedence behavior |
| Curated imports plus local rules | Preserves reuse without conflicting contracts | Refreshes require an explicit selection review |

## Consequences

The selected model preserves the formatter's identity and toolchain while making guidance ownership inspectable. It adds maintained docs, scoped rules and a small structural gate.

Shared imports are pinned snapshots. Their refresh is reviewed until an available synchronization mechanism can represent the exact allowlist without restoring excluded rules.

This decision does not alter formatter behavior, dependency versions, test frameworks, publishing identity or GitHub settings.

## Confirmation

The guidance gate rejects unexpected or modified imports, oversized roots, invalid redirects, malformed scopes, missing owners and broken documentation routing. Negative cases establish that those failures are detected.

Review measures the combined shared and Curb context and distinguishes existing code divergence from defects introduced by the change.
