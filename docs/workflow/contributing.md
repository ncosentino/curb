---
navigation_title: Contributing
description: The project boundaries, build entrypoints and validation needed when changing Curb.
---

# Contributing to Curb

This fork preserves {{product}}'s parser-only formatter and diagnostic-driven cleanup architecture. Changes need evidence for the behavior they affect, without replacing the project's toolchain or applying unrelated formatting.

## Project boundaries

| Location | Responsibility |
|---|---|
| `src/Nullean.Curb.Core` | Parser integration, options, document IR, printing and verification; no IO or configuration source |
| `src/Nullean.Curb.Cleanup` | Syntax rewrites from build diagnostics; no compilation or IO |
| `src/Nullean.Curb.EditorConfig` | EditorConfig resolution and binding through `IFileSystem` |
| `src/Nullean.Curb.Cli` | File selection, IO, caching, diagnostics and command execution |
| `src/Nullean.Curb.MSBuild` | Build-only props/targets and the packaged CLI payload |
| `tests/Nullean.Curb.Tests` | TUnit tests, formatting expectations and filesystem doubles |
| `tests/Nullean.Curb.Benchmarks` | BenchmarkDotNet component measurements |
| `examples` | Native-AOT and build-integration smoke projects |
| `tools/Nullean.Curb.OptionDocs` | Generated option reference |
| `build/scripts` | F# command definitions and target implementations |
| `build/guidance` | Read-only instruction and validation discovery |

Runtime projects must remain Native-AOT-compatible. Core's exact parser dependency is intentional. Do not add Workspaces, construct a compilation or use a semantic model, even to make a test easier.

All production filesystem access goes through `System.IO.Abstractions.IFileSystem`. Tests may use real temporary files when the filesystem is the subject. Build tooling has its own IO responsibilities.

## Commands

Use the SDK selected by `global.json`. PowerShell 7 is required for the guidance discovery tools, not for the distributed formatter.

Run build targets from the repository root. Both shell entrypoints invoke the F# runner. Its command union and target registration are the source of truth.

```powershell
$repo = (git rev-parse --show-toplevel); Set-Location -LiteralPath $repo
dotnet run --project (Join-Path $repo 'build\scripts\scripts.fsproj') -- guidance
```

The guidance target runs only its TUnit test population. The ordinary test target runs the full suite and is not the default workstation check.

Discover validation surfaces before choosing another command:

```powershell
pwsh -NoProfile -NonInteractive -File (Join-Path $repo 'build\guidance\Get-ValidationInventory.ps1') -Json
```

TUnit runs as an executable through `dotnet run`. Do not infer a `dotnet test` runner selection from the SDK version. Use Microsoft.Testing.Platform's tree-node filter for a targeted case or class.

The runner's build target includes a clean dependency. Avoid that path for a small check when the direct project command or a single target suffices.

## Evidence by change

| Change | Required evidence |
|---|---|
| Guidance | Structural guidance tests, matching-context measurements and review |
| Formatting option | Every value through the real binder, reference expectations and relevant mode coverage |
| Line-break decision | First-pass fixed point, preservation and deterministic cases, nested/trivia cases, conformance and churn |
| Cleanup rule or verifier delta | Positive/refusal cases, both negative delta directions, safety corpus and build-clean-rebuild conformance |
| Printer performance | Native-AOT measurements, with allocation and timing metrics kept distinct |
| Build integration | Relevant smoke project with positive and negative assertions |
| Packaging/AOT | Appropriate platform smoke and package checks |

Use the [option onboarding procedure](../../.github/skills/add-formatting-option/SKILL.md) and [cleanup onboarding procedure](../../.github/skills/add-cleanup-rule/SKILL.md) for those changes.

**Current divergence:** inherited Actions are disabled in this fork. Workflow definitions describe intended checks, not completed evidence. Full suites, corpus runs and platform matrices require approved fork CI; do not substitute an expensive local run or report a skipped check as passing.

## Correctness

Formatting expectations are deliberate inputs, not snapshots accepted from the formatter's current output. A fixture without a companion expectation must format to itself. Preserve the byte-level Git/editor exceptions for fixtures and the generated line-ending example.

Content verification, token verification, idempotency and reference compatibility are different properties. See [safety](../design-principles/safety.md), [conformance](../design-principles/conformance.md) and [layout modes](../design-principles/reflow.md).

The catalogs and executable option/rule listings own coverage. Do not maintain a second milestone or supported-key count in agent guidance.

## Review and delivery

Use a feature worktree based on freshly fetched fork main. Keep patches focused and preserve unrelated changes. Run the [review procedure](../../.github/skills/review-changes/SKILL.md) before delivery.

[Fork maintenance](fork-maintenance.md) governs repository destinations and publication. [Agent guidance](agent-guidance.md) explains the instruction ownership model.
