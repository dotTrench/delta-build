using DeltaBuild.Cli.Core;

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
    public async Task ReturnsNotFound_WhenDirectoryIsEmpty()
    {
        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.NotFound>();
    }

    [Test]
    public async Task ReturnsNotFound_WhenNoSolutionOrProjectFiles()
    {
        _repo.WriteFile("README.md", "hello");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.NotFound>();
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnFound()
    {
        _repo.WriteFile("MyApp.sln", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.sln");
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnxFound()
    {
        _repo.WriteFile("MyApp.slnx", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.slnx");
    }

    [Test]
    public async Task ReturnsSolution_WhenSingleSlnfFound()
    {
        _repo.WriteFile("MyApp.slnf", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.slnf");
    }

    [Test]
    public async Task ReturnsAmbiguous_WhenMultipleSolutionFilesFound()
    {
        _repo
            .WriteFile("First.sln", "")
            .WriteFile("Second.sln", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
    }

    [Test]
    public async Task ReturnsAmbiguous_WhenMixedSolutionFormatsFound()
    {
        _repo
            .WriteFile("MyApp.sln", "")
            .WriteFile("MyApp.slnx", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
    }

    [Test]
    public async Task ReturnsAmbiguous_CandidatesContainAllSolutionFiles()
    {
        _repo
            .WriteFile("First.sln", "")
            .WriteFile("Second.sln", "");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var ambiguous = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Ambiguous>();
        await Assert.That(ambiguous!.Candidates.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReturnsProjects_WhenNoSolutionButCsprojsFound()
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/App/App.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        await Assert.That(success!.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReturnsProjects_RecursivelyFindsNestedCsprojFiles()
    {
        _repo
            .CreateCsproj("src/Core/Core.csproj")
            .CreateCsproj("src/Nested/Deep/Deep.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        await Assert.That(success!.Paths.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PrefersSolution_OverCsprojFiles()
    {
        _repo
            .WriteFile("MyApp.sln", "")
            .CreateCsproj("src/Core/Core.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("MyApp.sln");
    }

    [Test]
    public async Task DoesNotRecurseForSolutionFiles()
    {
        _repo
            .WriteFile("nested/MyApp.sln", "")
            .CreateCsproj("src/Core/Core.csproj");

        var result = EntrypointDiscovery.Discover(_repo.WorkingDirectory);

        // should fall through to csproj discovery since sln is not in top-level directory
        var success = await Assert.That(result).IsTypeOf<EntrypointDiscoveryResult.Success>();
        var path = await Assert.That(success!.Paths).HasSingleItem();
        await Assert.That(path).EndsWith("Core.csproj");
    }
}