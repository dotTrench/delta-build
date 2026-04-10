using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Tests.Utils;

using LibGit2Sharp;

namespace DeltaBuild.Tests.Core.Git;

public class GitRepositoryTests
{
    [Test]
    public async Task DiscoverAsync_ReturnsRepository_WhenInGitRepository(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();

        var result = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task DiscoverAsync_ReturnsRepository_WhenInSubdirectory(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        var subdir = Path.Combine(repo.WorkingDirectory, "src", "Core");
        Directory.CreateDirectory(subdir);

        var result = await GitRepository.DiscoverAsync(subdir, cancellationToken);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.WorkingDirectory).IsEqualTo(repo.WorkingDirectory);
    }

    [Test]
    public async Task DiscoverAsync_ReturnsNull_WhenNotInGitRepository(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-not-a-repo");
        try
        {
            var result = await GitRepository.DiscoverAsync(directory.FullName, cancellationToken);

            await Assert.That(result).IsNull();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task LookupCommitShaAsync_ReturnsNull_WhenReferenceDoesNotExist(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md").Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        var result = await gitRepo.LookupCommitShaAsync("nonexistent", cancellationToken);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task LookupCommitShaAsync_ReturnsSha_WhenReferenceIsHEAD(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        await repo.WriteFileAsync("README.md", cancellationToken: cancellationToken);
        repo.Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        var result = await gitRepo.LookupCommitShaAsync("HEAD", cancellationToken);

        await Assert.That(result).IsEqualTo(repo.GetCurrentCommit());
    }

    [Test]
    public async Task LookupCommitShaAsync_ReturnsSha_WhenReferenceIsTag(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        await repo.WriteFileAsync("README.md", cancellationToken: cancellationToken);

        repo.Commit("initial");

        using (var gitRepo = new Repository(repo.WorkingDirectory))
            gitRepo.ApplyTag("v1.0.0");

        var gitRepository = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                            ?? throw new InvalidOperationException();

        var result = await gitRepository.LookupCommitShaAsync("v1.0.0", cancellationToken);

        await Assert.That(result).IsEqualTo(repo.GetCurrentCommit());
    }

    [Test]
    public async Task IsShallowRepositoryAsync_ReturnsFalse_ForNormalRepository(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md").Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        var result = await gitRepo.IsShallowRepositoryAsync(cancellationToken);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CommitExistsLocallyAsync_ReturnsTrue_WhenCommitExists(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md").Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        var result = await gitRepo.CommitExistsLocallyAsync(repo.GetCurrentCommit(), cancellationToken);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task CommitExistsLocallyAsync_ReturnsFalse_WhenCommitDoesNotExist(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md").Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        var result =
            await gitRepo.CommitExistsLocallyAsync("0000000000000000000000000000000000000000", cancellationToken);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task CreateWorktreeAsync_CreatesWorktreeAtCorrectCommit(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        await repo.WriteFileAsync("README.md", cancellationToken: cancellationToken);
        repo.Commit("initial");
        var sha = repo.GetCurrentCommit();
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        await using var worktree = await gitRepo.CreateWorktreeAsync(sha, cancellationToken);

        await Assert.That(Directory.Exists(worktree.WorkingDirectory)).IsTrue();
        using var worktreeRepo = new Repository(worktree.WorkingDirectory);
        await Assert.That(worktreeRepo.Head.Tip.Sha).IsEqualTo(sha);
    }

    [Test]
    public async Task CreateWorktreeAsync_CleansUpWorktree_WhenDisposed(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md").Commit("initial");
        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();

        string worktreeDirectory;
        await using (var worktree = await gitRepo.CreateWorktreeAsync(repo.GetCurrentCommit(), cancellationToken))
        {
            worktreeDirectory = worktree.WorkingDirectory;
            await Assert.That(Directory.Exists(worktreeDirectory)).IsTrue();
        }

        await Assert.That(Directory.Exists(worktreeDirectory)).IsFalse();
        using var gitRepo2 = new Repository(repo.WorkingDirectory);
        await Assert.That(gitRepo2.Worktrees.Count()).IsEqualTo(0);
    }
}
