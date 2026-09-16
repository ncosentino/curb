---
navigation_title: Artifact-free distribution
description: Separate temporary CI output from free public package distribution.
status: Accepted
date: 2026-09-16
supersedes: ""
superseded_by: ""
---

# ADR-0002: Artifact-free distribution

## Context

Retained Actions artifacts consume the account's storage allowance. Platform packages and
their combined download duplicate stored bytes. Short retention does not eliminate that cost.
The fork needs downloadable builds without using Actions artifact storage.

## Decision

Keep all ordinary validation output on temporary runner disk. Do not use Actions artifacts
for distribution or for transfers between jobs. Rebuild the local installation package when
another job cannot share its files.

Publish versioned package downloads to this fork's GitHub Releases. A manual release request
must pass the complete validation matrix first. Rebuilds use the validated commit and package
version, upload directly to a draft release, and publish only after package identity, digest
and installation checks pass. Never overwrite a published version or a differing draft asset.

Use NuGet.org for normal public .NET package consumption once its owner grants explicit
trusted-publishing authorization. Do not infer registry access from GitHub access or store
long-lived publication credentials in source.

Leave inherited site, container and release publishers disabled. Preserve the fork package
names, original attribution, license and notices.

## Alternatives

| Choice | Benefit | Cost |
|---|---|---|
| Retained Actions artifacts | Convenient transfers between jobs | Consumes the constrained storage allowance |
| Shorter artifact retention | Reduces accumulated storage | Still consumes storage and removes downloads |
| Runner-local validation plus Releases | No Actions artifact storage; durable package downloads | Rebuilds cost additional public-runner compute |
| NuGet.org | Standard tool installation and package references | Public immutable versions and owner-side authorization |
| Container-only distribution | Public container storage is currently free | Adds Docker requirements to native CLI consumption |

## Consequences

Validation and public distribution have separate permissions and failure boundaries.
A failed publication leaves a draft rather than a partial public package set.
Release automation is explicit, fork-scoped and independent of upstream publishing.

## Confirmation

Workflow checks reject artifact upload/download actions. Package tests exercise valid sets
and reject missing, foreign, misnamed or version-mismatched files. A complete release must
pass real CLI and MSBuild installation checks with no retained Actions artifacts.
