using System.Text.Json;
using System.Text.Json.Serialization;
using DeltaBuild.Cli.Core;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace DeltaBuild.Cli;

public interface IDiffFormatter
{
    Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken);
}

public sealed class PlainFormatter : IDiffFormatter
{
    public async Task FormatAsync(
        IEnumerable<ProjectDiffResult> projects,
        Stream output,
        CancellationToken cancellationToken
    )
    {
        await using var writer = new StreamWriter(output);

        foreach (var project in projects)
        {
            await writer.WriteLineAsync(project.Path);
        }
    }
}

public sealed class JsonFormatter : IDiffFormatter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new JsonStringEnumConverter<ProjectState>(),
            new JsonStringEnumConverter<FileState>()
        }
    };

    public Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken)
    {
        return JsonSerializer.SerializeAsync(output, projects, Options, cancellationToken);
    }
}

public sealed class SolutionFormatter : IDiffFormatter
{
    private readonly FileInfo _outputPath;
    private readonly string _repoRoot;
    private readonly bool _slnx;

    public SolutionFormatter(FileInfo outputPath, string repoRoot, bool slnx)
    {
        _outputPath = outputPath;
        _repoRoot = repoRoot;
        _slnx = slnx;
    }

    public Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken)
    {
        var model = new SolutionModel();
        foreach (var project in projects)
        {
            var absolutePath = Path.GetFullPath(project.Path, _repoRoot);
            var relativePath = Path.GetRelativePath(_outputPath.Directory!.FullName, absolutePath);
            model.AddProject(relativePath);
        }


        return _slnx
            ? SolutionSerializers.SlnXml.SaveAsync(output, model, cancellationToken)
            : SolutionSerializers.SlnFileV12.SaveAsync(output, model, cancellationToken);
    }
}