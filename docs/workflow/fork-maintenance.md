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

## Artifact-free validation and publication

Ordinary fork validation builds and tests with read-only permissions. It does not upload,
download or retain GitHub Actions artifacts. A full dispatch builds a native installation
package in its own job instead of storing packages for another job to download.

Inherited site deployment and release tagging have no automatic trigger and are disabled in their job definitions. Historical installation examples, container references and site settings still describe upstream.

The fork package IDs are `ncosentino.curb-cli` and `ncosentino.curb`. The CLI command remains
`curb`; namespaces and original author/license notices are retained. Use a local tool path or
manifest to avoid confusing the fork executable with a globally installed upstream tool.
Reference only one Curb MSBuild distribution in a project; both packages define the same build targets.

For a fork prerelease, manually dispatch `Fork validation` with `publish_release=true`.
That input forces the complete corpus and platform matrix, even if `full` is false. Only after
all validation jobs succeed does the separate `Fork prerelease` workflow receive release-write
permissions. Ordinary pushes and pull requests cannot enter that path.

The release workflow rebuilds the exact validated commit and pins its package version with
`MinVerVersionOverride`. Its jobs upload directly to a draft release, not to Actions artifact
storage. The final job requires all eight package identities, matching versions and SHA-256
digests, then installs the CLI and exercises the MSBuild package before publishing the prerelease.
`SHA256SUMS` accompanies the packages.

An incomplete release stays draft. Existing assets are never overwritten: identical bytes may
be reused, while different bytes fail explicitly. A repeated whole-release dispatch for an
existing tag fails rather than replacing it. Inspect an abandoned draft before removing it
and retrying; do not delete a published release to reuse its version.

This boundary is recorded in [ADR-0002](../adr/0002-artifact-free-distribution.md).
Site deployment, container publication and inherited release automation remain disabled.

## Installing release packages

Download all eight `.nupkg` assets from the chosen
[fork release](https://github.com/ncosentino/curb/releases) into one folder.
Use its `SHA256SUMS` to verify the downloaded bytes. The root CLI package resolves the
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

Replace the version placeholder with the release package version:

```powershell
$feed = (Resolve-Path '.\curb-packages').Path
$tools = Join-Path $PWD '.curb-tools'
dotnet tool install ncosentino.curb-cli --version '<release-version>' --tool-path $tools --configfile (Join-Path $feed 'NuGet.Config')
& (Join-Path $tools 'curb') --version
```

For build integration, reference `ncosentino.curb` at that same version with `PrivateAssets="all"`
and restore against the same local feed. Debug builds format; Release builds check. Do not also
reference the upstream MSBuild package in that project.

## NuGet.org authorization

NuGet.org is the intended public feed for standard tool installation and package references.
It needs an explicit NuGet.org owner and trusted-publishing policy before publication can run.
GitHub repository ownership does not establish that authorization. Until it is configured,
use the release packages; their presence does not claim a NuGet.org publication.

## Pull-request readiness

Review the complete diff and actual validation evidence before making a PR ready. Fix blocking problems or keep it draft. Disclose relevant assumptions, intentionally deferred scope and unrun required checks.

Use exact head guards when merging an approved PR. After merge, update the primary checkout and remove only the task's completed worktree and branch.

The [review procedure](../../.github/skills/review-changes/SKILL.md) and [contributor workflow](contributing.md) define the local checks.
