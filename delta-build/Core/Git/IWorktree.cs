namespace DeltaBuild.Cli.Core.Git;

public interface IWorktree : IDisposable
{
    string WorkingDirectory { get; }
    string Commit { get; }

    string? GetFileSha(string relativePath);
    bool IsFileIgnored(string relativePath);
}