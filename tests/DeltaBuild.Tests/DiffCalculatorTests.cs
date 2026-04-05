using DeltaBuild.Cli.Core;

namespace DeltaBuild.Tests;

public class DiffCalculatorTests
{
    private static Snapshot EmptySnapshot() => new()
    {
        Commit = "abc123",
        Projects = new Dictionary<string, SnapshotProject>(),
        FileHashes = new Dictionary<string, string>()
    };

    private static Snapshot BuildSnapshot(
        string commit,
        Dictionary<string, (string[] InputFiles, string[] ProjectReferences)> projects,
        Dictionary<string, string> fileHashes
    )
    {
        return new Snapshot
        {
            Commit = commit,
            Projects = projects.ToDictionary(
                p => p.Key,
                p => new SnapshotProject
                {
                    InputFiles = p.Value.InputFiles,
                    ProjectReferences = p.Value.ProjectReferences
                }),
            FileHashes = fileHashes
        };
    }

    [Test]
    public async Task Unchanged_WhenSnapshotsAreIdentical()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new() { ["src/Core/Core.csproj"] = "hash1" });

        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new() { ["src/Core/Core.csproj"] = "hash1" });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Modified_WhenInputFileHashChanges()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj", "src/Core/Foo.cs"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/Core/Foo.cs"] = "hash2"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj", "src/Core/Foo.cs"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/Core/Foo.cs"] = "hash3"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Affected_WhenDirectDependencyIsModified()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], []),
                ["src/App/App.csproj"] = (["src/App/App.csproj"], ["src/Core/Core.csproj"])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/App/App.csproj"] = "hash2"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], []),
                ["src/App/App.csproj"] = (["src/App/App.csproj"], ["src/Core/Core.csproj"])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash3", // changed
                ["src/App/App.csproj"] = "hash2"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Affected);
    }


    [Test]
    public async Task Affected_WhenTransitiveDependencyIsModified()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Project1/Project1.csproj"] = (["src/Project1/Project1.csproj"], []),
                ["src/Project2/Project2.csproj"] = (["src/Project2/Project2.csproj"], ["src/Project1/Project1.csproj"]),
                ["src/Project3/Project3.csproj"] = (["src/Project3/Project3.csproj"],
                    ["src/Project2/Project2.csproj", "src/Project1/Project1.csproj"])
            },
            new()
            {
                ["src/Project1/Project1.csproj"] = "hash1",
                ["src/Project2/Project2.csproj"] = "hash2",
                ["src/Project3/Project3.csproj"] = "hash3"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/Project1/Project1.csproj"] = (["src/Project1/Project1.csproj"], []),
                ["src/Project2/Project2.csproj"] = (["src/Project2/Project2.csproj"], ["src/Project1/Project1.csproj"]),
                ["src/Project3/Project3.csproj"] = (["src/Project3/Project3.csproj"],
                    ["src/Project2/Project2.csproj", "src/Project1/Project1.csproj"])
            },
            new()
            {
                ["src/Project1/Project1.csproj"] = "hash4", // changed
                ["src/Project2/Project2.csproj"] = "hash2",
                ["src/Project3/Project3.csproj"] = "hash3"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var p1 = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Project1/Project1.csproj");
        await Assert.That(p1.State).IsEqualTo(ProjectState.Modified);

        var p2 = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Project2/Project2.csproj");
        await Assert.That(p2.State).IsEqualTo(ProjectState.Affected);

        var p3 = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Project3/Project3.csproj");
        await Assert.That(p3.State).IsEqualTo(ProjectState.Affected);
    }

    [Test]
    public async Task Added_WhenProjectExistsInHeadButNotBase()
    {
        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new() { ["src/Core/Core.csproj"] = "hash1" });

        var result = DiffCalculator.Calculate(EmptySnapshot(), head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Added);
    }

    [Test]
    public async Task Removed_WhenProjectExistsInBaseButNotHead()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new() { ["src/Core/Core.csproj"] = "hash1" });

        var result = DiffCalculator.Calculate(@base, EmptySnapshot());

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Removed);
    }

    [Test]
    public async Task Modified_WhenInputFileIsAdded()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj", "src/Core/Foo.cs"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/Core/Foo.cs"] = "hash2"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Modified_WhenInputFileIsDeleted()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj", "src/Core/Foo.cs"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/Core/Foo.cs"] = "hash2"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], [])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Affected_WhenDependencyIsRemoved()
    {
        var @base = BuildSnapshot("abc", new()
            {
                ["src/Core/Core.csproj"] = (["src/Core/Core.csproj"], []),
                ["src/App/App.csproj"] = (["src/App/App.csproj"], ["src/Core/Core.csproj"])
            },
            new()
            {
                ["src/Core/Core.csproj"] = "hash1",
                ["src/App/App.csproj"] = "hash2"
            });

        var head = BuildSnapshot("def", new()
            {
                ["src/App/App.csproj"] = (["src/App/App.csproj"], ["src/Core/Core.csproj"])
            },
            new()
            {
                ["src/App/App.csproj"] = "hash2"
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Removed);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Affected);
    }
}