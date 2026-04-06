namespace DeltaBuild.Cli.Core.Git;

public interface IGitRepository : IDisposable
{
    string WorkingDirectory { get; }
    IWorktree CreateWorktree(string commit);
    string? LookupCommitSha(string reference);

    Task<IWorktree> CreateWorktreeAsync(string commitSha, CancellationToken cancellationToken = default);
    Task<string?> LookupCommitShaAsync(string reference, CancellationToken cancellationToken = default);
}
