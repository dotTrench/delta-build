using System.Collections.Frozen;

using DeltaBuild.Cli.Core.Snapshots;

namespace DeltaBuild.Cli.Core.Diff;

public static class DiffCalculator
{
    public static DiffResult Calculate(Snapshot baseSnapshot, Snapshot headSnapshot)
    {
        var fileStates = CalculateFileStates(baseSnapshot, headSnapshot);

        var baseProjects = baseSnapshot.Projects.ToFrozenDictionary(it => it.Path);
        var headProjects = headSnapshot.Projects.ToFrozenDictionary(it => it.Path);

        var results = new Dictionary<string, ProjectDiffResult>();
        // Mark added and removed projects
        foreach (var project in headProjects.Values)
        {
            if (!baseProjects.ContainsKey(project.Path))
            {
                results[project.Path] = new ProjectDiffResult(
                    project.Path,
                    ProjectState.Added,
                    [],
                    GetFileDiffs(
                        baseProject: null,
                        headProject: project,
                        fileStates
                    ));
            }
        }

        foreach (var project in baseProjects.Values)
        {
            if (!headProjects.ContainsKey(project.Path))
            {
                results[project.Path] = new ProjectDiffResult(
                    project.Path,
                    ProjectState.Removed,
                    [],
                    GetFileDiffs(baseProject: project, headProject: null, fileStates)
                );
            }
        }


        var sharedProjects = headSnapshot.Projects
            .Where(p => baseProjects.ContainsKey(p.Path))
            .OrderBy(it => it.TopologicalOrder)
            .Select(it => it.Path);

        foreach (var project in sharedProjects)
        {
            var fileDiffs = GetFileDiffs(
                baseProjects[project],
                headProjects[project],
                fileStates
            );


            if (IsModified(fileDiffs))
            {
                results[project] = new ProjectDiffResult(project, ProjectState.Modified, [], fileDiffs);
                continue;
            }

            var affectedBy = headProjects[project].ProjectReferences
                .Where(r => results.TryGetValue(r, out var p) && p.State is not ProjectState.Unchanged)
                .ToList();

            results[project] = affectedBy.Count > 0
                ? new ProjectDiffResult(project, ProjectState.Affected, affectedBy, fileDiffs)
                : new ProjectDiffResult(project, ProjectState.Unchanged, [], fileDiffs);
        }

        var projects = results.Values
            .OrderBy(it => it.Path)
            .ToList();
        return new DiffResult(projects);
    }

    private static FrozenDictionary<string, FileState> CalculateFileStates(Snapshot baseSnapshot, Snapshot headSnapshot)
    {
        var allFiles = baseSnapshot.FileHashes.Keys.Union(headSnapshot.FileHashes.Keys);

        return allFiles.ToFrozenDictionary(file => file, file =>
        {
            baseSnapshot.FileHashes.TryGetValue(file, out var baseHash);
            headSnapshot.FileHashes.TryGetValue(file, out var headHash);

            return (baseHash, headHash) switch
            {
                (null, not null) => FileState.Added,
                (not null, null) => FileState.Deleted,
                _ when baseHash != headHash => FileState.Modified,
                _ => FileState.Unchanged
            };
        });
    }

    private static List<FileDiffResult> GetFileDiffs(
        SnapshotProject? baseProject,
        SnapshotProject? headProject,
        FrozenDictionary<string, FileState> fileStates
    )
    {
        var allFiles = (baseProject?.InputFiles ?? []).Union(headProject?.InputFiles ?? []);

        return allFiles
            .Select(f => new FileDiffResult(f, fileStates.GetValueOrDefault(f, FileState.Unchanged)))
            .OrderBy(f => f.Path)
            .ToList();
    }

    private static bool IsModified(IReadOnlyCollection<FileDiffResult> files) =>
        files.Any(f => f.State != FileState.Unchanged);
}
