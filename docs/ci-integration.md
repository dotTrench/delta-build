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
        run: delta-build diff --base ${{ github.event.pull_request.base.sha }} --output diff.sln

      - name: Build affected projects
        run: dotnet build diff.sln
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
        uses: actions/cache@v4
        with:
          path: base-snapshot.json
          key: snapshot-${{ github.event.pull_request.base.sha }}

      - name: Generate base snapshot
        if: steps.cache.outputs.cache-hit != 'true'
        run: |
          git fetch --depth=1 origin ${{ github.event.pull_request.base.sha }}
          delta-build snapshot --commit ${{ github.event.pull_request.base.sha }} --output base-snapshot.json

      - name: Find affected projects
        run: delta-build diff --base base-snapshot.json --output diff.sln

      - name: Build affected projects
        run: dotnet build diff.sln
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

  - script: dotnet build diff.sln
    displayName: Build affected projects
```

### With snapshot caching

...