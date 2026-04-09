using LibGit2Sharp;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace DeltaBuild.Tests.Utils;

public sealed class TestRepository : IDisposable
{
    private readonly Signature _author = new("delta-build", "delta-build@test.com", DateTimeOffset.Now);
    private bool _disposed;
    private readonly Repository _repository;

    private TestRepository(string repoPath)
    {
        _repository = new Repository(repoPath);
    }

    public string WorkingDirectory => _repository.Info.WorkingDirectory;

    public TestRepository WriteFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return this;
    }

    public async Task<TestRepository> WriteFileAsync(
        string relativePath,
        string content = "",
        CancellationToken cancellationToken = default
    )
    {
        var fullPath = Path.Combine(WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        return this;
    }

    public TestRepository CreateCsproj(string relativePath, Action<ProjectRootElement>? configuration = null)
    {
        var root = ProjectRootElement.Create(NewProjectFileOptions.None);
        root.Sdk = "Microsoft.NET.Sdk";

        configuration?.Invoke(root);

        root.Save(Path.Combine(WorkingDirectory, relativePath));

        return this;
    }

    public TestRepository UpdateCsProj(string relativePath, Action<ProjectRootElement> configuration)
    {
        var root = ProjectRootElement.Open(Path.Combine(WorkingDirectory, relativePath));
        if (root is null)
        {
            throw new ArgumentNullException(nameof(relativePath), $"Project {relativePath} could not be found");
        }
        configuration(root);

        root.Save();
        return this;
    }

    public async Task CreateSlnxAsync(
        string relativePath,
        Action<SolutionModel>? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        var fullPath = Path.Combine(WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var model = new SolutionModel();
        configuration?.Invoke(model);
        await using var stream = File.Create(fullPath);
        await SolutionSerializers.SlnXml.SaveAsync(stream, model, cancellationToken);
    }

    public async Task CreateSlnAsync(
        string relativePath,
        Action<SolutionModel>? configuration = null,
        CancellationToken cancellationToken = default
    )
    {
        var fullPath = Path.Combine(WorkingDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var model = new SolutionModel();
        configuration?.Invoke(model);
        await using var stream = File.Create(fullPath);
        await SolutionSerializers.SlnFileV12.SaveAsync(stream, model, cancellationToken);
    }

    public TestRepository DeleteFile(string relativePath)
    {
        File.Delete(Path.Combine(WorkingDirectory, relativePath));
        return this;
    }

    public TestRepository Commit(string message = "commit")
    {
        using var repo = new Repository(WorkingDirectory);
        Commands.Stage(repo, "*");
        repo.Commit(message, _author, _author);
        return this;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _repository.Dispose();

        if (Directory.Exists(WorkingDirectory))
            DeleteDirectory(WorkingDirectory);
    }

    private static void DeleteDirectory(string path)
    {
        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, recursive: true);
    }

    public string GetCurrentCommit()
    {
        return _repository.Head.Tip.Sha;
    }


    public static TestRepository Create()
    {
        var directory = Directory.CreateTempSubdirectory("delta-build-tests");

        var repoPath = Repository.Init(directory.FullName);
        return new TestRepository(repoPath);
    }
}
