using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace DeltaBuild.Cli.Core.Diff.Formatting;

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
