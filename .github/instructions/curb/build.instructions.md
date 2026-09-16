---
applyTo: "build/**,**/*.csproj,**/*.fsproj,**/*.props,**/*.targets,*.slnx,global.json,nuget.config,.config/dotnet-tools.json,.github/workflows/**,action.yml"
scope: "repository build, validation, packaging and workflow contracts"
---

# Build and delivery

- Keep the F# Argu/Bullseye runner and derive targets from its command union and
  registration. Run from the repository root; do not invent commands or runner pins.
- Preserve the SDK selection in `global.json`, central package versions and the
  distinction between runtime projects, executable tests, tools and smoke projects.
- Keep Core, Cleanup, EditorConfig, CLI and build-only MSBuild boundaries. Do not
  impose feature/bootstrap/adapter projects or generated DI fixtures.
- Treat the exact Roslyn parser pin as a formatting behavior choice, not an automatic
  dependency update. Preserve AOT settings and narrow, justified suppressions.
- TUnit runs with `dotnet run` through the actual test project. Select a narrow
  test population; do not treat zero tests or a skipped check as success.
- Use headless processes and structured arguments. Never open browsers or shell
  windows from agent work; documentation generation uses the no-serve option.
- Restore/install only for changed manifests or demonstrated missing dependencies.
  Do not run a solution clean/build just to validate a guidance paragraph.
- Complete suites, corpus gates and platform publishes need approved fork CI.
  Existing workflow definitions are not evidence that disabled automation ran.
- Keep all GitHub writes and PR bases on `ncosentino/curb`. Never push to a remote
  default branch directly or enable inherited publication without authorization.
- Before future CI changes, verify runner requirements and public-repository cost
  rules. Bound artifact paths, missing-file behavior, retention and expected size.
- Do not invent repository-local artifact actions or policy files that do not exist.
  Keep package identity, registry, container and site changes out of unrelated work.

Commands and ownership: [contributing](../../../docs/workflow/contributing.md).
