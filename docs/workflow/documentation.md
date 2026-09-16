---
navigation_title: Documentation development
description: Maintain the docs-builder site without changing generated examples or triggering publication.
---

# Documentation development

{{product}} uses docs-builder, with a hand-maintained landing page applied after generation. Local documentation work must preserve that distinction.

## Sources and navigation

`docs/index.md` is the Markdown entrypoint. `docs/_docset.yml` defines site navigation and product substitutions. Add maintained pages to that navigation and link them from the relevant index.

Use `{{product}}` in prose. Headings use literal Curb because anchors are computed before substitution. Preserve the existing page frontmatter and documentation voice.

## Generated reference

`OptionDescriptor.All` and the option-doc tool generate `docs/reference`. Change those owners and run the existing options target instead of editing output.

The line-ending page contains real CRLF example bytes. Its `.gitattributes` exception is intentional. Preserve it along with the byte-exact formatter fixtures.

## Build without opening a browser

The F# documentation target downloads docs-builder into the existing artifact tools cache when needed. It builds the site and applies the landing-page override in the same order as the inherited workflow.

```powershell
$repo = (git rev-parse --show-toplevel); Set-Location -LiteralPath $repo
dotnet run --project (Join-Path $repo 'build\scripts\scripts.fsproj') -- docs --noserve
```

Agents use the no-serve option. The default preview can start a server and open a browser, which is not appropriate for headless automation.

Do not use direct on-demand serving as a substitute for this build. It skips the landing override and does not preview the same output.

## Landing and hosting

`docs/curb-landing.html` is not generated. Its base URL, `Documentation.PathPrefix` and the workflow prefix must agree. The existing prefix check protects this relationship.

This fork has not enabled its inherited publication workflow. Do not claim a local page was deployed, change the upstream domain, or alter crawler policy as a documentation cleanup.

Real public project and tool URLs are appropriate for attribution and installation instructions. Use placeholders for sample deployments and never publish private infrastructure, credentials or machine-specific paths.

See [fork maintenance](fork-maintenance.md) before any hosting or release change.
