using BenchmarkDotNet.Attributes;

using DeltaBuild.Cli.Core.Git;
using DeltaBuild.Cli.Core.Snapshots;

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
    private LibGit2Repository _repo = null!;
    private IWorktree _workTree = null!;

    [Params("fixtures/spectre.console/src/Spectre.Console.slnx", "fixtures/MassTransit/MassTransit.sln")]
    public required string Entrypoint { get; set; }


    [GlobalSetup]
    public void Setup()
    {
        var root = FixtureResolver.GetRepositoryRoot();

        var path = Path.Combine(root, Entrypoint);
        _graph = new ProjectGraph(path);
        _repo = LibGit2Repository.Discover(path) ?? throw new InvalidOperationException();
        _workTree = _repo.CreateWorktree("HEAD");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _workTree.Dispose();
        _repo.Dispose();
    }

    [Benchmark]
    public async Task<Snapshot> GenerateSnapshot()
    {
        return await SnapshotGenerator.GenerateSnapshot(_graph, _workTree);
    }
}
