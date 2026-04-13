using System.Diagnostics.CodeAnalysis;

namespace DeltaBuild.Cli.Core.Snapshots.Cache;

public static class SnapshotCacheFactory
{
    public static bool TryCreateCache(string path, [NotNullWhen(true)] out ISnapshotCache? cache)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == "file")
            {
                cache = new LocalSnapshotCache(new DirectoryInfo(uri.AbsolutePath));
                return true;
            }

            cache = null;
            return false;
        }

        cache = new LocalSnapshotCache(new DirectoryInfo(path));
        return true;
    }
}
