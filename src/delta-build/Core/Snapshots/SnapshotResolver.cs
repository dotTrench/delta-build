using System.Diagnostics;

using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots.Cache;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Graph;

namespace DeltaBuild.Cli.Core.Snapshots;

public sealed class SnapshotResolver(
    IGitRepository repository,
    IEnvironment environment,
    IStandardInput stdin,
    ISnapshotCache? cache
)
{
    public async Task<SnapshotResolverResult> ResolveAsync(
        string value,
        IReadOnlyList<string> entrypoints,
        CancellationToken cancellationToken = default
    )
    {
        if (value == "-")
        {
            await using var stream = stdin.OpenStream();
            return new SnapshotResolverResult.Success(
                await SnapshotSerializer.DeserializeAsync(stream, cancellationToken)
            );
        }

        if (File.Exists(value))
        {
            await using var file = File.OpenRead(value);
            return new SnapshotResolverResult.Success(
                await SnapshotSerializer.DeserializeAsync(file, cancellationToken)
            );
        }

        var sha = await repository.LookupCommitShaAsync(value, cancellationToken);
        if (sha is null)
            return new SnapshotResolverResult.CommitNotFound(value);

        if (cache is not null)
        {
            var cached = await cache.GetAsync(sha, cancellationToken);

            if (cached is not null)
            {
                return new SnapshotResolverResult.Success(cached);
            }
        }
        var relativeWorkingDirectory = Path.GetRelativePath(repository.WorkingDirectory, environment.WorkingDirectory);

        await using var worktree = await repository.CreateWorktreeAsync(sha, cancellationToken);

        var worktreeCwd = Path.GetFullPath(relativeWorkingDirectory, worktree.WorkingDirectory);

        IReadOnlyCollection<string> resolvedEntrypoints;
        if (entrypoints is { Count: > 0 })
        {
            var discovery = EntrypointDiscovery.Resolve(worktreeCwd, entrypoints);
            if (discovery is EntrypointDiscoveryResult.Ambiguous(var candidates))
            {
                return new SnapshotResolverResult.AmbiguousEntrypoints(candidates);
            }

            if (discovery is not EntrypointDiscoveryResult.Success(var resolved))
                return new SnapshotResolverResult.NoEntrypointsFound();
            resolvedEntrypoints = resolved;
        }
        else
        {
            var discovery = EntrypointDiscovery.Discover(worktreeCwd);

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
                    throw new UnreachableException();
            }
        }

        using var projectCollection = new ProjectCollection();
        var graph = new ProjectGraph(resolvedEntrypoints, projectCollection);

        var snapshot = await SnapshotGenerator.GenerateSnapshot(graph, worktree, cancellationToken);
        if (cache is not null)
        {
            await cache.SetAsync(sha, snapshot, cancellationToken);
        }
        return new SnapshotResolverResult.Success(snapshot);
    }
}
