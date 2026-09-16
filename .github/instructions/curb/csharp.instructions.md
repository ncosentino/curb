---
applyTo: "**/*.cs"
---

# Curb C# contracts

- Follow `.editorconfig` and the analyzers actually declared by the repository.
  Use `const` only for compile-time-constant values; assignment count alone is insufficient.
- Keep runtime code Native-AOT-compatible. Do not introduce reflection, dynamic code
  generation, Workspaces, `CSharpCompilation`, or semantic-model access, including in tests.
- Core depends only on the exact-pinned parser and has no IO or configuration source.
  Cleanup consumes build diagnostics rather than calculating semantic verdicts.
- Use `System.IO.Abstractions.IFileSystem` for production filesystem access.
  Preserve explicit file selection, linked compile items and `MockFileSystem` support.
- Validate paths at the actual IO boundary against the operation's authorized scope.
  Do not use bare string prefixes for confinement or interpolate paths into shell commands.
- Preserve `FormatResult`, `CleanupResult`, diagnostics and explicit refusal reasons.
  Narrow exception-to-result boundaries may abort an unsafe print; never turn failures
  into successful empty results or weaken verification to make a case pass.
- Keep the CLI's stdout/stderr and exit-code contract. Do not add a logging or DI
  framework merely to satisfy generic conventions.
- Keep temporal inputs explicit where cache/test APIs already accept them. Do not add
  clock plugins or fixture dependencies without a separately justified change.
