using LibGit2Sharp;

namespace DeltaBuild.Cli.Core.Git;

public sealed class LibGit2Repository : IGitRepository
{
    private readonly Repository _repository;

    public LibGit2Repository(string workingDirectory)
    {
        _repository = new Repository(workingDirectory);
    }

    public string WorkingDirectory => _repository.Info.WorkingDirectory;

    public string? LookupCommitSha(string reference)
    {
        var obj = _repository.Lookup(reference);
        return obj?.Peel<Commit>()?.Sha;
    }

    public Task<IWorktree> CreateWorktreeAsync(string commitSha, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateWorktree(commitSha));
    }

    public Task<string?> LookupCommitShaAsync(string reference, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(LookupCommitSha(reference));
    }

    public IWorktree CreateWorktree(string commit)
    {
        var name = $"delta-build-worktree-{Path.GetRandomFileName()}";
        var path = Path.Combine(Path.GetTempPath(), name);
        var worktree = _repository.Worktrees.Add(commit, name, path, false);

        return new LibGit2Worktree(_repository, worktree);
    }

    public void Dispose() => _repository.Dispose();


    public static LibGit2Repository? Discover(string directory)
    {
        var path = Repository.Discover(directory);
        if (path is null) return null;

        return new LibGit2Repository(path);
    }
}
