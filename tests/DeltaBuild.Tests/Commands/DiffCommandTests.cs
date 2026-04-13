using System.Text.Json;

using DeltaBuild.Cli.Commands;
using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Diff;
using DeltaBuild.Cli.Core.Diff.Formatting;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Cli.Core.Snapshots.Cache;
using DeltaBuild.Tests.Utils;

using LibGit2Sharp;

using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;

namespace DeltaBuild.Tests.Commands;

public sealed class DiffCommandTests : IDisposable
{
    private readonly TestRepository _repo;

    public DiffCommandTests()
    {
        _repo = TestRepository.Create();
    }

    public void Dispose() => _repo.Dispose();

    private CommandAppTester BuildApp(InMemoryStandardOutput? stdout = null)
    {
        var app = new CommandAppTester();
        app.Configure(c =>
        {
            c.Settings.Registrar.RegisterInstance<IEnvironment>(new TestEnvironment(_repo.WorkingDirectory));
            c.Settings.Registrar.RegisterInstance<IStandardOutput>(stdout ?? new InMemoryStandardOutput());
            c.Settings.Registrar.RegisterInstance<IStandardInput>(new NullStandardInput());
            c.AddCommand<DiffCommand>("diff");
            c.PropagateExceptions();
        });
        return app;
    }

    [Test]
    public async Task OutputsModifiedProject(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines();
        var line = await Assert.That(lines).HasSingleItem();
        await Assert.That(line).IsEqualTo("src/Core/Core.csproj");
    }

    [Test]
    public async Task OutputsAffectedProject_WhenDependencyModified(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj", x => x.AddItem("ProjectReference", @"../Core/Core.csproj"))
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines().ToList();

        await Assert.That(lines.Count).IsEqualTo(2);

        await Assert.That(lines).Contains("src/Core/Core.csproj");
        await Assert.That(lines).Contains("src/App/App.csproj");
    }

    [Test]
    public async Task DoesNotOutputUnchangedProjects_ByDefault(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines();
        await Assert.That(lines).DoesNotContain("src/App/App.csproj");
    }

    [Test]
    public async Task OutputsUnchangedProjects_WhenIncludeUnchanged(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--include-unchanged"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines();
        await Assert.That(lines).Contains("src/App/App.csproj");
    }

    [Test]
    public async Task DoesNotOutputRemovedProjects_ByDefault(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .DeleteFile("src/App/App.csproj")
            .Commit("Remove App");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines();
        await Assert.That(lines).DoesNotContain("src/App/App.csproj");
    }

    [Test]
    public async Task OutputsRemovedProjects_WhenIncludeRemoved(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .DeleteFile("src/App/App.csproj")
            .Commit("Remove App");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--include-removed"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines();
        var line = await Assert.That(lines).HasSingleItem();
        await Assert.That(line).IsEqualTo("src/App/App.csproj");
    }

    [Test]
    public async Task OutputsProject_WhenRootDirectoryBuildPropsModified(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("Directory.Build.props", "<Project />", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("Directory.Build.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>", cancellationToken);
        _repo.Commit("Update Directory.Build.props");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var line = await Assert.That(stdout.GetLines()).HasSingleItem();
        await Assert.That(line).IsEqualTo("src/Core/Core.csproj");
    }

    [Test]
    public async Task OutputsOnlyScopedProjects_WhenSubdirectoryDirectoryBuildPropsModified(CancellationToken cancellationToken)
    {
        // Directory.Build.props in src/ should only affect projects under src/,
        // not projects at the root level or in sibling directories.
        _repo
            .CreateCsproj("tools/Tool.csproj")
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj");
        await _repo.WriteFileAsync("src/Directory.Build.props", "<Project />", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Directory.Build.props", "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>", cancellationToken);
        _repo.Commit("Enable nullable in src");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines().ToList();

        await Assert.That(lines).Contains("src/Core/Core.csproj");
        await Assert.That(lines).Contains("src/App/App.csproj");
        await Assert.That(lines).DoesNotContain("tools/Tool.csproj");
    }

    [Test]
    public async Task OutputsAllProjects_WhenRootDirectoryBuildPropsModified_WithNestedDirectoryBuildProps(CancellationToken cancellationToken)
    {
        // MSBuild stops searching for parent Directory.Build.props once it finds one in a
        // subdirectory, so src/Core/Core.csproj only imports src/Directory.Build.props —
        // NOT the root one. Changing the root Directory.Build.props therefore only affects
        // projects that directly import it (i.e. tools/Tool.csproj, which has no closer one).
        _repo
            .CreateCsproj("tools/Tool.csproj")
            .CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("Directory.Build.props", "<Project />", cancellationToken);
        await _repo.WriteFileAsync("src/Directory.Build.props", "<Project />", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("Directory.Build.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>", cancellationToken);
        _repo.Commit("Update root Directory.Build.props");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines().ToList();

        await Assert.That(lines).Contains("tools/Tool.csproj");
        await Assert.That(lines).DoesNotContain("src/Core/Core.csproj");
    }

    [Test]
    public async Task OutputsOnlyScopedProjects_WhenNestedDirectoryBuildPropsModified(CancellationToken cancellationToken)
    {
        // Both root and src/ have Directory.Build.props.
        // Changing only the src/ one should affect src/ projects but not tools/.
        _repo
            .CreateCsproj("tools/Tool.csproj")
            .CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("Directory.Build.props", "<Project />", cancellationToken);
        await _repo.WriteFileAsync("src/Directory.Build.props", "<Project />", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Directory.Build.props", "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>", cancellationToken);
        _repo.Commit("Update src Directory.Build.props");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines().ToList();

        await Assert.That(lines).Contains("src/Core/Core.csproj");
        await Assert.That(lines).DoesNotContain("tools/Tool.csproj");
    }

    [Test]
    public async Task DoesNotOutputProject_WhenOnlyChangedFileIsIgnored(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("src/Core/appsettings.json", "{}", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/appsettings.json", "{ \"key\": \"value\" }", cancellationToken);
        _repo.Commit("Update settings");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--ignore", "**/*.json"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(stdout.GetLines()).IsEmpty();
    }

    [Test]
    public async Task OutputsProject_WhenIgnorePatternDoesNotMatch(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("src/Core/appsettings.json", "{}", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/appsettings.json", "{ \"key\": \"value\" }", cancellationToken);
        _repo.Commit("Update settings");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--ignore", "**/*.xml"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var line = await Assert.That(stdout.GetLines()).HasSingleItem();
        await Assert.That(line).IsEqualTo("src/Core/Core.csproj");
    }

    [Test]
    public async Task DoesNotOutputProject_WhenMultipleIgnorePatternsCoversAllChanges(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj");
        await _repo.WriteFileAsync("src/Core/appsettings.json", "{}", cancellationToken);
        await _repo.WriteFileAsync("src/Core/data.xml", "<root/>", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/appsettings.json", "{ \"key\": \"value\" }", cancellationToken);
        await _repo.WriteFileAsync("src/Core/data.xml", "<root><item/></root>", cancellationToken);
        _repo.Commit("Update config files");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--ignore", "**/*.json", "--ignore", "**/*.xml"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(stdout.GetLines()).IsEmpty();
    }

    [Test]
    public async Task OutputIsInTopologicalOrder(CancellationToken cancellationToken)
    {
        // Z has no dependencies (topological order 0).
        // A depends on Z (topological order 1).
        // Alphabetically A < Z, but topologically Z must come first.
        _repo
            .CreateCsproj("src/Z/Z.csproj")
            .CreateCsproj("src/A/A.csproj", x => x.AddItem("ProjectReference", @"../Z/Z.csproj"))
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Z/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Modify Z");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var lines = stdout.GetLines().ToList();

        await Assert.That(lines.Count).IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("src/Z/Z.csproj");
        await Assert.That(lines[1]).IsEqualTo("src/A/A.csproj");
    }

    [Test]
    public async Task OutputsProject_WhenExplicitlyImportedPropsFileModified(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj", x => x.AddImport("../../build/common.props"));
        await _repo.WriteFileAsync("build/common.props", "<Project />", cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("build/common.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>", cancellationToken);
        _repo.Commit("Update common.props");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        var line = await Assert.That(stdout.GetLines()).HasSingleItem();
        await Assert.That(line).IsEqualTo("src/Core/Core.csproj");
    }

    [Test]
    public async Task DependentProject_IsAffected_NotModified_WhenDependencyProjectFileChanges(CancellationToken cancellationToken)
    {
        // When Core.csproj itself changes, App should be Affected (via dependency graph)
        // not Modified (via direct file tracking).
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj", x => x.AddItem("ProjectReference", @"../Core/Core.csproj"))
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .CreateCsproj("src/Core/Core.csproj", x => x.AddProperty("LangVersion", "latest"))
            .Commit("Update Core.csproj");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--format", "json"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);

        var projects = JsonSerializer.Deserialize<List<ProjectDiffResult>>(stdout.GetString(), JsonFormatter.Options)!;

        var core = await Assert.That(projects).HasSingleItem(p => p.Path == "src/Core/Core.csproj");
        await Assert.That(core.State).IsEqualTo(ProjectState.Modified);

        var app = await Assert.That(projects).HasSingleItem(p => p.Path == "src/App/App.csproj");
        await Assert.That(app.State).IsEqualTo(ProjectState.Affected);
    }

    [Test]
    public async Task ReturnsExitCode1_WhenInvalidCacheSpecified(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", baseCommit, "--cache", "https://invalid"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task UsesCachedSnapshot_WhenCacheHit(CancellationToken cancellationToken)
    {
        // Pre-populate the cache for the base commit with an empty snapshot (no projects).
        // On a real diff both snapshots would have Core.csproj with identical hashes → no output.
        // With the empty cached base, Core.csproj only exists in head → shows as Added.
        // That observable difference proves the cached snapshot was used.
        _repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("unrelated.txt", "hello", cancellationToken);
        _repo.Commit("Unrelated change");

        var cacheDir = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var emptySnapshot = new Snapshot { Commit = baseCommit, Projects = [] };
            await new LocalSnapshotCache(cacheDir).SetAsync(baseCommit, emptySnapshot, cancellationToken);

            var stdout = new InMemoryStandardOutput();
            var result = await BuildApp(stdout).RunAsync(
                ["diff", "--base", baseCommit, "--cache", cacheDir.FullName],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            var line = await Assert.That(stdout.GetLines()).HasSingleItem();
            await Assert.That(line).IsEqualTo("src/Core/Core.csproj");
        }
        finally
        {
            cacheDir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task PopulatesCache_AfterDiff(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var headCommit = _repo.GetCurrentCommit();

        var cacheDir = Directory.CreateTempSubdirectory("delta-build-cache-tests");
        try
        {
            var result = await BuildApp().RunAsync(
                ["diff", "--base", baseCommit, "--cache", cacheDir.FullName],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0).Because(result.Output);
            var cache = new LocalSnapshotCache(cacheDir);
            await Assert.That(await cache.GetAsync(baseCommit, cancellationToken)).IsNotNull();
            await Assert.That(await cache.GetAsync(headCommit, cancellationToken)).IsNotNull();
        }
        finally
        {
            cacheDir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReturnsExitCode1_WhenBaseCommitNotFound(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", "nonexistent"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnsExitCodeOnEmpty_WhenNoProjectsChanged(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("unrelated.txt", "hello", cancellationToken);
        _repo.Commit("Add unrelated file");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", baseCommit, "--exit-code-on-empty", "2"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(2);
    }

    [Test]
    public async Task ReturnsZero_WhenProjectsChanged_EvenIfExitCodeOnEmptySet(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", baseCommit, "--exit-code-on-empty", "2"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task ReturnsZero_ByDefault_WhenNoProjectsChanged(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("unrelated.txt", "hello", cancellationToken);
        _repo.Commit("Add unrelated file");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", baseCommit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyOutput_WhenNoChangesBetweenCommits(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var commit = _repo.GetCurrentCommit();

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", commit, "--head", commit],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(stdout.GetLines()).IsEmpty();
    }

    [Test]
    public async Task EmptyOutput_WhenEntrypointHasNoProjects(CancellationToken cancellationToken)
    {
        await _repo.CreateSlnxAsync("empty.slnx", cancellationToken: cancellationToken);
        _repo.Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("unrelated.txt", "hello", cancellationToken);
        _repo.Commit("Add unrelated file");

        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", baseCommit, "--entrypoint", "empty.slnx"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(stdout.GetLines()).IsEmpty();
    }

    [Test]
    public async Task DoesNotLeaveWorktreesOrBranches_AfterDiff(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        await _repo.WriteFileAsync("src/Core/Foo.cs", "public class Foo {}", cancellationToken);
        _repo.Commit("Add Foo");

        var app = BuildApp();
        var result = await app.RunAsync(["diff", "--base", baseCommit], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        using var gitRepo = new Repository(_repo.WorkingDirectory);

        await Assert.That(gitRepo.Worktrees.Count()).IsEqualTo(0);
        await Assert.That(gitRepo.Branches.Count()).IsEqualTo(1);
    }
}
