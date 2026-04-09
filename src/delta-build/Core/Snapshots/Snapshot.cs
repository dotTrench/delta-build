namespace DeltaBuild.Cli.Core.Snapshots;

public sealed record Snapshot
{
    public required string Commit { get; init; }
    public required IReadOnlyList<SnapshotProject> Projects { get; init; }
}

public sealed record SnapshotProject
{
    public required string Path { get; init; }
    public required int TopologicalOrder { get; init; }
    public required IReadOnlyDictionary<string, string> InputFiles { get; init; }
    public required IReadOnlyCollection<string> ProjectReferences { get; init; }
}
