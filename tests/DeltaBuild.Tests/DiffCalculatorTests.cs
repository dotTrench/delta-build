using DeltaBuild.Cli.Core.Diff;
using DeltaBuild.Cli.Core.Snapshots;

namespace DeltaBuild.Tests;

public class DiffCalculatorTests
{
    private static Snapshot EmptySnapshot() => new() { Commit = "abc123", Projects = [] };

    private static Snapshot BuildSnapshot(
        string commit,
        Dictionary<string, (Dictionary<string, string> InputFiles, string[] ProjectReferences)> projects
    )
    {
        return new Snapshot
        {
            Commit = commit,
            Projects = projects.Select((it, i) => new SnapshotProject
            {
                Path = it.Key,
                TopologicalOrder = i,
                InputFiles = it.Value.InputFiles,
                ProjectReferences = it.Value.ProjectReferences
            }).ToList(),
        };
    }

    [Test]
    public async Task Unchanged_WhenSnapshotsAreIdentical()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Modified_WhenInputFileHashChanges()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2" }, [])
            });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash3" }, [])
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Affected_WhenDirectDependencyIsModified()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []),
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"])
            });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash3" }, []), // changed
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"])
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
            ["src/Project1/Project1.csproj"] = (new() { ["src/Project1/Project1.csproj"] = "hash1" }, []),
            ["src/Project2/Project2.csproj"] =
                (new() { ["src/Project2/Project2.csproj"] = "hash2" }, ["src/Project1/Project1.csproj"]),
            ["src/Project3/Project3.csproj"] = (new() { ["src/Project3/Project3.csproj"] = "hash3" },
                ["src/Project2/Project2.csproj", "src/Project1/Project1.csproj"])
        });

        var head = BuildSnapshot("def", new()
        {
            ["src/Project1/Project1.csproj"] = (new() { ["src/Project1/Project1.csproj"] = "hash4" }, []), // changed
            ["src/Project2/Project2.csproj"] =
                (new() { ["src/Project2/Project2.csproj"] = "hash2" }, ["src/Project1/Project1.csproj"]),
            ["src/Project3/Project3.csproj"] = (new() { ["src/Project3/Project3.csproj"] = "hash3" },
                ["src/Project2/Project2.csproj", "src/Project1/Project1.csproj"])
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
        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(EmptySnapshot(), head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Added);
    }

    [Test]
    public async Task Removed_WhenProjectExistsInBaseButNotHead()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(@base, EmptySnapshot());

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Removed);
    }

    [Test]
    public async Task Modified_WhenInputFileIsAdded()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2" }, [])
            });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Modified_WhenInputFileIsDeleted()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2" }, [])
            });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Affected_WhenDependencyIsModified_AndProjectsAreInReverseInsertionOrder()
    {
        var @base = new Snapshot
        {
            Commit = "abc",
            Projects =
            [
                new SnapshotProject
                {
                    Path = "src/App/App.csproj",
                    TopologicalOrder = 1,
                    InputFiles = new Dictionary<string, string> { ["src/App/App.csproj"] = "hash2" },
                    ProjectReferences = ["src/Core/Core.csproj"]
                },
                new SnapshotProject
                {
                    Path = "src/Core/Core.csproj",
                    TopologicalOrder = 0,
                    InputFiles = new Dictionary<string, string> { ["src/Core/Core.csproj"] = "hash1" },
                    ProjectReferences = []
                }
            ]
        };

        var head = new Snapshot
        {
            Commit = "def",
            Projects =
            [
                new SnapshotProject
                {
                    Path = "src/App/App.csproj",
                    TopologicalOrder = 1,
                    InputFiles = new Dictionary<string, string> { ["src/App/App.csproj"] = "hash2" },
                    ProjectReferences = ["src/Core/Core.csproj"]
                },
                new SnapshotProject
                {
                    Path = "src/Core/Core.csproj",
                    TopologicalOrder = 0,
                    InputFiles = new Dictionary<string, string> { ["src/Core/Core.csproj"] = "hash3" }, // changed
                    ProjectReferences = []
                }
            ]
        };

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Affected);
    }

    [Test]
    public async Task Unchanged_WhenOnlyChangedFileIsIgnored()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2" }, []) });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash3" }, []) });

        var result = DiffCalculator.Calculate(@base, head, new GlobMatcher(["**/*.cs"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Modified_WhenNonIgnoredFileAlsoChanged()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2", ["src/Core/appsettings.json"] = "hash3" },
                    [])
            });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (
                    new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash4", ["src/Core/appsettings.json"] = "hash5" },
                    [])
            });

        // Ignore the json but not the cs — project should still be Modified
        var result = DiffCalculator.Calculate(@base, head, new GlobMatcher(["**/*.json"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);
    }

    [Test]
    public async Task Unchanged_WhenIgnoredFileIsAdded()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/appsettings.json"] = "hash2" }, []) });

        var result = DiffCalculator.Calculate(@base, head, new GlobMatcher(["**/*.json"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Unchanged_WhenIgnoredFileIsDeleted()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/appsettings.json"] = "hash2" }, []) });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(@base, head, new GlobMatcher(["**/*.json"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task AddedProject_StillAdded_WhenAllFilesIgnored()
    {
        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1", ["src/Core/Foo.cs"] = "hash2" }, []) });

        var result = DiffCalculator.Calculate(EmptySnapshot(), head, new GlobMatcher(["**/*.cs", "**/*.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Added);
    }

    [Test]
    public async Task Affected_WhenDependencyIsRemoved()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []),
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"])
            });

        var head = BuildSnapshot("def",
            new() { ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"]) });

        var result = DiffCalculator.Calculate(@base, head);

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Removed);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Affected);
    }

    [Test]
    public async Task Unchanged_WhenModifiedProjectIsIgnored()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash2" }, []) });

        var result = DiffCalculator.Calculate(@base, head, ignoreProject: new GlobMatcher(["src/Core/Core.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Unchanged_WhenDependentOfIgnoredModifiedProject()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []),
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"])
            });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash3" }, []), // changed, but ignored
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, ["src/Core/Core.csproj"])
            });

        var result = DiffCalculator.Calculate(@base, head, ignoreProject: new GlobMatcher(["src/Core/Core.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Unchanged_WhenAddedProjectIsIgnored()
    {
        var head = BuildSnapshot("def",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(EmptySnapshot(), head, ignoreProject: new GlobMatcher(["src/Core/Core.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task Unchanged_WhenRemovedProjectIsIgnored()
    {
        var @base = BuildSnapshot("abc",
            new() { ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []) });

        var result = DiffCalculator.Calculate(@base, EmptySnapshot(), ignoreProject: new GlobMatcher(["src/Core/Core.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);
    }

    [Test]
    public async Task IgnoreProject_SupportsGlobPattern()
    {
        var @base = BuildSnapshot("abc",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash1" }, []),
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash2" }, [])
            });

        var head = BuildSnapshot("def",
            new()
            {
                ["src/Core/Core.csproj"] = (new() { ["src/Core/Core.csproj"] = "hash3" }, []), // changed
                ["src/App/App.csproj"] = (new() { ["src/App/App.csproj"] = "hash4" }, [])  // changed
            });

        // Ignore only Core
        var result = DiffCalculator.Calculate(@base, head, ignoreProject: new GlobMatcher(["**/Core.csproj"]));

        var core = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Unchanged);

        var app = await Assert.That(result.Projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Modified);
    }
}
