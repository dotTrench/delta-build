using System.Text;

using DeltaBuild.Cli;
using DeltaBuild.Cli.Core;

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

    private static CommandAppTester BuildApp(TestRepository repo, InMemoryStandardOutput? stdout = null)
    {
        var app = new CommandAppTester();
        app.Configure(c =>
        {
            c.Settings.Registrar.RegisterInstance<IEnvironment>(new TestEnvironment(repo.WorkingDirectory));
            c.Settings.Registrar.RegisterInstance<IStandardOutput>(stdout ?? new InMemoryStandardOutput());
            c.AddCommand<SnapshotCommand>("snapshot");
        });
        return app;
    }
}

public sealed class InMemoryStandardOutput : IStandardOutput
{
    private readonly MemoryStream _stream = new();

    public byte[] GetBytes() => _stream.ToArray();
    public string GetString() => Encoding.UTF8.GetString(GetBytes());

    public IEnumerable<string> GetLines()
    {
        using var reader = new StringReader(GetString());
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    public Stream OpenStream()
    {
        return _stream;
    }
}

public sealed class TestEnvironment : IEnvironment
{
    public TestEnvironment(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }
}