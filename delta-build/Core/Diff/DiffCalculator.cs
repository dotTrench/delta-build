using System.Collections.Frozen;

using DeltaBuild.Cli.Core.Snapshots;

namespace DeltaBuild.Cli.Core.Diff;

public static class DiffCalculator
{
    public static DiffResult Calculate(Snapshot baseSnapshot, Snapshot headSnapshot)
    {
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
                        headProject: project
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
                    GetFileDiffs(baseProject: project, headProject: null)
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
                headProjects[project]
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

    private static List<FileDiffResult> GetFileDiffs(
        SnapshotProject? baseProject,
        SnapshotProject? headProject
    )
    {
        if (baseProject is null && headProject is null)
            return [];

        var baseFiles = baseProject?.InputFiles.Keys ?? [];
        var headFiles = headProject?.InputFiles.Keys ?? [];

        return headFiles.Union(baseFiles)
            .Select(it => new FileDiffResult(it, GetFileState(it)))
            .OrderBy(it => it.Path)
            .ToList();

        FileState GetFileState(string path)
        {
            if (headProject is null || !headProject.InputFiles.TryGetValue(path, out var headSha))
            {
                return FileState.Deleted;
            }

            if (baseProject is null || !baseProject.InputFiles.TryGetValue(path, out var baseSha))
            {
                return FileState.Added;
            }

            if (headSha != baseSha)
            {
                return FileState.Modified;
            }

            return FileState.Unchanged;
        }
    }

    private static bool IsModified(IReadOnlyCollection<FileDiffResult> files) =>
        files.Any(f => f.State != FileState.Unchanged);
}
