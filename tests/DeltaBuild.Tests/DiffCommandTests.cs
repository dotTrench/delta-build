using System.Text.Json;

using DeltaBuild.Cli.Commands;
using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Diff;
using DeltaBuild.Cli.Core.Diff.Formatting;
using DeltaBuild.Tests.Utils;

using LibGit2Sharp;

using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;

namespace DeltaBuild.Tests;

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

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");

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

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");

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

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");

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

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");

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
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("Directory.Build.props", "<Project />")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("Directory.Build.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>")
            .Commit("Update Directory.Build.props");

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
            .CreateCsproj("src/App/App.csproj")
            .WriteFile("src/Directory.Build.props", "<Project />")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Directory.Build.props", "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>")
            .Commit("Enable nullable in src");

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
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("Directory.Build.props", "<Project />")
            .WriteFile("src/Directory.Build.props", "<Project />")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("Directory.Build.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>")
            .Commit("Update root Directory.Build.props");

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
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("Directory.Build.props", "<Project />")
            .WriteFile("src/Directory.Build.props", "<Project />")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Directory.Build.props", "<Project><PropertyGroup><Nullable>enable</Nullable></PropertyGroup></Project>")
            .Commit("Update src Directory.Build.props");

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
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("src/Core/appsettings.json", "{}")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Core/appsettings.json", "{ \"key\": \"value\" }")
            .Commit("Update settings");

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
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("src/Core/appsettings.json", "{}")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Core/appsettings.json", "{ \"key\": \"value\" }")
            .Commit("Update settings");

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
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("src/Core/appsettings.json", "{}")
            .WriteFile("src/Core/data.xml", "<root/>")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Core/appsettings.json", "{ \"key\": \"value\" }")
            .WriteFile("src/Core/data.xml", "<root><item/></root>")
            .Commit("Update config files");

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

        _repo
            .WriteFile("src/Z/Foo.cs", "public class Foo {}")
            .Commit("Modify Z");

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
        _repo
            .CreateCsproj("src/Core/Core.csproj", x => x.AddImport("../../build/common.props"))
            .WriteFile("build/common.props", "<Project />")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("build/common.props", "<Project><PropertyGroup><LangVersion>latest</LangVersion></PropertyGroup></Project>")
            .Commit("Update common.props");

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
    public async Task ReturnsExitCode1_WhenBaseCommitNotFound(CancellationToken cancellationToken)
    {
        _repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var result = await BuildApp().RunAsync(
            ["diff", "--base", "nonexistent"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task DoesNotLeaveWorktreesOrBranches_AfterDiff(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");

        var app = BuildApp();
        var result = await app.RunAsync(["diff", "--base", baseCommit], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        using var gitRepo = new Repository(_repo.WorkingDirectory);

        await Assert.That(gitRepo.Worktrees).IsEmpty();
        await Assert.That(gitRepo.Branches.Count()).IsEqualTo(1);
    }
}
