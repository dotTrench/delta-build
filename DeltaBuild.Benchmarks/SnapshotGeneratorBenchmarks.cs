using BenchmarkDotNet.Attributes;

using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.TestUtils;

using Microsoft.Build.Graph;
using Microsoft.Build.Locator;

namespace DeltaBuild.Benchmarks;

[MemoryDiagnoser]
public class SnapshotGeneratorBenchmarks
{
    static SnapshotGeneratorBenchmarks()
    {
        MSBuildLocator.RegisterDefaults();
    }

    private ProjectGraph _graph = null!;
    private IGitRepository _repo = null!;
    private IWorktree _workTree = null!;

    [Params("spectre.console", "MassTransit")]
    public required string Fixture { get; set; }

    [Params("LibGit2", "GitCli")]
    public required string RepositoryType { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var fixture = TestFixtures.Get(Fixture);
        _graph = new ProjectGraph(Path.Combine(fixture.Root, fixture.PrimaryEntrypoint));

        if (RepositoryType == "LibGit2")
        {
            var repo = LibGit2Repository.Discover(fixture.Root) ?? throw new InvalidOperationException();
            _repo = repo;
            _workTree = await repo.CreateWorktreeAsync("HEAD");
        }
        else
        {
            var repo = await GitRepository.DiscoverAsync(fixture.Root) ?? throw new InvalidOperationException();
            _repo = repo;
            var sha = await repo.LookupCommitShaAsync("HEAD") ?? throw new InvalidOperationException();
            _workTree = await repo.CreateWorktreeAsync(sha);
        }
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _workTree.DisposeAsync();
        (_repo as IDisposable)?.Dispose();
    }

    [Benchmark]
    public async Task<Snapshot> GenerateSnapshot()
    {
        return await SnapshotGenerator.GenerateSnapshot(_graph, _workTree);
    }
}
