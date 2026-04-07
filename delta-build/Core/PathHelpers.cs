namespace DeltaBuild.Cli.Core;

public static class PathHelpers
{
    public static string Normalize(string path) => path.Replace('\\', '/');
}
