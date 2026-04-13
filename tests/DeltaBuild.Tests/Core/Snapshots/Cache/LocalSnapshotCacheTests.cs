using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Cli.Core.Snapshots.Cache;

using System.Text.Json;

namespace DeltaBuild.Tests.Core.Snapshots.Cache;

public class LocalSnapshotCacheTests
{
    private static Snapshot SimpleSnapshot(string commit = "abc123") => new()
    {
        Commit = commit,
        Projects = []
    };

    private static LocalSnapshotCache CreateCache(string directory)
        => new(new DirectoryInfo(directory));

    [Test]
    public async Task GetAsync_ReturnsNull_WhenDirectoryDoesNotExist(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var cache = CreateCache(directory);

        var result = await cache.GetAsync("abc123", cancellationToken);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetAsync_ReturnsNull_WhenEntryDoesNotExist(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var cache = CreateCache(directory.FullName);

            var result = await cache.GetAsync("abc123", cancellationToken);

            await Assert.That(result).IsNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task GetAsync_ReturnsNull_WhenFileIsCorrupt(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "abc123.json"),
                "not valid json",
                cancellationToken);

            var cache = CreateCache(directory.FullName);

            var result = await cache.GetAsync("abc123", cancellationToken);

            await Assert.That(result).IsNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_CreatesDirectory_WhenItDoesNotExist(CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var cache = CreateCache(directory);

            await cache.SetAsync("abc123", SimpleSnapshot(), cancellationToken);

            await Assert.That(Directory.Exists(directory)).IsTrue();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_ThenGetAsync_ReturnsSnapshot(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var cache = CreateCache(directory.FullName);
            var snapshot = SimpleSnapshot("deadbeef");

            await cache.SetAsync("deadbeef", snapshot, cancellationToken);
            var result = await cache.GetAsync("deadbeef", cancellationToken);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Commit).IsEqualTo("deadbeef");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_OverwritesExistingEntry(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var cache = CreateCache(directory.FullName);

            await cache.SetAsync("abc123", SimpleSnapshot("first"), cancellationToken);
            await cache.SetAsync("abc123", SimpleSnapshot("second"), cancellationToken);
            var result = await cache.GetAsync("abc123", cancellationToken);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.Commit).IsEqualTo("second");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task GetAsync_DoesNotReturnEntry_ForDifferentSha(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var cache = CreateCache(directory.FullName);

            await cache.SetAsync("abc123", SimpleSnapshot(), cancellationToken);
            var result = await cache.GetAsync("def456", cancellationToken);

            await Assert.That(result).IsNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
