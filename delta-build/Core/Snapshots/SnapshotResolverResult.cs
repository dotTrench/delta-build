namespace DeltaBuild.Cli.Core.Snapshots;

public abstract record SnapshotResolverResult
{
    public record Success(Snapshot Snapshot) : SnapshotResolverResult;

    public record CommitNotFound(string Reference) : SnapshotResolverResult;

    public record EntrypointNotFound(string Path) : SnapshotResolverResult;

    public record AmbiguousEntrypoints(IReadOnlyList<string> Candidates) : SnapshotResolverResult;

    public record NoEntrypointsFound : SnapshotResolverResult;
}
