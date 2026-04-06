using LibGit2Sharp;

namespace DeltaBuild.Cli.Core.Git;

public sealed class LibGit2Worktree : IWorktree
{
    private readonly Repository _parent;
    private readonly Worktree _worktree;
    private bool _disposed;

    public LibGit2Worktree(Repository parent, Worktree worktree)
    {
        _parent = parent;
        _worktree = worktree;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _worktree.WorktreeRepository.Dispose();
        _parent.Worktrees.Prune(_worktree);
        _parent.Branches.Remove(_worktree.Name);
    }

    public string WorkingDirectory => _worktree.WorktreeRepository.Info.WorkingDirectory;
    public string Commit => _worktree.WorktreeRepository.Head.Tip.Sha;

    public string? GetFileSha(string relativePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entry = _worktree.WorktreeRepository.Head.Tip[relativePath];

        return entry?.Target.Sha;
    }

    public bool IsFileIgnored(string relativePath) =>
        _worktree.WorktreeRepository.Ignore.IsPathIgnored(relativePath);

    public Task<IReadOnlyDictionary<string, string>> CollectTreeObjectIdsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var root = _worktree.WorktreeRepository.Head.Tip.Tree;

        var output = new Dictionary<string, string>();

        RecurseTree(root, null, output);

        return Task.FromResult<IReadOnlyDictionary<string, string>>(output);

        void RecurseTree(Tree t, string? parent, Dictionary<string, string> ids)
        {
            foreach (var entry in t)
            {
                var path = parent is not null ? parent + '/' + entry.Path : entry.Path;
                switch (entry.TargetType)
                {
                    case TreeEntryTargetType.Blob:
                        ids.Add(path, entry.Target.Sha);
                        break;
                    case TreeEntryTargetType.Tree:
                        RecurseTree(entry.Target.Peel<Tree>(), path, ids);
                        break;
                    case TreeEntryTargetType.GitLink:
                        break;
                }
            }
        }
    }
}
