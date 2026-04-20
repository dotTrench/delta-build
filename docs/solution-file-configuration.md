# Solution File Configuration

## The problem

When delta-build outputs a solution file containing only the affected projects, `dotnet
build` does not propagate the build configuration to project dependencies that are absent
from the solution.

For example, given `App.csproj` which has a `ProjectReference` to `Lib.csproj`, and only
`App.csproj` has changed:

```bash
delta-build diff --base main --output affected.slnx
dotnet build affected.slnx --configuration Release
```

`affected.slnx` contains only `App.csproj`. When MSBuild builds it, `Lib.csproj` is
resolved as an out-of-solution project reference and falls back to its default
configuration (Debug) rather than inheriting Release from the solution.

This is known to affect `dotnet build`. `dotnet publish` handles configuration propagation
correctly and does not require `--include-dependencies` for this reason. The behaviour of
other commands (`dotnet pack`, `dotnet test`, etc.) has not been verified — check whether
configuration propagates correctly for your use case before deciding whether to use the
flag.

Direct project builds are unaffected — `dotnet build App.csproj --configuration Release`
propagates the configuration correctly through `ProjectReference` items regardless of
whether dependencies are in a solution.

## The fix

Pass `--include-dependencies` to include the transitive dependencies of every included
project in the output, even if they are unchanged:

```bash
delta-build diff --base main --output affected.slnx --include-dependencies
dotnet build affected.slnx --configuration Release
```

`affected.slnx` now contains both `App.csproj` and `Lib.csproj`. MSBuild maps both to
the Release configuration, so `Lib.csproj` is built correctly.

Dependencies pulled in this way are reported with state `Dependency` in JSON output,
distinguishing them from projects that changed:

```bash
delta-build diff --base main --format json --include-dependencies
```

```json
[
  {
    "path": "src/Lib/Lib.csproj",
    "state": "Dependency"
  },
  {
    "path": "src/App/App.csproj",
    "state": "Modified"
  }
]
```
