# delta-build

delta-build analyzes your .NET build graph and git history to determine exactly which projects need to be rebuilt,

Inspired by [dotnet-affected](https://github.com/leonardochaia/dotnet-affected).

## How It Works

DeltaBuild captures **snapshots** of your build graph at different points in time, then diffs them to identify affected
projects.

A snapshot contains:

- All projects in your solution and their source files
- Git blob hashes for every input file
- Project reference relationships

When you run a diff, DeltaBuild:

1. Resolves the base and head commits/snapshots
2. Computes which source files changed
3. Propagates changes through the dependency graph
4. Outputs the minimal set of projects that need building

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

| Flag                      | Description                                                             | Default       |
|---------------------------|-------------------------------------------------------------------------|---------------|
| `--base <ref>`            | Commit, branch, tag, or snapshot file to compare against                | Required      |
| `--head <ref>`            | Commit, branch, tag, or snapshot file to compare to                     | `HEAD`        |
| `-e, --entrypoint <path>` | Solution or project file(s) to analyze                                  | Auto-discover |
| `--include-affected`      | Include projects depending on changed code                              | `true`        |
| `--include-modified`      | Include projects with direct file changes                               | `true`        |
| `--include-added`         | Include new projects                                                    | `true`        |
| `--include-removed`       | Include deleted projects                                                | `false`       |
| `--include-unchanged`     | Include unchanged projects                                              | `false`       |
| `--pretty`                | Render a colored tree view to stderr                                    | `false`       |
| `--detailed`              | When combined with --pretty prints more detailed view of diff to stderr | `false`       |
| `--format <format>`       | Output format: `plain`, `json`, `sln`, `slnx`                           | `plain`       |
| `--output <path>`         | Write output to file                                                    | stdout        |

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

