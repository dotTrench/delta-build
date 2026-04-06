namespace DeltaBuild.Cli.Core.Git;

public interface IGitRepository
{
    string WorkingDirectory { get; }

    Task<IWorktree> CreateWorktreeAsync(string commitSha, CancellationToken cancellationToken = default);
    Task<string?> LookupCommitShaAsync(string reference, CancellationToken cancellationToken = default);
    Task<bool> IsShallowRepositoryAsync(CancellationToken cancellationToken = default);
}
