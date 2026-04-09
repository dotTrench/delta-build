using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Tests.Utils;

namespace DeltaBuild.Tests;

public class SnapshotResolverTests
{
    private static Snapshot SimpleSnapshot() => new() { Commit = "abc123", Projects = [] };

    private static async Task<MemoryStream> SerializeSnapshotAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken
    )
    {
        var stream = new MemoryStream();
        await SnapshotSerializer.SerializeAsync(stream, snapshot, cancellationToken);
        stream.Position = 0;
        return stream;
    }

    private sealed class StreamStandardInput(Stream stream) : IStandardInput
    {
        public Stream OpenStream() => stream;
    }

    [Test]
    public async Task Success_WhenValueIsDash_ReadsFromStdin(CancellationToken cancellationToken)
    {
        using var stream = await SerializeSnapshotAsync(SimpleSnapshot(), cancellationToken);
        var resolver = new SnapshotResolver(
            new GitRepository(Directory.GetCurrentDirectory()),
            new TestEnvironment(Directory.GetCurrentDirectory()),
            new StreamStandardInput(stream));

        var result = await resolver.ResolveAsync("-", [], cancellationToken);

        var success = await Assert.That(result).IsTypeOf<SnapshotResolverResult.Success>();
        await Assert.That(success!.Snapshot.Commit).IsEqualTo("abc123");
    }

    [Test]
    public async Task Success_WhenValueIsFilePath_ReadsFromFile(CancellationToken cancellationToken)
    {
        var file = Path.GetTempFileName();
        try
        {
            await using (var stream = File.OpenWrite(file))
                await SnapshotSerializer.SerializeAsync(stream, SimpleSnapshot(), cancellationToken);

            var resolver = new SnapshotResolver(
                new GitRepository(Directory.GetCurrentDirectory()),
                new TestEnvironment(Directory.GetCurrentDirectory()),
                new NullStandardInput());

            var result = await resolver.ResolveAsync(file, [], cancellationToken);

            var success = await Assert.That(result).IsTypeOf<SnapshotResolverResult.Success>();
            await Assert.That(success!.Snapshot.Commit).IsEqualTo("abc123");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public async Task EntrypointNotFound_WhenExplicitEntrypointDoesNotExistInWorktree(
        CancellationToken cancellationToken
    )
    {
        using var repo = TestRepository.Create();
        repo.CreateCsproj("src/Core/Core.csproj").Commit("initial");

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput());

        var result = await resolver.ResolveAsync(
            repo.GetCurrentCommit(),
            ["src/Nonexistent/Nonexistent.csproj"],
            cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.NoEntrypointsFound>();
    }

    [Test]
    public async Task AmbiguousEntrypoints_WhenMultipleSolutionsDiscovered(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("First.sln", "").WriteFile("Second.sln", "").Commit("initial");

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput());

        var result = await resolver.ResolveAsync(repo.GetCurrentCommit(), [], cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.AmbiguousEntrypoints>();
    }

    [Test]
    public async Task NoEntrypointsFound_WhenNoSolutionsDiscovered(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md", "hello").Commit("initial");

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput());

        var result = await resolver.ResolveAsync(repo.GetCurrentCommit(), [], cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.NoEntrypointsFound>();
    }
}
