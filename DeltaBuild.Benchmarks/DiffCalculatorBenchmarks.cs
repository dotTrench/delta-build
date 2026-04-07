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

        _head = ApplyChanges(_base);
    }

    [Benchmark]
    public DiffResult Calculate() => DiffCalculator.Calculate(_base, _head);

    private static Snapshot ApplyChanges(Snapshot @base)
    {
        return @base;
    }

    // Project1 -> Project2 -> Project3 -> ...
    private static Snapshot GenerateLinear(int count)
    {
        var projects = new List<SnapshotProject>();

        for (var i = 0; i < count; i++)
        {
            var path = ProjectPath(i);
            var refs = i > 0 ? [ProjectPath(i - 1)] : Array.Empty<string>();
            projects.Add(new SnapshotProject
            {
                TopologicalOrder = i,
                Path = path,
                InputFiles = new Dictionary<string, string>() { [path] = Hash(i) },
                ProjectReferences = refs
            });
        }

        return BuildSnapshot(projects);
    }

    // Binary tree — each project depends on two children
    private static Snapshot GenerateTree(int count)
    {
        var projects = new List<SnapshotProject>();

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
                InputFiles = new Dictionary<string, string> { [path] = Hash(i) },
                ProjectReferences = refs.ToArray()
            });
        }

        return BuildSnapshot(projects);
    }

    // First project is shared core, all others depend on it
    // plus a chain of dependencies between them
    private static Snapshot GenerateDiamond(int count)
    {
        var projects = new List<SnapshotProject>();

        var core = ProjectPath(0);
        projects.Add(new SnapshotProject
        {
            Path = core,
            TopologicalOrder = 0,
            InputFiles = new Dictionary<string, string> { [core] = Hash(0) },
            ProjectReferences = []
        });

        for (var i = 1; i < count; i++)
        {
            var path = ProjectPath(i);
            projects.Add(new SnapshotProject
            {
                Path = path,
                TopologicalOrder = 1,
                InputFiles = new Dictionary<string, string> { [path] = Hash(i) },
                ProjectReferences = [core]
            });
        }

        return BuildSnapshot(projects);
    }

    // No dependencies between projects
    private static Snapshot GenerateFlat(int count)
    {
        var projects = new List<SnapshotProject>();

        for (var i = 0; i < count; i++)
        {
            var path = ProjectPath(i);
            projects.Add(new SnapshotProject
            {
                Path = path,
                TopologicalOrder = i,
                InputFiles = new Dictionary<string, string> { [path] = Hash(i) },
                ProjectReferences = []
            });
        }

        return BuildSnapshot(projects);
    }

    private static Snapshot BuildSnapshot(
        IReadOnlyList<SnapshotProject> projects
    ) => new()
    {
        Commit = "abc123",
        Projects = projects.OrderBy(it => it.TopologicalOrder).ThenBy(it => it.Path).ToList(),
    };

    private static string ProjectPath(int i) => $"src/Project{i}/Project{i}.csproj";
    private static string Hash(int i) => $"hash{i:D40}";
}
