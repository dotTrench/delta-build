namespace DeltaBuild.Cli.Core.Diff.Formatting;

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