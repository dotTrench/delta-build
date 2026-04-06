using DeltaBuild.Cli;
using DeltaBuild.Cli.Commands;
using DeltaBuild.Cli.Core;
using DeltaBuild.Cli.Core.Snapshots;
using DeltaBuild.Tests.Utils;
using DeltaBuild.TestUtils;

using LibGit2Sharp;

using Spectre.Console.Cli.Testing;

namespace DeltaBuild.Tests;

public class SnapshotCommandTests
{
    [Test]
    public async Task WritesSnapshotToStdout(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();

        repo
            .CreateCsproj("src/Core/Core.csproj")
            .WriteFile("src/Core/Sample.cs")
            .CreateCsproj("src/App/App.csproj", x => x.AddItem("ProjectReference", @"..\Core\Core.csproj"))
            .Commit("Initial commit");


        var stdout = new InMemoryStandardOutput();

        var app = BuildApp(repo, stdout);

        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode)
            .IsEqualTo(0)
            .Because(result.Output); // Just use console output as context


        await VerifyJson(stdout.GetString())
            .ScrubMember("commit");
    }

    [Test]
    public async Task WritesJsonToStandardOutput_WithTransitiveReferences(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo
            .CreateCsproj("src/Project1/Project1.csproj")
            .CreateCsproj("src/Project2/Project2.csproj",
                x => x.AddItem("ProjectReference", @"..\Project1\Project1.csproj"))
            .CreateCsproj("src/Project3/Project3.csproj",
                x => x.AddItem("ProjectReference", @"..\Project2\Project2.csproj"))
            .Commit("Initial commit");

        var stdout = new InMemoryStandardOutput();
        var app = BuildApp(repo, stdout);

        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await VerifyJson(stdout.GetString())
            .ScrubMember("commit");
    }

    [Test]
    public async Task ReturnsExitCode1_WhenNotInGitRepository(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-tests");
        try
        {
            var app = new CommandAppTester();
            var stdout = new InMemoryStandardOutput();
            var env = new TestEnvironment(directory.FullName);
            app.Configure(c =>
            {
                c.Settings.Registrar.RegisterInstance<IEnvironment>(env);
                c.Settings.Registrar.RegisterInstance<IStandardOutput>(stdout);
                c.AddCommand<SnapshotCommand>("snapshot");
            });

            var result = await app.RunAsync(["snapshot"], cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReturnsExitCode1_WhenCommitDoesNotExist(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var app = BuildApp(repo);
        var result = await app.RunAsync(["snapshot", "--commit", "nonexistent"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnsExitCode1_WhenEntrypointsAreAmbiguous(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo
            .WriteFile("First.sln", "")
            .WriteFile("Second.sln", "")
            .Commit("Initial commit");

        var app = BuildApp(repo);
        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnsExitCode1_WhenNoEntrypointsFound(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.WriteFile("README.md", "hello").Commit("Initial commit");

        var app = BuildApp(repo);
        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task ReturnsExitCode1_WhenExplicitEntrypointDoesNotExist(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();
        repo.CreateCsproj("src/Core/Core.csproj").Commit("Initial commit");

        var app = BuildApp(repo);
        var result = await app.RunAsync(
            ["snapshot", "--entrypoint", "src/NonExistent/NonExistent.csproj"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(1);
    }

    [Test]
    public async Task WritesJsonToStandardOutput_WithMultiTargetedProject(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();

        repo
            .CreateCsproj("src/Core/Core.csproj", x =>
            {
                x.AddProperty("TargetFrameworks", "net8.0;net9.0");
            })
            .CreateCsproj("src/App/App.csproj", x =>
            {
                x.AddProperty("TargetFramework", "net9.0");
                x.AddItem("ProjectReference", @"../Core/Core.csproj");
            })
            .Commit("Initial commit");

        var stdout = new InMemoryStandardOutput();
        var app = BuildApp(repo, stdout);

        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);

        var snapshot = SnapshotSerializer.Deserialize(stdout.GetBytes());

        using (Assert.Multiple())
        {
            // should only have one entry per project, not one per target framework
            await Assert.That(snapshot.Projects.Count).IsEqualTo(2);
            var coreProject =
                await Assert.That(snapshot.Projects).HasSingleItem(it => it.Path == "src/Core/Core.csproj");
            await Assert.That(snapshot.Projects).HasSingleItem(it => it.Path == "src/App/App.csproj");

            await Assert.That(coreProject.InputFiles).IsEquivalentTo(coreProject.InputFiles.Distinct());
        }
    }

    [Test]
    public async Task DoesNotLeaveWorktreesOrBranches_AfterSnapshot(CancellationToken cancellationToken)
    {
        using var repo = TestRepository.Create();

        repo
            .CreateCsproj("src/Core/Core.csproj")
            .Commit("Initial commit");

        var app = BuildApp(repo);
        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        using var gitRepo = new Repository(repo.WorkingDirectory);

        await Assert.That(gitRepo.Worktrees).IsEmpty();
        await Assert.That(gitRepo.Branches.Count()).IsEqualTo(1);
    }

    public static IEnumerable<Func<TestFixture>> Fixtures()
    {
        yield return () => TestFixtures.SpectreConsole;
        yield return () => TestFixtures.MassTransit;
    }

    [Test]
    [MethodDataSource(nameof(Fixtures))]
    public async Task RunsForFixture(TestFixture fixture, CancellationToken cancellationToken)
    {
        var stdout = new InMemoryStandardOutput();
        var app = BuildApp(fixture.Root, stdout);
        var result = await app.RunAsync(["snapshot"], cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);

        await VerifyJson(stdout.GetString());
    }

    private static CommandAppTester BuildApp(string workingDirectory, InMemoryStandardOutput? stdout = null)
    {
        var app = new CommandAppTester();
        app.Configure(c =>
        {
            c.Settings.Registrar.RegisterInstance<IEnvironment>(new TestEnvironment(workingDirectory));
            c.Settings.Registrar.RegisterInstance<IStandardOutput>(stdout ?? new InMemoryStandardOutput());
            c.AddCommand<SnapshotCommand>("snapshot");
        });
        return app;
    }

    private static CommandAppTester BuildApp(TestRepository repository, InMemoryStandardOutput? stdout = null)
        => BuildApp(repository.WorkingDirectory, stdout);
}
