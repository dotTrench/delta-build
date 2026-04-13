using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Cli.Core.Snapshots.Cache;
using DeltaBuild.Tests.Utils;

namespace DeltaBuild.Tests.Core.Snapshots;

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
            new StreamStandardInput(stream),
            null
        );

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
                new NullStandardInput(),
                null
            );

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
            new NullStandardInput(),
            null
        );

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
        await repo.WriteFileAsync("First.sln", "", cancellationToken);
        await repo.WriteFileAsync("Second.sln", "", cancellationToken);
        repo.Commit("initial");

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput(),
            null
        );

        var result = await resolver.ResolveAsync(repo.GetCurrentCommit(), [], cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.AmbiguousEntrypoints>();
    }

    [Test]
    public async Task NoEntrypointsFound_WhenNoSolutionsDiscovered(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        await repo.WriteFileAsync("README.md", "hello", cancellationToken);
        repo.Commit("initial");

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput(),
            null
        );

        var result = await resolver.ResolveAsync(repo.GetCurrentCommit(), [], cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.NoEntrypointsFound>();
    }

    [Test]
    public async Task Success_ReturnsCachedSnapshot_WhenCacheHit(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.CreateCsproj("src/Core/Core.csproj").Commit("initial");

        var sha = repo.GetCurrentCommit();
        var cached = new Snapshot { Commit = "from-cache", Projects = [] };
        var cache = new InMemorySnapshotCache();
        await cache.SetAsync(sha, cached, cancellationToken);

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput(),
            cache
        );

        var result = await resolver.ResolveAsync(sha, [], cancellationToken);

        var success = await Assert.That(result).IsTypeOf<SnapshotResolverResult.Success>();
        await Assert.That(success!.Snapshot.Commit).IsEqualTo("from-cache");
    }

    [Test]
    public async Task PopulatesCache_AfterResolvingCommit(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.CreateCsproj("src/Core/Core.csproj").Commit("initial");

        var sha = repo.GetCurrentCommit();
        var cache = new InMemorySnapshotCache();

        var gitRepo = await GitRepository.DiscoverAsync(repo.WorkingDirectory, cancellationToken)
                      ?? throw new InvalidOperationException();
        var resolver = new SnapshotResolver(
            gitRepo,
            new TestEnvironment(repo.WorkingDirectory),
            new NullStandardInput(),
            cache
        );

        var result = await resolver.ResolveAsync(sha, [], cancellationToken);

        await Assert.That(result).IsTypeOf<SnapshotResolverResult.Success>();
        var cachedEntry = await cache.GetAsync(sha, cancellationToken);
        await Assert.That(cachedEntry).IsNotNull();
    }

    private sealed class InMemorySnapshotCache : ISnapshotCache
    {
        private readonly Dictionary<string, Snapshot> _store = new();

        public Task SetAsync(string sha, Snapshot data, CancellationToken cancellationToken = default)
        {
            _store[sha] = data;
            return Task.CompletedTask;
        }

        public Task<Snapshot?> GetAsync(string sha, CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(sha, out var snapshot);
            return Task.FromResult(snapshot);
        }
    }
}
