using BenchmarkDotNet.Attributes;

using DeltaBuild.Cli.Core.Diff;
using DeltaBuild.Cli.Core.Snapshots;

namespace DeltaBuild.Benchmarks;

public enum GraphShape { Linear, Tree, Diamond, Flat }

[MemoryDiagnoser]
public class DiffCalculatorBenchmarks
{
    private Snapshot _base = null!;
    private Snapshot _head = null!;

    [Params(10, 50, 200, 1000)] public int ProjectCount { get; set; }


    [Params(GraphShape.Linear, GraphShape.Tree, GraphShape.Diamond, GraphShape.Flat)]
    public GraphShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _base = Shape switch
        {
            GraphShape.Linear => GenerateLinear(ProjectCount),
            GraphShape.Tree => GenerateTree(ProjectCount),
            GraphShape.Diamond => GenerateDiamond(ProjectCount),
            GraphShape.Flat => GenerateFlat(ProjectCount),
            _ => throw new ArgumentOutOfRangeException()
        };

        var changedCount = Random.Shared.Next(0, _base.FileHashes.Count);
        _head = ApplyChanges(_base, changedCount);
    }

    [Benchmark]
    public DiffResult Calculate() => DiffCalculator.Calculate(_base, _head);

    private static Snapshot ApplyChanges(Snapshot @base, int changedCount)
    {
        var updatedHashes = new Dictionary<string, string>(@base.FileHashes);

        foreach (var file in @base.FileHashes.Keys.Take(changedCount))
            updatedHashes[file] = Guid.NewGuid().ToString("N");

        return @base with { FileHashes = updatedHashes };
    }

    // Project1 -> Project2 -> Project3 -> ...
    private static Snapshot GenerateLinear(int count)
    {
        var projects = new List<SnapshotProject>();
        var hashes = new Dictionary<string, string>();

        for (var i = 0; i < count; i++)
        {
            var path = ProjectPath(i);
            var refs = i > 0 ? [ProjectPath(i - 1)] : Array.Empty<string>();
            projects.Add(new SnapshotProject
            {
                TopologicalOrder = i, Path = path, InputFiles = [path], ProjectReferences = refs
            });
            hashes[path] = Hash(i);
        }

        return BuildSnapshot(projects, hashes);
    }

    // Binary tree — each project depends on two children
    private static Snapshot GenerateTree(int count)
    {
        var projects = new List<SnapshotProject>();
        var hashes = new Dictionary<string, string>();

        for (var i = 0; i < count; i++)
        {
            var path = ProjectPath(i);
            var refs = new List<string>();
            var left = 2 * i + 1;
            var right = 2 * i + 2;
            if (left < count) refs.Add(ProjectPath(left));
            if (right < count) refs.Add(ProjectPath(right));

            projects.Add(new SnapshotProject
            {
                Path = path,
                TopologicalOrder = count - 1 - i, // reverse: leaves first, root last
                InputFiles = [path],
                ProjectReferences = refs.ToArray()
            });
            hashes[path] = Hash(i);
        }

        return BuildSnapshot(projects, hashes);
    }

    // First project is shared core, all others depend on it
    // plus a chain of dependencies between them
    private static Snapshot GenerateDiamond(int count)
    {
        var projects = new List<SnapshotProject>();
        var hashes = new Dictionary<string, string>();

        var core = ProjectPath(0);
        projects.Add(new SnapshotProject
        {
            Path = core, TopologicalOrder = 0, InputFiles = [core], ProjectReferences = []
        });
        hashes[core] = Hash(0);

        for (var i = 1; i < count; i++)
        {
            var path = ProjectPath(i);
            projects.Add(new SnapshotProject
            {
                Path = path, TopologicalOrder = 1, InputFiles = [path], ProjectReferences = [core]
            });
            hashes[path] = Hash(i);
        }

        return BuildSnapshot(projects, hashes);
    }

    // No dependencies between projects
    private static Snapshot GenerateFlat(int count)
    {
        var projects = new List<SnapshotProject>();
        var hashes = new Dictionary<string, string>();

        for (var i = 0; i < count; i++)
        {
            var path = ProjectPath(i);
            projects.Add(new SnapshotProject
            {
                Path = path, TopologicalOrder = i, InputFiles = [path], ProjectReferences = []
            });
            hashes[path] = Hash(i);
        }

        return BuildSnapshot(projects, hashes);
    }

    private static Snapshot BuildSnapshot(
        IReadOnlyList<SnapshotProject> projects,
        Dictionary<string, string> hashes
    ) => new()
    {
        Commit = "abc123",
        Projects = projects.OrderBy(it => it.TopologicalOrder).ThenBy(it => it.Path).ToList(),
        FileHashes = hashes
    };

    private static string ProjectPath(int i) => $"src/Project{i}/Project{i}.csproj";
    private static string Hash(int i) => $"hash{i:D40}";
}
