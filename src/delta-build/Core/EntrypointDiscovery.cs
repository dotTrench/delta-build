namespace DeltaBuild.Cli.Core;

public abstract record EntrypointDiscoveryResult
{
    public record Success(IReadOnlyList<string> Paths) : EntrypointDiscoveryResult;

    public record Ambiguous(IReadOnlyList<string> Candidates) : EntrypointDiscoveryResult;

    public record NotFound() : EntrypointDiscoveryResult;
}


public static class EntrypointDiscovery
{
    private static readonly string[] SolutionPatterns = ["*.sln", "*.slnx", "*.slnf"];

    public static EntrypointDiscoveryResult Discover(string directory)
    {
        var solutions = SolutionPatterns
            .SelectMany(p => Directory.GetFiles(directory, p, SearchOption.TopDirectoryOnly))
            .ToList();

        return solutions.Count switch
        {
            0 => DiscoverProjects(directory),
            1 => new EntrypointDiscoveryResult.Success(solutions),
            _ => new EntrypointDiscoveryResult.Ambiguous(solutions)
        };
    }

    private static EntrypointDiscoveryResult DiscoverProjects(string directory)
    {
        var projects = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories).ToList();

        return projects.Count switch
        {
            0 => new EntrypointDiscoveryResult.NotFound(),
            _ => new EntrypointDiscoveryResult.Success(projects)
        };
    }
}
