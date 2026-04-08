# Shallow Clones

Many CI systems perform shallow clones by default (`--depth=1`) to speed up checkout.
This can cause problems with delta-build, which needs access to the base commit in order
to generate a snapshot of it.

## The warning

If delta-build detects a shallow repository and the base reference is not the current
HEAD, it will print:

```
Warning: This repository is a shallow clone. Diffing against commit references may fail
if the target commit has not been fetched. Consider using a snapshot file instead, or
ensure the repository has sufficient depth.
```

## Solutions

### 1. Fetch sufficient history

Fetch enough history that the base commit is available locally. How much you need depends
on how far back your base branch diverged:

```yaml
# GitHub Actions — fetch full history
- uses: actions/checkout@v4
  with:
    fetch-depth: 0

# GitHub Actions — fetch a fixed depth (faster, but may not be enough)
- uses: actions/checkout@v4
  with:
    fetch-depth: 100
```

```yaml
# Azure DevOps — fetch full history
- checkout: self
  fetchDepth: 0
```

Fetching full history (`fetch-depth: 0`) is always safe but can be slow on large
repositories. A fixed depth works if your PRs are always within that many commits of
the base — but will silently fail if a long-lived branch exceeds it.

### 2. Fetch only the base commit

If you know the exact commit or branch you're diffing against, you can fetch just that
ref without deepening the rest of the clone:

```bash
# Fetch a specific commit SHA
git fetch --depth=1 origin <base-sha>

# Fetch a branch tip
git fetch --depth=1 origin main
```

This is the most efficient option for CI — it adds only what delta-build needs without
pulling down unnecessary history. See the [CI Integration](ci-integration.md) examples
for how this fits into a full pipeline.
