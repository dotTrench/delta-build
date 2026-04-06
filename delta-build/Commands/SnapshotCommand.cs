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
            "One or more solution or project files to use as the build graph entrypoint. If not specified, delta-build will attempt to discover entrypoints automatically.")]
        public required FileInfo[] Entrypoints { get; init; } = [];


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
        using var repo = LibGit2Repository.Discover(_environment.WorkingDirectory);
        if (repo is null)
        {
            _console.WriteLine("[red]Unable to find git repository[/]");
            return 1;
        }

        var sha = repo.LookupCommitSha(settings.Commit);

        if (sha is null)
        {
            _console.WriteLine($"[red]Unable to find commit '{settings.Commit}'[/]");
            return 1;
        }


        var relativeWorkingDirectory = Path.GetRelativePath(repo.WorkingDirectory, _environment.WorkingDirectory);


        using var worktree = repo.CreateWorktree(sha);

        IReadOnlyCollection<string> entrypoints;
        if (settings.Entrypoints is { Length: > 0 })
        {
            entrypoints = settings.Entrypoints
                .Select(e => Path.GetRelativePath(repo.WorkingDirectory, e.FullName))
                .Select(it => Path.GetFullPath(it, worktree.WorkingDirectory))
                .ToList();

            foreach (var entrypoint in entrypoints)
            {
                if (Path.Exists(entrypoint)) continue;

                _console.MarkupLine($"[red]Entrypoint {entrypoint} does not exist[/]");
                return 1;
            }
        }
        else
        {
            var result =
                EntrypointDiscovery.Discover(Path.GetFullPath(relativeWorkingDirectory, worktree.WorkingDirectory));

            switch (result)
            {
                case EntrypointDiscoveryResult.Success success:
                    entrypoints = success.Paths;
                    break;

                case EntrypointDiscoveryResult.Ambiguous a:
                    _console.MarkupLine("[red]Ambigious entrypoints found:[/]");
                    foreach (var candidate in a.Candidates)
                    {
                        _console.MarkupLine($"[red]{candidate}[/]");
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
