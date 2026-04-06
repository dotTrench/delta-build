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
            throw new GitProcessException($"Failed to create worktree for {commitSha}: {result.Stderr}");
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
