namespace DeltaBuild.Cli.Core.Diff.Formatting;

public interface IDiffFormatter
{
    Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken);
}