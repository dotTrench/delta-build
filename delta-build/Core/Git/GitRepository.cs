namespace DeltaBuild.Cli.Core.Git;

public sealed class GitRepository : IGitRepository
{
    public GitRepository(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }

    public async Task<IWorktree> CreateWorktreeAsync(string commitSha, CancellationToken cancellationToken = default)
    {
        var name = $"delta-build-{commitSha}-{Guid.NewGuid():N}";
        var directory = Path.Combine(Path.GetTempPath(), name);
        var result = await GitProcessRunner.RunCmd(
            WorkingDirectory,
            ["worktree", "add", "--detach", directory, commitSha],
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            throw new NotImplementedException(result.Stderr);
        }

        return new GitWorktree(WorkingDirectory, directory, commitSha);
    }

    public async Task<string?> LookupCommitShaAsync(string reference, CancellationToken cancellationToken = default)
    {
        var result = await GitProcessRunner.RunCmd(
            WorkingDirectory,
            ["rev-parse", reference],
            cancellationToken
        );

        if (result.ExitCode != 0)
        {
            return null;
        }

        return result.Stdout.TrimEnd('\r', '\n');
    }


    public static async Task<GitRepository?> DiscoverAsync(
        string directory,
        CancellationToken cancellationToken = default
    )
    {
        var result = await GitProcessRunner.RunCmd(directory, ["rev-parse", "--show-toplevel"], cancellationToken);

        if (result.ExitCode != 0)
        {
            return null;
        }

        var path = result.Stdout.TrimEnd('\r', '\n');


        return new GitRepository(path);
    }
}

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
            throw new NotImplementedException();
        }

        var files = new Dictionary<string, string>();
        using var reader = new StringReader(result.Stdout);
        string? line;
        
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split('\t');
            if (parts.Length != 3)
            {
                throw new NotImplementedException();
            }
            

            if (parts[0] != "blob") continue;

            var sha = parts[1];
            var path = parts[2];

            files[path] = sha;
        }

        return files;
    }
}
