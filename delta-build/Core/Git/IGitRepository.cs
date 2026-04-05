namespace DeltaBuild.Cli.Core.Git;

public interface IGitRepository : IDisposable
{
    string WorkingDirectory { get; }
    IWorktree CreateWorktree(string commit);
    string? LookupCommit(string reference);
}