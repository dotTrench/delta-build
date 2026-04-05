namespace DeltaBuild.Cli.Core;

public static class DiffCalculator
{
    public static DiffResult Calculate(Snapshot base_, Snapshot head)
    {
        var results = new Dictionary<string, ProjectDiffResult>();


        // Mark added and removed projects
        foreach (var project in head.Projects.Keys)
        {
            if (!base_.Projects.ContainsKey(project))
                results[project] = new ProjectDiffResult(
                    project,
                    ProjectState.Added,
                    [],
                    GetFileDiffs(project, base_, head));
        }

        foreach (var project in base_.Projects.Keys)
            if (!head.Projects.ContainsKey(project))
                results[project] = new ProjectDiffResult(
                    project,
                    ProjectState.Removed,
                    [],
                    GetFileDiffs(project, base_, head)
                );
        // Process remaining projects in topological order
        var sharedProjects = head.Projects
            .Where(p => base_.Projects.ContainsKey(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);

        foreach (var project in TopologicalSort(sharedProjects))
        {
            var fileDiffs = GetFileDiffs(project, base_, head);


            if (IsModified(fileDiffs))
            {
                results[project] = new ProjectDiffResult(project, ProjectState.Modified, [], fileDiffs);
                continue;
            }

            var affectedBy = head.Projects[project].ProjectReferences
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

    private static List<FileDiffResult> GetFileDiffs(string project, Snapshot @base, Snapshot head)
    {
        var baseFiles = @base.Projects.TryGetValue(project, out var bp)
            ? bp.InputFiles
            : [];
        var headFiles = head.Projects.TryGetValue(project, out var hp)
            ? hp.InputFiles
            : [];

        return baseFiles.Union(headFiles)
            .Order()
            .Select(file =>
            {
                @base.FileHashes.TryGetValue(file, out var baseHash);
                head.FileHashes.TryGetValue(file, out var headHash);

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
        foreach (var reference in snapshot.ProjectReferences)
            if (projects.ContainsKey(reference))
                inDegree[project]++;

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

public record FileDiffResult(string Path, FileState State);

public enum FileState
{
    Added,
    Deleted,
    Modified,
    Unchanged
}

public enum ProjectState
{
    Unchanged = 0,
    Added = 1,
    Removed = 2,
    Modified = 3,
    Affected = 4,
}

public record ProjectDiffResult(
    string Path,
    ProjectState State,
    IReadOnlyCollection<string> AffectedBy,
    IReadOnlyCollection<FileDiffResult> InputFiles);

public record DiffResult(IReadOnlyCollection<ProjectDiffResult> Projects);