using DeltaBuild.Cli.Core.Git;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Graph;

namespace DeltaBuild.Cli.Core;

public sealed class SnapshotResolver(IGitRepository repository, IEnvironment environment)
{
    public async Task<SnapshotResolverResult> ResolveAsync(
        string value,
        IReadOnlyList<FileInfo> entrypoints,
        CancellationToken cancellationToken = default
    )
    {
        if (File.Exists(value))
        {
            await using var file = File.OpenRead(value);
            var snapshot = await SnapshotSerializer.DeserializeAsync(file, cancellationToken);
            return new SnapshotResolverResult.Success(snapshot);
        }

        var sha = repository.LookupCommit(value);
        if (sha is null)
            return new SnapshotResolverResult.CommitNotFound(value);

        var relativeWorkingDirectory = Path.GetRelativePath(repository.WorkingDirectory, environment.WorkingDirectory);

        using var worktree = repository.CreateWorktree(sha);

        IReadOnlyCollection<string> resolvedEntrypoints;
        if (entrypoints is { Count: > 0 })
        {
            resolvedEntrypoints = entrypoints
                .Select(e => Path.GetRelativePath(repository.WorkingDirectory, Path.GetFullPath(e.FullName)))
                .Select(it => Path.GetFullPath(it, worktree.WorkingDirectory))
                .ToList();

            foreach (var entrypoint in resolvedEntrypoints)
            {
                if (!Path.Exists(entrypoint))
                    return new SnapshotResolverResult.EntrypointNotFound(entrypoint);
            }
        }
        else
        {
            var discovery = EntrypointDiscovery.Discover(
                Path.GetFullPath(relativeWorkingDirectory, worktree.WorkingDirectory));

            switch (discovery)
            {
                case EntrypointDiscoveryResult.Success(var ep):
                    resolvedEntrypoints = ep;
                    break;
                case EntrypointDiscoveryResult.Ambiguous(var candidates):
                    return new SnapshotResolverResult.AmbiguousEntrypoints(candidates);
                case EntrypointDiscoveryResult.NotFound:
                    return new SnapshotResolverResult.NoEntrypointsFound();
                default:
                    throw new ArgumentOutOfRangeException(nameof(discovery), discovery, null);
            }
        }

        using var projectCollection = new ProjectCollection();
        var graph = new ProjectGraph(resolvedEntrypoints, projectCollection);
        return new SnapshotResolverResult.Success(SnapshotGenerator.GenerateSnapshot(graph, worktree));
    }
}

public abstract record SnapshotResolverResult
{
    public record Success(Snapshot Snapshot) : SnapshotResolverResult;

    public record CommitNotFound(string Reference) : SnapshotResolverResult;

    public record EntrypointNotFound(string Path) : SnapshotResolverResult;

    public record AmbiguousEntrypoints(IReadOnlyList<string> Candidates) : SnapshotResolverResult;

    public record NoEntrypointsFound : SnapshotResolverResult;
}