using Microsoft.Extensions.FileSystemGlobbing;

namespace DeltaBuild.Cli.Core.Diff;

public sealed class GlobMatcher
{
    private readonly Matcher _matcher;

    public GlobMatcher(IEnumerable<string> patterns)
    {
        _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in patterns)
            _matcher.AddInclude(PathHelpers.Normalize(pattern));
    }

    public bool IsIgnored(string relativePath) =>
        _matcher.Match(relativePath).HasMatches;
}
