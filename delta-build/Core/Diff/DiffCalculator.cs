using DeltaBuild.Cli.Core.Snapshots;

namespace DeltaBuild.Cli.Core.Diff;

public static class DiffCalculator
{
    public static DiffResult Calculate(Snapshot baseSnapshot, Snapshot headSnapshot)
    {
        var results = new Dictionary<string, ProjectDiffResult>();


        // Mark added and removed projects
        foreach (var project in headSnapshot.Projects.Keys)
        {
            if (!baseSnapshot.Projects.ContainsKey(project))
                results[project] = new ProjectDiffResult(
                    project,
                    ProjectState.Added,
                    [],
                    GetFileDiffs(project, baseSnapshot, headSnapshot));
        }

        foreach (var project in baseSnapshot.Projects.Keys)
        {
            if (!headSnapshot.Projects.ContainsKey(project))
                results[project] = new ProjectDiffResult(
                    project,
                    ProjectState.Removed,
                    [],
                    GetFileDiffs(project, baseSnapshot, headSnapshot)
                );
        }

        // Process remaining projects in topological order
        var sharedProjects = headSnapshot.Projects
            .Where(p => baseSnapshot.Projects.ContainsKey(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);

        foreach (var project in TopologicalSort(sharedProjects))
        {
            var fileDiffs = GetFileDiffs(project, baseSnapshot, headSnapshot);


            if (IsModified(fileDiffs))
            {
                results[project] = new ProjectDiffResult(project, ProjectState.Modified, [], fileDiffs);
                continue;
            }

            var affectedBy = headSnapshot.Projects[project].ProjectReferences
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

    private static List<FileDiffResult> GetFileDiffs(string project, Snapshot baseSnapshot, Snapshot headSnapshot)
    {
        var baseFiles = baseSnapshot.Projects.TryGetValue(project, out var bp)
            ? bp.InputFiles
            : [];
        var headFiles = headSnapshot.Projects.TryGetValue(project, out var hp)
            ? hp.InputFiles
            : [];

        return baseFiles.Union(headFiles)
            .Order()
            .Select(file =>
            {
                baseSnapshot.FileHashes.TryGetValue(file, out var baseHash);
                headSnapshot.FileHashes.TryGetValue(file, out var headHash);

                var state = (baseHash, headHash) switch
                {
                    (null, _) => FileState.Added,
                    (_, null) => FileState.Deleted,
                    _ when baseHash != headHash => FileState.Modified,
                    _ => FileState.Unchanged
                };

                return new FileDiffResult(file, state);
            })
            .ToList();
    }

    private static bool IsModified(IReadOnlyCollection<FileDiffResult> files) =>
        files.Any(f => f.State != FileState.Unchanged);

    private static IEnumerable<string> TopologicalSort(IReadOnlyDictionary<string, SnapshotProject> projects)
    {
        // in-degree = number of dependencies (things this project depends on)
        var inDegree = projects.Keys.ToDictionary(p => p, _ => 0);

        foreach (var (project, snapshot) in projects)
        {
            foreach (var reference in snapshot.ProjectReferences)
            {
                if (projects.ContainsKey(reference))
                    inDegree[project]++;
            }
        }

        // start with projects that have no dependencies
        var queue = new Queue<string>(inDegree.Where(x => x.Value == 0).Select(x => x.Key));

        while (queue.TryDequeue(out var project))
        {
            yield return project;

            // reduce in-degree for projects that depend on this one
            foreach (var dependent in projects
                         .Where(p => p.Value.ProjectReferences.Contains(project))
                         .Select(p => p.Key))
            {
                if (--inDegree[dependent] == 0)
                    queue.Enqueue(dependent);
            }
        }
    }
}