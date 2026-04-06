namespace DeltaBuild.Cli.Core.Git;

public sealed class GitWorktree : IWorktree
{
    private bool _disposed;
    private readonly string _parentRoot;

    public GitWorktree(string parentRoot, string directory, string commitSha)
    {
        _parentRoot = parentRoot;
        WorkingDirectory = directory;
        Commit = commitSha;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await GitProcessRunner.RunCmd(
            _parentRoot,
            ["worktree", "remove", "--force", WorkingDirectory]
        );
    }

    public string WorkingDirectory { get; }
    public string Commit { get; }

    public async Task<IReadOnlyDictionary<string, string>> GetTrackedFileShasAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = await GitProcessRunner.RunCmd(WorkingDirectory,
            ["ls-tree", "-r", Commit, "--format", "%(objecttype)%x09%(objectname)%x09%(path)"], cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new GitProcessException($"Failed to get tracked files for {Commit}: {result.Stderr}");
        }

        var files = new Dictionary<string, string>();
        using var reader = new StringReader(result.Stdout);
        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split('\t');
            if (parts.Length != 3)
            {
                throw new GitProcessException($"Invalid format for output of tracked files for {Commit}: {line}");
            }


            if (parts[0] != "blob") continue;

            var sha = parts[1];
            var path = parts[2];

            files[path] = sha;
        }

        return files;
    }
}
