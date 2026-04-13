using DeltaBuild.Cli.Core.Snapshots.Cache;

namespace DeltaBuild.Tests.Core.Snapshots.Cache;

public class SnapshotCacheFactoryTests
{
    [Test]
    public async Task TryCreateCache_ReturnsTrue_ForPlainPath()
    {
        var result = SnapshotCacheFactory.TryCreateCache("/tmp/cache", out var cache);

        await Assert.That(result).IsTrue();
        await Assert.That(cache).IsTypeOf<LocalSnapshotCache>();
    }

    [Test]
    public async Task TryCreateCache_ReturnsTrue_ForFileUri()
    {
        var result = SnapshotCacheFactory.TryCreateCache("file:///tmp/cache", out var cache);

        await Assert.That(result).IsTrue();
        await Assert.That(cache).IsTypeOf<LocalSnapshotCache>();
    }

    [Test]
    public async Task TryCreateCache_ReturnsFalse_ForNonFileUri()
    {
        var result = SnapshotCacheFactory.TryCreateCache("https://example.com/cache", out var cache);

        await Assert.That(result).IsFalse();
        await Assert.That(cache).IsNull();
    }
}
