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
        run: delta-build diff --base ${{ github.event.pull_request.base.sha }} --output diff.sln

      - name: Build affected projects
        if: steps.diff.outcome == 'success'
        run: dotnet build diff.sln

      - name: Build all projects (fallback)
        if: steps.diff.outcome != 'success'
        run: dotnet build
```

### With snapshot caching

Caches the base snapshot by commit SHA so snapshot generation is skipped on subsequent
runs against the same base. Useful for large repositories where snapshot generation is slow.

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

      - name: Restore base snapshot from cache
        id: cache
        uses: actions/cache@v5
        with:
          path: base-snapshot.json
          key: snapshot-${{ github.event.pull_request.base.sha }}

      - name: Generate base snapshot
        if: steps.cache.outputs.cache-hit != 'true'
        continue-on-error: true
        run: |
          git fetch --depth=1 origin ${{ github.event.pull_request.base.sha }}
          delta-build snapshot --commit ${{ github.event.pull_request.base.sha }} --output base-snapshot.json

      - name: Find affected projects
        id: diff
        continue-on-error: true
        run: delta-build diff --base base-snapshot.json --output diff.sln

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

  - script: delta-build diff --base "$(System.PullRequest.TargetBranchName)" --output diff.sln
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

Caches the base snapshot by target branch SHA so snapshot generation is skipped on subsequent
runs against the same base. Useful for large repositories where snapshot generation is slow.

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
      key: '"snapshot" | "$(BaseSha)"'
      path: base-snapshot.json
      cacheHitVar: SNAPSHOT_CACHE_HIT
    displayName: Restore base snapshot from cache

  - script: delta-build snapshot --commit $(BaseSha) --output base-snapshot.json
    condition: ne(variables.SNAPSHOT_CACHE_HIT, 'true')
    continueOnError: true
    displayName: Generate base snapshot

  - script: delta-build diff --base base-snapshot.json --output diff.sln
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