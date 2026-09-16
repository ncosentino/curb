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

The fork validation workflow builds and tests without publication credentials or write permissions. Its complete corpus and platform matrix is available through an explicit full dispatch.

Inherited site deployment and release tagging have no automatic trigger and are disabled in their job definitions. Historical installation examples, container references and site settings still describe upstream.

Do not publish with those settings merely because the code builds. A release needs distinct fork identity and destinations, appropriate credentials, validated artifacts and explicit authorization.

The fork package IDs are `ncosentino.curb-cli` and `ncosentino.curb`. The CLI command remains
`curb`; namespaces and original author/license notices are retained. Use a local tool path or
manifest to avoid confusing the fork executable with a globally installed upstream tool.
Reference only one Curb MSBuild distribution in a project; both packages define the same build targets.

Full validation assembles native and portable packages, installs the CLI from that local feed,
and exercises format/check builds through the MSBuild package before retaining the artifacts for seven days. These artifacts are
not a registry release. The upload covers only package files, not the workspace or test data.

No test result can substitute for that authorization. A skipped or disabled workflow is not successful CI evidence.

## Installing validation artifacts

Download the `fork-packages` artifact from a successful full validation run in this fork's
Actions tab. Extract all its packages into one folder; the root CLI package resolves the
matching native package from that same feed.

Save the following as `NuGet.Config` inside that folder. An explicit local-only configuration
avoids an inherited source mapping or an accidental public-registry fallback.

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="fork" value="." />
  </packageSources>
</configuration>
```

Replace the version placeholder with the artifact package version:

```powershell
$feed = (Resolve-Path '.\fork-packages').Path
$tools = Join-Path $PWD '.curb-tools'
dotnet tool install ncosentino.curb-cli --version '<artifact-version>' --tool-path $tools --configfile (Join-Path $feed 'NuGet.Config')
& (Join-Path $tools 'curb') --version
```

For build integration, reference `ncosentino.curb` at that same version with `PrivateAssets="all"`
and restore against the same local feed. Debug builds format; Release builds check. Do not also
reference the upstream MSBuild package in that project.

## Pull-request readiness

Review the complete diff and actual validation evidence before making a PR ready. Fix blocking problems or keep it draft. Disclose relevant assumptions, intentionally deferred scope and unrun required checks.

Use exact head guards when merging an approved PR. After merge, update the primary checkout and remove only the task's completed worktree and branch.

The [review procedure](../../.github/skills/review-changes/SKILL.md) and [contributor workflow](contributing.md) define the local checks.
