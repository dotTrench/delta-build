using Microsoft.Extensions.FileSystemGlobbing;

namespace DeltaBuild.Cli.Core;

public abstract record EntrypointDiscoveryResult
{
    public record Success(IReadOnlyList<string> Paths) : EntrypointDiscoveryResult;

    public record Ambiguous(IReadOnlyList<string> Candidates) : EntrypointDiscoveryResult;

    public record NotFound : EntrypointDiscoveryResult;
}

public static class EntrypointDiscovery
{
    private static readonly string[] SolutionPatterns = ["*.sln", "*.slnx", "*.slnf"];
    private static readonly string[] ProjectPatterns = ["**/*.csproj"];

    public static EntrypointDiscoveryResult Discover(string directory)
    {
        var solutionResult = Resolve(directory, SolutionPatterns);

        return solutionResult switch
        {
            EntrypointDiscoveryResult.NotFound => Resolve(directory, ProjectPatterns),
            _ => solutionResult
        };
    }

    private static readonly HashSet<string> SolutionExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".sln", ".slnx", ".slnf" };

    public static EntrypointDiscoveryResult Resolve(string directory, IEnumerable<string> patterns)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
            matcher.AddInclude(PathHelpers.Normalize(pattern));
        var paths = matcher.GetResultsInFullPath(directory).ToList();

        if (paths.Count == 0)
            return new EntrypointDiscoveryResult.NotFound();

        var solutionFiles = paths
            .Where(p => SolutionExtensions.Contains(Path.GetExtension(p)))
            .ToList();

        if (solutionFiles.Count > 1)
            return new EntrypointDiscoveryResult.Ambiguous(solutionFiles);

        return new EntrypointDiscoveryResult.Success(paths);
    }
}
