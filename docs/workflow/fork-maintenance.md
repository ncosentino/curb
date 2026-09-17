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

An optional `release_version` pins the prerelease version for every validation and packaging
job through `MinVerVersionOverride`. Use a new version above the published package versions;
branch-height versions alone need not increase across merged histories. Leaving it empty
retains MinVer selection. Published versions are never replaced.

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
GitHub repository ownership does not establish permission to publish there. Configure the
following once before requesting publication:

1. Set the repository variable `NUGET_USER` to the NuGet.org profile name, not an email address.
2. Configure the GitHub environment `nuget-org` to allow deployments from the `main` branch only.
3. In that NuGet.org account, create a trusted-publishing policy for owner `ncosentino`,
   repository `curb`, workflow file `publish-nuget.yml`, and environment `nuget-org`.
   Grant new-package and new-version publishing with these package scopes:

```text
ncosentino.curb
ncosentino.curb-cli
ncosentino.curb-cli.*
```

Manually run `Promote fork release to NuGet.org` with a published release tag. The default
`publish=false` inspects its source, package identities and digests without requesting a
NuGet credential. Actual publication requires `publish=true` from fork main and the configured
owner policy; missing authorization fails explicitly.

Use `publish=false` with `verify_registry=true` to repeat registry availability and installation
checks without credentials or uploads. This is the recovery path when publication succeeds
but indexing or installation verification fails; do not rerun immutable package uploads.

Promotion consumes the release's existing package bytes rather than rebuilding them. The
dependency packages publish before the root CLI package. NuGet repository signing can change
archive bytes, so post-publication checks validate registry package identities and exercise
the installed CLI's exact version and source commit instead of comparing signed archive hashes.
Archive visibility does not guarantee that the NuGet client can discover a new version yet.
Installation uses uncached requests and retries only the SDK's exact version-not-found result,
with a bounded wait. Other installation failures remain immediate errors.

A failed or partial push is not success. Published versions are immutable; the workflow does
not hide duplicate-version conflicts. Inspect registry state before retrying or releasing a
new version. Until authorized publication completes, use the release downloads.

## Pull-request readiness

Review the complete diff and actual validation evidence before making a PR ready. Fix blocking problems or keep it draft. Disclose relevant assumptions, intentionally deferred scope and unrun required checks.

Use exact head guards when merging an approved PR. After merge, update the primary checkout and remove only the task's completed worktree and branch.

The [review procedure](../../.github/skills/review-changes/SKILL.md) and [contributor workflow](contributing.md) define the local checks.
