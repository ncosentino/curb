---
navigation_title: Fork maintenance
description: Keep development, support and publication scoped to this independently maintained fork.
---

# Maintaining this fork

This repository is an independently maintained fork of Curb. Preserve Nullean and contributor attribution, the original Git history, the MIT license and `NOTICE`.

## Repository destinations

All ordinary development activity belongs to `ncosentino/curb`. Check the repository and PR base explicitly before a GitHub write. Configure the local GitHub CLI default and push remote for this fork.

Use feature branches and worktrees based on freshly fetched fork main. Deliver through a PR to this fork's default branch, never a direct default-branch push.

Do not open upstream issues or PRs, comment on them, request reviews, mention maintainers or run automation that generates those interactions. Read-only investigation is separate and should remain within the requested task.

Upstream issue links in development conversations can create backlinks. When a reference is needed, use GitHub's documented `redirect.github.com` form to avoid that activity. A normal repository attribution link does not replace the fork notice.

## Shared instruction maintenance

Imported rules are an explicit allowlist under the shared namespace. Keep them unchanged. Place local rules in the Curb namespace and exclude incompatible imports instead of layering contradictions.

Review refreshes as exact file/hash changes. Do not run unfiltered synchronization or copy a private source repository's metadata, paths, issues or operational context into this public repository.

## Publication is separate

Inherited Actions remain disabled until their fork-specific configuration is reviewed and authorized. Some existing package metadata, registries, container references and site settings still describe upstream.

Do not publish with those settings merely because the code builds. A release needs distinct fork identity and destinations, appropriate credentials, validated artifacts and explicit authorization.

No test result can substitute for that authorization. A skipped or disabled workflow is not successful CI evidence.

## Pull-request readiness

Review the complete diff and actual validation evidence before making a PR ready. Fix blocking problems or keep it draft. Disclose relevant assumptions, intentionally deferred scope and unrun required checks.

Use exact head guards when merging an approved PR. After merge, update the primary checkout and remove only the task's completed worktree and branch.

The [review procedure](../../.github/skills/review-changes/SKILL.md) and [contributor workflow](contributing.md) define the local checks.
