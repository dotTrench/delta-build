using System.Text.Json;

namespace DeltaBuild.Cli.Core.Snapshots;

public static class SnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task SerializeAsync(Stream stream, Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        return JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken);
    }

    public static Snapshot Deserialize(byte[] bytes)
    {
        return JsonSerializer.Deserialize<Snapshot>(bytes, SerializerOptions) ??
               throw new ArgumentException("Snapshot is null", nameof(bytes));
    }

    public static async Task<Snapshot> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        return await JsonSerializer.DeserializeAsync<Snapshot>(stream, SerializerOptions, cancellationToken) ??
               throw new ArgumentException("Snapshot is null", nameof(stream));
    }
}