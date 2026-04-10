using DeltaBuild.Cli.Commands;
using DeltaBuild.Cli.Core;
using DeltaBuild.Tests.Utils;

using Spectre.Console.Cli.Testing;

namespace DeltaBuild.Tests;

public sealed class DiffCommandOutputFormatTests : IDisposable
{
    private readonly TestRepository _repo;
    private readonly string _baseCommit;

    public DiffCommandOutputFormatTests()
    {
        _repo = TestRepository.Create();

        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj", x => x.AddItem("ProjectReference", @"../Core/Core.csproj"))
            .Commit("Initial commit");

        _baseCommit = _repo.GetCurrentCommit();

        _repo
            .WriteFile("src/Core/Foo.cs", "public class Foo {}")
            .Commit("Add Foo");
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
        });
        return app;
    }

    [Test]
    public async Task Json_WritesJsonToStdout(CancellationToken cancellationToken)
    {
        var stdout = new InMemoryStandardOutput();
        var result = await BuildApp(stdout).RunAsync(
            ["diff", "--base", _baseCommit, "--format", "json"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await VerifyJson(stdout.GetString());
    }

    [Test]
    public async Task Sln_WritesSlnToOutputFile(CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(_repo.WorkingDirectory, "affected.sln");
        try
        {
            var result = await BuildApp().RunAsync(
                ["diff", "--base", _baseCommit, "--format", "sln", "--output", outputFile],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Verify(await File.ReadAllTextAsync(outputFile, cancellationToken));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    [Test]
    public async Task Slnx_WritesSlnxToOutputFile(CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(_repo.WorkingDirectory, "affected.slnx");
        try
        {
            var result = await BuildApp().RunAsync(
                ["diff", "--base", _baseCommit, "--format", "slnx", "--output", outputFile],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await VerifyXml(await File.ReadAllTextAsync(outputFile, cancellationToken));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    [Test]
    public async Task Sln_InfersFormatFromExtension(CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(_repo.WorkingDirectory, "affected.sln");
        try
        {
            var result = await BuildApp().RunAsync(
                ["diff", "--base", _baseCommit, "--output", outputFile],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Verify(await File.ReadAllTextAsync(outputFile, cancellationToken));
        }
        finally
        {
            File.Delete(outputFile);
        }
    }

    [Test]
    public async Task Sln_Fails_WhenNoOutputFileSpecified(CancellationToken cancellationToken)
    {
        var result = await BuildApp().RunAsync(
            ["diff", "--base", _baseCommit, "--format", "sln"],
            cancellationToken);

        await Assert.That(result.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task Output_Fails_WhenFileExistsAndOverwriteNotSpecified(
        CancellationToken cancellationToken
    )
    {
        var outputFile = Path.Combine(_repo.WorkingDirectory, "affected.sln");
        try
        {
            await File.WriteAllTextAsync(outputFile, "existing content", cancellationToken);

            var result = await BuildApp().RunAsync(
                ["diff", "--base", _baseCommit, "--format", "sln", "--output", outputFile],
                cancellationToken);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Test]
    public async Task Output_OverwritesExistingFile_WhenOverwriteSpecified(CancellationToken cancellationToken)
    {
        var outputFile = Path.Combine(_repo.WorkingDirectory, "affected.sln");
        try
        {
            await File.WriteAllTextAsync(outputFile, "existing content", cancellationToken);

            var result = await BuildApp().RunAsync(
                ["diff", "--base", _baseCommit, "--format", "sln", "--output", outputFile, "--overwrite"],
                cancellationToken);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            var content = await File.ReadAllTextAsync(outputFile, cancellationToken);
            await Assert.That(content).IsNotEqualTo("existing content");
        }
        finally
        {
            File.Delete(outputFile);
        }
    }
}
