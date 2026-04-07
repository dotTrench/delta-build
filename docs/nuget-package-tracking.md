# NuGet Package Tracking

By default, delta-build tracks changes to `Directory.Packages.props` as an input file
for all projects that import it. This means any change to the file — even bumping a
package version that only one project uses — will mark every project as affected.
## Fine-grained tracking with NuGet lock files

NuGet lock files (`packages.lock.json`) give you project-scoped package change detection
for free: each project generates its own lock file, which only changes when that project's
resolved package graph actually changes.

### Setup

Enable lock files for your projects (either in `Directory.Build.props` or per-project):

```xml
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

Run `dotnet restore` to generate the initial `packages.lock.json` files, then commit them.

### Usage

Pass `--ignore Directory.Packages.props` to suppress the coarse-grained signal, and let
the per-project lock files carry the change signal instead:

```bash
delta-build diff --base main --ignore Directory.Packages.props
```

When a package version changes in `Directory.Packages.props`, only the projects whose
resolved dependency graph actually changed will have a modified `packages.lock.json` —
so only those projects are marked as affected.

### Why this works

- `Directory.Packages.props` defines *requested* versions — it's repo-wide and coarse
- `packages.lock.json` records *resolved* versions per project — it's precise and scoped
- A version bump in a package only used by `src/Api/Api.csproj` will only change
  `src/Api/packages.lock.json`, leaving all other lock files untouched

This delegates the complexity of NuGet version resolution (multi-targeting, conditional
references, `VersionOverride`, transitive dependencies) to NuGet itself rather than
delta-build having to replicate that logic.
