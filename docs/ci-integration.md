# CI Integration

## GitHub Actions

```yaml
on:
  pull_request:
    branches: [ "main" ]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # required — see shallow clones doc

      - name: Install delta-build
        run: dotnet tool install -g delta-build

      - name: Find affected projects
        run: delta-build diff --base ${{ github.event.pull_request.base.sha }} --output diff.sln

      - name: Build affected projects
        run: dotnet build diff.sln
```

## Azure DevOps

```yaml
# PR pipeline
steps:
  - checkout: self
    fetchDepth: 0 # required - see shallow clones doc

  - script: dotnet tool install -g delta-build
    displayName: Install delta-build

  - script: delta-build diff --base "$(System.PullRequest.TargetBranchName)" --output diff.sln
    displayName: Find affected projects

  - script: dotnet build diff.sln
    displayName: Build affected projects
```
