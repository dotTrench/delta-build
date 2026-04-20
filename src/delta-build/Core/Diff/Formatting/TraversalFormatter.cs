using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace DeltaBuild.Cli.Core.Diff.Formatting;

public sealed class TraversalFormatter : IDiffFormatter
{
    private readonly FileInfo _outputPath;
    private readonly string _repoRoot;
    private readonly string? _version;

    public TraversalFormatter(FileInfo outputPath, string repoRoot, string? version)
    {
        _outputPath = outputPath;
        _repoRoot = repoRoot;
        _version = version;
    }

    public async Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken)
    {
        var sdk = _version is not null
            ? $"Microsoft.Build.Traversal/{_version}"
            : "Microsoft.Build.Traversal";

        var root = ProjectRootElement.Create(NewProjectFileOptions.None);
        root.Sdk = sdk;

        var itemGroup = root.AddItemGroup();
        foreach (var project in projects)
        {
            var absolutePath = Path.GetFullPath(project.Path, _repoRoot);
            var relativePath = Path.GetRelativePath(_outputPath.Directory!.FullName, absolutePath)
                .Replace('\\', '/');
            itemGroup.AddItem("ProjectReference", relativePath);
        }

        await using var writer = new StreamWriter(output);
        root.Save(writer);
    }
}
