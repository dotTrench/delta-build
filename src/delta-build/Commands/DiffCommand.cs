using System.Collections.Frozen;
using System.ComponentModel;
using System.Diagnostics;

using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Diff;
using DeltaBuild.Cli.Core.Diff.Formatting;
using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Cli.Core.Snapshots.Cache;

using Spectre.Console;
using Spectre.Console.Cli;

namespace DeltaBuild.Cli.Commands;

public sealed class DiffCommand : AsyncCommand<DiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--head <commit-or-snapshot-file>")]
        [DefaultValue("HEAD")]
        [Description("The commit, branch, tag, or snapshot file to use as the head. '-' for STDIN")]
        public required string Head { get; init; } = "HEAD";

        [CommandOption("--base <commit-or-snapshot-file>", isRequired: true)]
        [Description("The commit, branch, tag, or snapshot file to diff against. '-' for STDIN")]
        public required string Base { get; init; }

        [CommandOption("-e|--entrypoint <project-or-solution>")]
        [Description(
            "One or more solution or project files (or glob patterns) to use as the build graph entrypoint for both head and base. " +
            "If not specified, delta-build will attempt to discover entrypoints automatically."
        )]
        public required string[] Entrypoints { get; init; } = [];

        [CommandOption("--head-entrypoint <project-or-solution>")]
        [Description("Overrides --entrypoint for the head snapshot only.")]
        public string[]? HeadEntrypoints { get; init; }

        [CommandOption("--base-entrypoint <project-or-solution>")]
        [Description("Overrides --entrypoint for the base snapshot only.")]
        public string[]? BaseEntrypoints { get; init; }

        [CommandOption("--include-removed")]
        [DefaultValue(false)]
        [Description("Include projects that exist in the base but not in the head.")]
        public bool IncludeRemoved { get; init; }

        [CommandOption("--include-unchanged")]
        [DefaultValue(false)]
        [Description("Include projects with no changes and no affected dependencies.")]
        public bool IncludeUnchanged { get; init; }

        [CommandOption("--include-added")]
        [DefaultValue(true)]
        [Description("Include projects that exist in the head but not in the base.")]
        public bool IncludeAdded { get; init; } = true;

        [CommandOption("--include-modified")]
        [DefaultValue(true)]
        [Description("Include projects whose own input files have changed.")]
        public bool IncludeModified { get; init; } = true;

        [CommandOption("--include-affected")]
        [DefaultValue(true)]
        [Description("Include projects with an upstream dependency that was modified or added.")]
        public bool IncludeAffected { get; init; } = true;

        [CommandOption("--include-dependencies")]
        [DefaultValue(false)]
        [Description(
            "For each included project, also include all of its transitive project dependencies, even if they are unchanged. " +
            "Useful when building with solution files, where dotnet does not propagate the build configuration to dependencies."
        )]
        public bool IncludeDependencies { get; init; }

        [CommandOption("--ignore <pattern>")]
        [Description(
            "Glob pattern for files to exclude from the diff. " +
            "Files matching any pattern are treated as unchanged. " +
            "Supports * and ? wildcards and ** for recursive matching. " +
            "Can be specified multiple times.")]
        public string[] Ignore { get; init; } = [];

        [CommandOption("--ignore-project <pattern>")]
        [Description(
            "Glob pattern for projects to exclude from the diff. " +
            "Projects matching any pattern are treated as unchanged and will not cause dependents to be marked as affected. " +
            "Supports * and ? wildcards and ** for recursive matching. " +
            "Can be specified multiple times.")]
        public string[] IgnoreProject { get; init; } = [];

        [CommandOption("--explain")]
        [Description("Write a colored tree view of the affected projects to stderr alongside the normal output.")]
        public bool Explain { get; init; }

        [CommandOption("--detailed")]
        [Description(
            "Enriches the --explain tree view with file-level changes and upstream causes for each affected project.")]
        public bool Detailed { get; init; }

        [CommandOption("--format <format>")]
        [Description(
            "Output format for the list of affected projects. " +
            "Inferred from the --output file extension if not specified. " +
            "plain: newline-separated project paths. " +
            "json: JSON array with project paths and states. " +
            "sln: Visual Studio solution file containing the affected projects (requires --output). " +
            "slnx: XML-based solution file containing the affected projects (requires --output)."
        )]
        public OutputFormat? Format { get; init; }

        [CommandOption("--output <path>")]
        [Description(
            "Write output to a file instead of stdout. " +
            "The output format will be inferred from the file extension (.json, .sln, .slnx) unless --format is specified. " +
            "For sln and slnx formats, project paths will be made relative to the output file location."
        )]
        public FileInfo? Output { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite the --output file if it already exists.")]
        public bool Overwrite { get; init; }


        [CommandOption("--exit-code-on-empty")]
        [Description("Exit code when no projects are outputted")]
        [DefaultValue(0)]
        public int ExitCodeOnEmpty { get; init; } = 0;


        [CommandOption("--cache")]
        [Description("Directory to cache build graph snapshots in. Cached snapshots are reused across runs to avoid redundant worktree builds for commits already seen.")]
        public string? Cache { get; init; }

        public enum OutputFormat
        {
            Plain,
            Json,
            Sln,
            Slnx
        }


        public override ValidationResult Validate()
        {
            if (!Overwrite && Output is { Exists: true })
            {
                return ValidationResult.Error("--output already exists, use --overwrite to overwrite.");
            }

            if (Format is OutputFormat.Slnx or OutputFormat.Sln && Output is null)
            {
                return ValidationResult.Error("--output is required when using sln or slnx format.");
            }

            if (Base == "-" && Head == "-")
            {
                return ValidationResult.Error(
                    "STDIN(-) redirect can't be used for both '--head' and '--base' at the same time"
                );
            }

            return ValidationResult.Success();
        }
    }


    private readonly IEnvironment _environment;
    private readonly IAnsiConsole _console;
    private readonly IStandardOutput _stdout;
    private readonly IStandardInput _stdin;

    public DiffCommand(IEnvironment environment, IAnsiConsole console, IStandardOutput stdout, IStandardInput stdin)
    {
        _environment = environment;
        _console = console;
        _stdout = stdout;
        _stdin = stdin;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    )
    {
        var repo = await GitRepository.DiscoverAsync(_environment.WorkingDirectory, cancellationToken);
        if (repo is null)
        {
            return 1;
        }

        if (await repo.IsShallowRepositoryAsync(cancellationToken))
        {
            var isUnsafe = await IsShallowUnsafeAsync(repo, settings.Base, cancellationToken) ||
                           await IsShallowUnsafeAsync(repo, settings.Head, cancellationToken);

            if (isUnsafe)
            {
                _console.MarkupLine(
                    "[yellow]Warning: This repository is a shallow clone. " +
                    "Diffing against commit references may fail if the target commit has not been fetched. " +
                    "Consider using a snapshot file instead, or ensure the repository has sufficient depth. " +
                    "See https://github.com/dotTrench/delta-build/blob/main/docs/shallow-clones.md[/]"
                );
            }
        }

        ISnapshotCache? cache;
        if (settings.Cache is not null)
        {
            if (!SnapshotCacheFactory.TryCreateCache(settings.Cache, out cache))
            {
                _console.MarkupLineInterpolated($"[red]Invalid cache '{settings.Cache}' specified[/]");
                return 1;
            }
        }
        else
        {
            cache = null;
        }
        var resolver = new SnapshotResolver(repo, _environment, _stdin, cache);


        var headSnapshot = await ResolveSnapshotAsync(
            resolver,
            settings.Head,
            settings.HeadEntrypoints ?? settings.Entrypoints,
            cancellationToken
        );

        if (headSnapshot is null)
            return 1;

        var baseSnapshot = await ResolveSnapshotAsync(
            resolver,
            settings.Base,
            settings.BaseEntrypoints ?? settings.Entrypoints,
            cancellationToken
        );

        if (baseSnapshot is null)
            return 1;

        var formatter = GetFormatter(settings, repo.WorkingDirectory);


        var ignore = settings.Ignore.Length > 0 ? new GlobMatcher(settings.Ignore) : null;
        var ignoreProject = settings.IgnoreProject.Length > 0 ? new GlobMatcher(settings.IgnoreProject) : null;
        var diff = DiffCalculator.Calculate(baseSnapshot, headSnapshot, ignore, ignoreProject);

        var outputProjects = diff.Projects
            .Where(it => ShouldInclude(it, settings))
            .ToList();

        if (settings.IncludeDependencies && outputProjects.Count > 0)
        {
            var headProjectsByPath = headSnapshot.Projects.ToFrozenDictionary(p => p.Path);
            var includedPaths = outputProjects.Select(p => p.Path).ToHashSet();
            var toExpand = new Queue<string>(includedPaths);
            while (toExpand.Count > 0)
            {
                var path = toExpand.Dequeue();
                if (!headProjectsByPath.TryGetValue(path, out var project))
                {
                    continue;
                }
                foreach (var dep in project.ProjectReferences)
                {
                    if (includedPaths.Add(dep))
                        toExpand.Enqueue(dep);
                }
            }

            if (includedPaths.Count > outputProjects.Count)
            {
                var alreadyIncluded = outputProjects.Select(p => p.Path).ToHashSet();
                outputProjects = diff.Projects
                    .Where(p => includedPaths.Contains(p.Path))
                    .Select(p => alreadyIncluded.Contains(p.Path) ? p : p with { State = ProjectState.Dependency })
                    .ToList();
            }
        }

        await using (var output = settings.Output?.Create() ?? _stdout.OpenStream())
        {
            await formatter.FormatAsync(outputProjects, output, cancellationToken);
        }

        if (settings.Explain)
        {
            DiffRenderer.Render(_console, !settings.Detailed ? outputProjects : diff.Projects, settings.Detailed);
        }

        if (outputProjects.Count == 0)
        {
            return settings.ExitCodeOnEmpty;
        }

        return 0;
    }

    private static IDiffFormatter GetFormatter(Settings settings, string repoWorkingDirectory)
    {
        Settings.OutputFormat outputFormat;
        if (settings.Format.HasValue)
        {
            outputFormat = settings.Format.Value;
        }
        else if (settings.Output is not null)
        {
            outputFormat = DetectOutputFormat(settings.Output);
        }
        else
        {
            outputFormat = Settings.OutputFormat.Plain;
        }

        return outputFormat switch
        {
            Settings.OutputFormat.Plain => new PlainFormatter(),
            Settings.OutputFormat.Json => new JsonFormatter(),
            Settings.OutputFormat.Sln => new SolutionFormatter(
                settings.Output ?? throw new UnreachableException("No output file specified while using sln format"),
                repoWorkingDirectory,
                false
            ),
            Settings.OutputFormat.Slnx => new SolutionFormatter(
                settings.Output ??
                throw new InvalidOperationException("No output file specified while using slnx format"),
                repoWorkingDirectory,
                true
            ),
            _ => throw new UnreachableException()
        };
    }

    private static Settings.OutputFormat DetectOutputFormat(FileInfo file)
    {
        return Path.GetExtension(file.Name) switch
        {
            ".json" => Settings.OutputFormat.Json,
            ".sln" => Settings.OutputFormat.Sln,
            ".slnx" => Settings.OutputFormat.Slnx,
            _ => Settings.OutputFormat.Plain
        };
    }

    private static bool ShouldInclude(ProjectDiffResult project, Settings settings)
    {
        return project.State switch
        {
            ProjectState.Affected when settings.IncludeAffected => true,
            ProjectState.Added when settings.IncludeAdded => true,
            ProjectState.Modified when settings.IncludeModified => true,
            ProjectState.Removed when settings.IncludeRemoved => true,
            ProjectState.Unchanged when settings.IncludeUnchanged => true,
            _ => false
        };
    }


    private static async Task<bool> IsShallowUnsafeAsync(
        GitRepository repo,
        string value,
        CancellationToken cancellationToken
    )
    {
        if (value == "-" || File.Exists(value))
            return false;

        var sha = await repo.LookupCommitShaAsync(value, cancellationToken);
        if (sha is null)
            return true;

        return !await repo.CommitExistsLocallyAsync(sha, cancellationToken);
    }

    private async Task<Snapshot?> ResolveSnapshotAsync(
        SnapshotResolver resolver,
        string value,
        IReadOnlyList<string> entrypoints,
        CancellationToken cancellationToken
    )
    {
        var result = await resolver.ResolveAsync(
            value,
            entrypoints,
            cancellationToken
        );


        switch (result)
        {
            case SnapshotResolverResult.Success(var snapshot):
                return snapshot;
            case SnapshotResolverResult.CommitNotFound(var reference):
                _console.MarkupLine($"[red]Could not find commit by reference '{reference}[/]");
                return null;
            case SnapshotResolverResult.AmbiguousEntrypoints(var candidates):
                _console.MarkupLine("[red]Ambiguous entrypoints[/]");
                foreach (var c in candidates)
                {
                    _console.MarkupLine($"[red]- {c}[/]");
                }

                return null;
            case SnapshotResolverResult.NoEntrypointsFound:
                _console.MarkupLine("[red]No entrypoints found[/]");
                return null;
            default:
                _console.MarkupLine($"[red]Unknown result{result}[/]");
                return null;
        }
    }
}
