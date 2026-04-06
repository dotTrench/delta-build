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
    private IWorktree _workTree = null!;

    [Params("spectre.console", "MassTransit")]
    public required string Fixture { get; set; }


    [GlobalSetup]
    public async Task Setup()
    {
        var fixture = TestFixtures.Get(Fixture);
        _graph = new ProjectGraph(Path.Combine(fixture.Root, fixture.PrimaryEntrypoint));

        var repo = await GitRepository.DiscoverAsync(fixture.Root) ?? throw new InvalidOperationException();
        var sha = await repo.LookupCommitShaAsync("HEAD") ?? throw new InvalidOperationException();
        _workTree = await repo.CreateWorktreeAsync(sha);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _workTree.DisposeAsync();
    }

    [Benchmark]
    public async Task<Snapshot> GenerateSnapshot()
    {
        return await SnapshotGenerator.GenerateSnapshot(_graph, _workTree);
    }
}
