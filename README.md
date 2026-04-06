# delta-build

delta-build analyzes your .NET build graph and git history to determine exactly which projects need to be rebuilt,

Inspired by [dotnet-affected](https://github.com/leonardochaia/dotnet-affected).

## How it works

delta-build generates a snapshot of your repository's build graph at a given commit. To ensure a clean, reproducible
view of the repository at that point in time, it creates a git worktree for the target commit
using [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) — this means the snapshot always reflects the committed
state, never any local modifications.

Within the worktree, delta-build uses
MSBuild's [project graph](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview) to resolve the
full set of projects and their dependencies.
It then uses [Microsoft.Build.Prediction](https://github.com/microsoft/MSBuildPrediction)
to predict the input files for each project — the source files, project files, and other assets that MSBuild would
consume during a build. Each input file is recorded alongside its git blob hash, giving a precise and fast fingerprint
of the
project's state without hashing files on disk.

When diffing two snapshots, delta-build compares the blob hashes of each project's input files to determine which
projects have changed directly, then walks the dependency graph to mark any downstream projects as affected.

## Usage

### `diff` - Find affected projects

Compare your current branch against `main`:

```bash
delta-build diff --base main
```

Compare any two commits:

```bash
delta-build diff --base v1.0 --head HEAD~5
```

Compare against a saved snapshot:

```bash
delta-build diff --base snapshot.json
```

## Output Formats

Plain text (default):

```bash
delta-build diff --base main
# src/Core/Core.csproj
# src/Api/Api.csproj
```

JSON with change states:

```bash
delta-build diff --base main --format json
```

Visual Studio solution:

```bash
delta-build diff --base main --format sln --output affected.sln
```

## Options

| Flag                      | Description                                                                         | Default       |
|---------------------------|-------------------------------------------------------------------------------------|---------------|
| `--base <ref>`            | Commit, branch, tag, or snapshot file to compare against (use - to read from stdin) | Required      |
| `--head <ref>`            | Commit, branch, tag, or snapshot file to compare to, (use - to read from stdin)     | `HEAD`        |
| `-e, --entrypoint <path>` | Solution or project file(s) to analyze                                              | Auto-discover |
| `--include-affected`      | Include projects depending on changed code                                          | `true`        |
| `--include-modified`      | Include projects with direct file changes                                           | `true`        |
| `--include-added`         | Include new projects                                                                | `true`        |
| `--include-removed`       | Include deleted projects                                                            | `false`       |
| `--include-unchanged`     | Include unchanged projects                                                          | `false`       |
| `--pretty`                | Render a colored tree view to stderr                                                | `false`       |
| `--detailed`              | When combined with --pretty prints more detailed view of diff to stderr             | `false`       |
| `--format <format>`       | Output format: `plain`, `json`, `sln`, `slnx`                                       | `plain`       |
| `--output <path>`         | Write output to file                                                                | stdout        |

### `snapshot` - Save a build graph snapshot

Save a snapshot of your current build graph to a file:

```bash
delta-build snapshot > snapshot.json
```

Or snapshot a specific commit:

```bash
delta-build snapshot --commit v1.0 > snapshot.json
```

#### Snapshot Options

| Flag                      | Description                                      | Default       |
|---------------------------|--------------------------------------------------|---------------|
| `--commit <ref>`          | Commit, branch, or tag to snapshot               | `HEAD`        |
| `-e, --entrypoint <path>` | Solution or project file(s) to analyze           | Auto-discover |
| `-o, --output <path>`     | Write output to file                             | stdout        |
| `--overwrite`             | Overwrite the --output file if it already exists | `false`       |

## Piping

delta-build is designed with Unix-style pipelines in mind, making it easy to chain commands or handle your snapshots.

### Examples

```bash
# Calculate differences between HEAD and main, build all affected projects
delta-build diff --base main | dotnet build

# Pretty print manifest file
delta-build snapshot | jq

# Compress snapshot of HEAD using gzip
delta-build snapshot | gzip > snapshot.json

# Use a gzipped snapshot from Azure Blob Storage as a base
az storage blob download --container-name snapshots --name main.json.gz \
  | gzip --decompress \
  | delta-build diff --base=- --head=HEAD \
  | dotnet build
```