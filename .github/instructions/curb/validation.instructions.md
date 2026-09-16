---
applyTo: "tests/**,examples/**"
scope: "formatter tests, smoke projects and performance evidence"
---

# Validation

- Keep the declared TUnit/Microsoft.Testing.Platform executable runner and
  AwesomeAssertions. Use the existing formatting harness for formatting cases.
- Tests run through `dotnet run`, not an assumed runner selection in `global.json`.
  Read the F# runner and project manifests before choosing the smallest command.
- Use real formatter, cleaner and binder implementations. `MockFileSystem` and
  narrow failing infrastructure doubles are appropriate boundaries.
- Do not install generated DI fixtures, Moq, a new result-assertion library or clock
  packages merely to match a generic test recipe.
- Keep tests independent and deterministic. Reuse established helpers instead of
  duplicating setup; use unique state when sharing an external resource.
- For time-sensitive cache cases, pass explicit fixed timestamps through the existing
  API. Do not sleep or depend on wall-clock advancement.
- Await asynchronous subjects and use observable completion with bounded timeouts.
  Do not use arbitrary delays, blocking task waits or conditional success assertions.
- Assert exact outputs, counts, status and refusal reasons, not merely that a result
  exists. Check the complete result before consuming its text or values.
- Preserve fixture bytes. `.test` files and the generated line-ending example have
  deliberate Git/editor exceptions; do not normalize them in unrelated work.
- A golden fixture without a companion expectation must format to itself.
  Formatting cases must preserve content/tokens and reach a fixed point.
- Use `Formats`, `Unchanged` and `WithAndWithout` as appropriate. Expected text comes
  from the reference behavior or a justified layout decision, not a Curb snapshot
  accepted merely because Curb produced it.
- Cover each supported option value and both layout modes when relevant.
  Include comments, directives, disabled branches and nesting where the change reaches them.
- Cleanup cases include fixes and refusals. New verifier deltas require negative
  coverage on both sides, and both corpus checks before a rule is ready.
- Keep smoke projects outside the ordinary solution where their setup requires it.
  Preserve positive and negative assertions that prove the integration really ran.
- JIT benchmarks explain costs; the published native-AOT executable owns production
  performance evidence. Keep CPU, elapsed time and allocations distinct.
- Complete suites, corpus runs and platform matrices belong to approved fork CI.
  A disabled or skipped check is missing evidence, never a pass.
