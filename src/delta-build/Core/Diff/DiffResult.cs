namespace DeltaBuild.Cli.Core.Diff;

public record DiffResult(IReadOnlyCollection<ProjectDiffResult> Projects);

public record FileDiffResult(string Path, FileState State);

public enum FileState
{
    Unchanged = 0,
    Added = 1,
    Deleted = 2,
    Modified = 3,
}

public enum ProjectState
{
    Unchanged = 0,
    Added = 1,
    Removed = 2,
    Modified = 3,
    Affected = 4,
    Dependency = 5,
}

public record ProjectDiffResult(
    string Path,
    ProjectState State,
    IReadOnlyCollection<string> AffectedBy,
    IReadOnlyCollection<FileDiffResult> InputFiles
);
