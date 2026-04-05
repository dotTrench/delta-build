namespace DeltaBuild.Cli.Core.Snapshots;

public sealed record Snapshot
{
    public required string Commit { get; init; }
    public required IReadOnlyDictionary<string, SnapshotProject> Projects { get; init; }

    public required IReadOnlyDictionary<string, string> FileHashes { get; init; }
}

public sealed record SnapshotProject
{
    public required IReadOnlyCollection<string> InputFiles { get; init; }
    public required IReadOnlyCollection<string> ProjectReferences { get; init; }
}