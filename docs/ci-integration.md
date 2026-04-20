# CI Integration

## GitHub Actions

### Basic

```yaml
on:
  pull_request:
    branches: [ "main" ]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - name: Install delta-build
        run: dotnet tool install -g delta-build

      - name: Fetch base commit
        run: git fetch --depth=1 origin ${{ github.event.pull_request.base.sha }}

      - name: Find affected projects
        id: diff
        continue-on-error: true
        run: delta-build diff --base ${{ github.event.pull_request.base.sha }} --output diff.sln --include-dependencies

      - name: Build affected projects
        if: steps.diff.outcome == 'success'
        run: dotnet build diff.sln

      - name: Build all projects (fallback)
        if: steps.diff.outcome != 'success'
        run: dotnet build
```

### With snapshot caching

Caches build graph snapshots by commit SHA so snapshot generation is skipped on subsequent
runs against the same base. Useful for large repositories where snapshot generation is slow.

Pass `--cache` to `diff` — it reads from the cache when a snapshot exists for a commit and
writes to it after generating a new one, so no separate snapshot step is needed.

```yaml
on:
  pull_request:
    branches: [ "main" ]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - name: Install delta-build
        run: dotnet tool install -g delta-build

      - name: Restore snapshot cache
        uses: actions/cache@v5
        with:
          path: .delta-build-cache
          key: delta-build-snapshots-${{ github.event.pull_request.base.sha }}
          restore-keys: |
            delta-build-snapshots-

      - name: Fetch base commit
        run: git fetch --depth=1 origin ${{ github.event.pull_request.base.sha }}

      - name: Find affected projects
        id: diff
        continue-on-error: true
        run: delta-build diff --base ${{ github.event.pull_request.base.sha }} --cache .delta-build-cache --output diff.sln --include-dependencies

      - name: Build affected projects
        if: steps.diff.outcome == 'success'
        run: dotnet build diff.sln

      - name: Build all projects (fallback)
        if: steps.diff.outcome != 'success'
        run: dotnet build
```

## Azure DevOps

### Basic

```yaml
# PR pipeline
steps:
  - checkout: self

  - script: dotnet tool install -g delta-build
    displayName: Install delta-build

  - script: git fetch --depth=1 origin $(System.PullRequest.TargetBranchName)
    displayName: Fetch base commit

  - script: delta-build diff --base "$(System.PullRequest.TargetBranchName)" --output diff.sln --include-dependencies
    displayName: Find affected projects
    continueOnError: true
    name: diff

  - script: dotnet build diff.sln
    displayName: Build affected projects
    condition: eq(variables['diff.result'], 'succeeded')

  - script: dotnet build
    displayName: Build all projects (fallback)
    condition: eq(variables['diff.result'], 'failed')
```

### With snapshot caching

Caches build graph snapshots by commit SHA so snapshot generation is skipped on subsequent
runs against the same base. Useful for large repositories where snapshot generation is slow.

Pass `--cache` to `diff` — it reads from the cache when a snapshot exists for a commit and
writes to it after generating a new one, so no separate snapshot step is needed.

```yaml
# PR pipeline
steps:
  - checkout: self

  - script: dotnet tool install -g delta-build
    displayName: Install delta-build

  - script: |
      git fetch --depth=1 origin $(System.PullRequest.TargetBranchName)
      BASE_SHA=$(git rev-parse origin/$(System.PullRequest.TargetBranchName))
      echo "##vso[task.setvariable variable=BaseSha]$BASE_SHA"
    displayName: Resolve target branch SHA

  - task: Cache@2
    inputs:
      key: '"delta-build-snapshots" | "$(BaseSha)"'
      restoreKeys: '"delta-build-snapshots"'
      path: .delta-build-cache
    displayName: Restore snapshot cache

  - script: delta-build diff --base $(BaseSha) --cache .delta-build-cache --output diff.sln --include-dependencies
    displayName: Find affected projects
    continueOnError: true
    name: diff

  - script: dotnet build diff.sln
    displayName: Build affected projects
    condition: eq(variables['diff.result'], 'succeeded')

  - script: dotnet build
    displayName: Build all projects (fallback)
    condition: eq(variables['diff.result'], 'failed')
```