using DeltaBuild.Cli.Core;
using DeltaBuild.Tests.Utils;

namespace DeltaBuild.Tests;

public sealed class EntrypointDiscoveryTests : IDisposable
{
    private readonly TestRepository _repo;

    public EntrypointDiscoveryTests()
    {
        _repo = TestRepository.Create();
    }

    public void Dispose() => _repo.Dispose();

    [Test]
    public async Task ReturnsNotFound_WhenDirectoryIsEmpty(CancellationToken cancellationToken)
    {
        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.NotFound>();
    }

    [Test]
    public async Task ReturnsNotFound_WhenNoSolutionOrProjectFiles(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("README.md", "hello", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.NotFound>();
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnFound(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("MyApp.sln", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.sln");
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnxFound(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("MyApp.slnx", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.slnx");
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnfFound(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("MyApp.slnf", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.slnf");
    }

    [Test]
    public async Task ReturnsAmbiguous_WhenMultipleSolutionFilesFound(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("First.sln", "", cancellationToken);
        await _repo.WriteFileAsync("Second.sln", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
    }

    [Test]
    public async Task ReturnsAmbiguous_WhenMixedSolutionFormatsFound(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("MyApp.sln", "", cancellationToken);
        await _repo.WriteFileAsync("MyApp.slnx", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
    }

    [Test]
    public async Task ReturnsAmbiguous_CandidatesContainAllSolutionFiles(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("First.sln", "", cancellationToken);
        await _repo.WriteFileAsync("Second.sln", "", cancellationToken);

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var ambiguous = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
        await Assert.That(ambiguous!.Candidates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReturnsProjects_WhenNoSolutionButCsprojsFound(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        await Assert.That(success!.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReturnsProjects_RecursivelyFindsNestedCsprojFiles(CancellationToken cancellationToken)
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/Nested/Deep/Deep.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        await Assert.That(success!.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PrefersSolution_OverCsprojFiles(CancellationToken cancellationToken)
    {
        await _repo.WriteFileAsync("MyApp.sln", "", cancellationToken);
        _repo.CreateCsproj("src/Core/Core.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.sln");
    }

    [Test]
    public async Task DoesNotRecurseForSolutionFiles(CancellationToken cancellationToken)
    {
        await _repo.CreateSlnAsync("nested/MyApp.sln", cancellationToken: cancellationToken);
        _repo.CreateCsproj("src/Core/Core.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        // should fall through to csproj discovery since sln is not in top-level directory
        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("Core.csproj");
    }

    [Test]
    public async Task Resolve_ReturnsSuccess_WhenPatternMatchesSingleSolution()
    {
        _repo.WriteFile("MyApp.sln", "");

        var result = EntrypointDiscovery.Resolve(_repo.WorkingDirectory, ["*.sln"]);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.sln");
    }

    [Test]
    public async Task Resolve_ReturnsAmbiguous_WhenPatternMatchesMultipleSolutionFiles()
    {
        _repo
            .WriteFile("First.sln", "")
            .WriteFile("Second.sln", "");

        var result = EntrypointDiscovery.Resolve(_repo.WorkingDirectory, ["*.sln"]);

        var ambiguous = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
        await Assert.That(ambiguous!.Candidates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Resolve_ReturnsSuccess_WhenPatternMatchesMultipleCsprojFiles()
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj");

        var result = EntrypointDiscovery.Resolve(_repo.WorkingDirectory, ["**/*.csproj"]);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        await Assert.That(success!.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Resolve_ReturnsNotFound_WhenPatternMatchesNothing()
    {
        _repo.CreateCsproj("src/Core/Core.csproj");

        var result = EntrypointDiscovery.Resolve(_repo.WorkingDirectory, ["*.sln"]);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.NotFound>();
    }
}
