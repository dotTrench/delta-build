using System.ComponentModel;

using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Graph;

using Spectre.Console;
using Spectre.Console.Cli;

namespace DeltaBuild.Cli.Commands;

public sealed class SnapshotCommand : AsyncCommand<SnapshotCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--commit <commit>")]
        [DefaultValue("HEAD")]
        [Description("The commit, branch, or tag to snapshot. Accepts any git reference. Defaults to HEAD.")]
        public required string Commit { get; init; } = "HEAD";

        [CommandOption("-e|--entrypoint <project-or-solution>")]
        [Description(
            "One or more solution or project files (or glob patterns) to use as the build graph entrypoint. If not specified, delta-build will attempt to discover entrypoints automatically.")]
        public required string[] Entrypoints { get; init; } = [];


        [CommandOption("-o|--output <path>")]
        [Description("Write output to a file instead of stdout.")]
        public FileInfo? Output { get; init; }


        [CommandOption("--overwrite")]
        [Description("Overwrite the --output file if it already exists.")]
        [DefaultValue(false)]
        public bool Overwrite { get; init; }


        public override ValidationResult Validate()
        {
            if (!Overwrite && Output is { Exists: true })
            {
                return ValidationResult.Error("--output already exists, use --overwrite to overwrite.");
            }

            return ValidationResult.Success();
        }
    }

    private readonly IAnsiConsole _console;
    private readonly IEnvironment _environment;
    private readonly IStandardOutput _stdout;

    public SnapshotCommand(IAnsiConsole console, IEnvironment environment, IStandardOutput stdout)
    {
        _console = console;
        _environment = environment;
        _stdout = stdout;
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
            _console.MarkupLine("[red]Unable to find git repository[/]");
            return 1;
        }

        var sha = await repo.LookupCommitShaAsync(settings.Commit, cancellationToken);

        if (sha is null)
        {
            _console.MarkupLineInterpolated($"[red]Unable to find commit '{settings.Commit}'[/]");
            return 1;
        }

        var isShallow = await repo.IsShallowRepositoryAsync(cancellationToken);
        if (isShallow && !await repo.CommitExistsLocallyAsync(sha, cancellationToken))
        {
            _console.MarkupLine(
                "[yellow]Warning: This repository is a shallow clone. " +
                "Snapshotting a commit other than HEAD may fail if the target commit has not been fetched. " +
                "Ensure the repository has sufficient depth. " +
                "See https://github.com/dotTrench/delta-build/blob/main/docs/shallow-clones.md[/]"
            );
        }


        var relativeWorkingDirectory = Path.GetRelativePath(repo.WorkingDirectory, _environment.WorkingDirectory);


        await using var worktree = await repo.CreateWorktreeAsync(sha, cancellationToken);

        var worktreeCwd = Path.GetFullPath(relativeWorkingDirectory, worktree.WorkingDirectory);

        IReadOnlyCollection<string> entrypoints;
        if (settings.Entrypoints is { Length: > 0 })
        {
            var result = EntrypointDiscovery.Resolve(worktreeCwd, settings.Entrypoints);
            if (result is EntrypointDiscoveryResult.Ambiguous(var candidates))
            {
                _console.MarkupLine("[red]Ambiguous entrypoints found:[/]");
                foreach (var candidate in candidates)
                {
                    _console.MarkupLine($"[red]-{candidate}[/]");
                }

                return 1;
            }

            if (result is not EntrypointDiscoveryResult.Success(var resolved))
            {
                _console.MarkupLine("[red]No entrypoints found matching the specified patterns[/]");
                return 1;
            }

            entrypoints = resolved;
        }
        else
        {
            var result = EntrypointDiscovery.Discover(worktreeCwd);

            switch (result)
            {
                case EntrypointDiscoveryResult.Success success:
                    entrypoints = success.Paths;
                    break;

                case EntrypointDiscoveryResult.Ambiguous a:
                    _console.MarkupLine("[red]Ambiguous entrypoints found:[/]");
                    foreach (var candidate in a.Candidates)
                    {
                        _console.MarkupLine($"[red]-{candidate}[/]");
                    }

                    return 1;

                case EntrypointDiscoveryResult.NotFound:
                    _console.MarkupLine("[red]Entrypoint could not be found[/]");
                    return 1;


                default:
                    throw new InvalidOperationException("Unknown discoveryResult");
            }
        }

        using var projectCollection = new ProjectCollection();
        var graph = new ProjectGraph(entrypoints, projectCollection);

        var snapshot = await SnapshotGenerator.GenerateSnapshot(graph, worktree, cancellationToken);

        await using var output = settings.Output?.Create() ?? _stdout.OpenStream();
        await SnapshotSerializer.SerializeAsync(output, snapshot, cancellationToken);

        return 0;
    }
}
