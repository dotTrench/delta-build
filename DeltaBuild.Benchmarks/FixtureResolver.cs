using System.Runtime.CompilerServices;

namespace DeltaBuild.Benchmarks;

public static class FixtureResolver
{
    public static string GetRepositoryRoot([CallerFilePath] string caller = "")
    {
        var file = new FileInfo(caller);

        DirectoryInfo? parent = file.Directory;

        while (true)
        {
            if (parent is null)
            {
                throw new NotImplementedException();
            }

            if (File.Exists(Path.Combine(parent.FullName, "DeltaBuild.slnx")))
                return parent.FullName;
            parent = parent.Parent;
        }
    }
}
