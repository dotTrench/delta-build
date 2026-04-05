using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeltaBuild.Cli.Core.Diff.Formatting;

public sealed class JsonFormatter : IDiffFormatter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<ProjectState>(), new JsonStringEnumConverter<FileState>() }
    };

    public Task FormatAsync(IEnumerable<ProjectDiffResult> projects, Stream output, CancellationToken cancellationToken)
    {
        return JsonSerializer.SerializeAsync(output, projects, Options, cancellationToken);
    }
}