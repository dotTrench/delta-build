using System.Text.Json;

namespace DeltaBuild.Cli.Core.Snapshots.Cache;

public sealed class LocalSnapshotCache : ISnapshotCache
{
    private readonly DirectoryInfo _directory;

    public LocalSnapshotCache(DirectoryInfo directory)
    {
        _directory = directory;
    }

    public async Task SetAsync(string sha, Snapshot data, CancellationToken cancellationToken = default)
    {
        if (!_directory.Exists)
        {
            _directory.Create();
        }

        var path = Path.Combine(_directory.FullName, $"{sha}.json");

        await using var fs = File.Create(path);
        await SnapshotSerializer.SerializeAsync(fs, data, cancellationToken);
    }

    public async Task<Snapshot?> GetAsync(string sha, CancellationToken cancellationToken = default)
    {
        if (!_directory.Exists)
        {
            return null;
        }

        var path = Path.Combine(_directory.FullName, $"{sha}.json");

        if (!File.Exists(path))
        {
            return null;
        }


        try
        {
            await using var fs = File.OpenRead(path);
            return await SnapshotSerializer.DeserializeAsync(fs, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
