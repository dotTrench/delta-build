namespace DeltaBuild.Cli.Core.Snapshots.Cache;

public interface ISnapshotCache
{
    Task SetAsync(string sha, Snapshot data, CancellationToken cancellationToken = default);
    Task<Snapshot?> GetAsync(string sha, CancellationToken cancellationToken = default);
}
