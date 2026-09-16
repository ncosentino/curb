---
name: commit
description: Stage relevant files and create a well-formed git commit. Use this when the user asks to commit changes, save work, or create a commit.
---

# Commit Skill

Read [`.claude/skills/writing-style.md`](../writing-style.md) before writing the commit message.

Creates a clean, well-formed commit following this project's conventions.

Operate only on the intended fork worktree. Verify the push remote is `ncosentino/curb`,
run the repository review procedure, and keep public text free of private/local context.
Commit only when authorized. Do not enable hooks, tooling or publishing as a side effect.

## Steps

### 1. Check for project hooks

Inspect the repository's actual hook configuration. Run configured hooks and fix their
failures; restore a missing declared tool only when the failure establishes that need.
Do not install a generic hook runner merely because another repository uses it.

Do not use `--no-verify`.

### 2. Understand what changed

```bash
git status
git diff
git diff --staged
git log --oneline -5
```

### 3. Stage files

Stage specific files by name — never `git add -A` or `git add .` blindly. Exclude:
- `.env` files or anything with secrets/credentials
- Large binaries not already tracked
- Unrelated changes to the task at hand

### 4. Write the commit message

- **First line**: Imperative mood, ≤72 chars, no trailing period. Front-load the outcome — a reader scanning `git log` sees this line only.
- **Body** (optional): One short paragraph explaining *why*, not what. Skip if the title is self-explanatory. Follow the sentence mechanics in `writing-style.md`.
- **Trailer**: Use the accurate assistant attribution required by the active session.
  Do not give Copilot another provider's identity. Copilot CLI contributions use:

```text
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
```

Prepare multiline text in a UTF-8 file outside the repository and pass its absolute
path. This works without Bash heredocs or opening an editor:

```text
git commit --file "<absolute-message-file>"
```

### 5. Handle hook failures

If a git hook fails:
1. Read the error output carefully
2. Fix the underlying issue (formatting, linting, type errors — whatever the hook checks)
3. Re-stage the affected files
4. Create a **new commit** — never `git commit --amend` for a failed commit, and never use `--no-verify`

### 6. Verify success

```bash
git status
```

Confirm a clean working tree.

### 7. Refresh the PR description if one exists

```bash
gh pr list --repo ncosentino/curb --head "<branch>" --json number,url,isDraft,baseRefName
```

- **No PR** → done. Say nothing.
- **PR exists** → compare the current body against the current diff versus the PR's base branch. A PR description always describes the current diff against the base branch. It is never a log of the commits on the branch and never records the direction the work took. If any section (`## What`, `## Verify`, or the lead paragraph) no longer describes that diff, the description is stale.
- **Stale description** → read and follow [pr](../pr/SKILL.md)'s update path (step 7). Do not hand-edit the body inline from the commit skill. State plainly what was refreshed.
- **Still accurate** → state that the description is still accurate. No edit needed.
