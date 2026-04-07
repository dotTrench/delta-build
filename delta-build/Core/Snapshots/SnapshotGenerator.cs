using System.Collections.Concurrent;

using DeltaBuild.Cli.Core.Git;

using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Prediction;
using Microsoft.Build.Prediction.Predictors;

namespace DeltaBuild.Cli.Core.Snapshots;

public static class SnapshotGenerator
{
    // ProjectFileAndImportsGraphPredictor is excluded because its job is already covered
    // by ProjectFileAndImportsPredictor (IProjectPredictor), which tracks each project's
    // own file and imports as inputs. The graph variant additionally reports the project
    // files and imports of referenced projects as inputs to the consumer — this would
    // cause downstream projects to be marked as Modified (via direct file tracking) rather
    // than Affected (via the dependency graph), collapsing the distinction between the two states.
    private static readonly IProjectGraphPredictor[] GraphPredictors = ProjectPredictors.AllProjectGraphPredictors
        .Where(it => it.GetType() != typeof(ProjectFileAndImportsGraphPredictor))
        .ToArray();

    public static async Task<Snapshot> GenerateSnapshot(
        ProjectGraph projectGraph,
        IWorktree workTree,
        CancellationToken cancellationToken = default
    )
    {
        var objectIds = await workTree.GetTrackedFileShasAsync(cancellationToken);
        var executor = new ProjectGraphPredictionExecutor(
            GraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );


        var collector = new Collector(workTree, objectIds);
        executor.PredictInputsAndOutputs(projectGraph, collector);

        var projects = collector.GetProjects(projectGraph).ToList();

        return new Snapshot { Commit = workTree.Commit, Projects = projects };
    }


    private sealed class Collector : IProjectPredictionCollector
    {
        private readonly IWorktree _worktree;
        private readonly ConcurrentDictionary<string, ProjectCollector> _collectors = new();
        private readonly IReadOnlyDictionary<string, string> _objectIds;

        public Collector(IWorktree worktree, IReadOnlyDictionary<string, string> objectIds)
        {
            _worktree = worktree;
            _objectIds = objectIds;
        }

        public void AddInputFile(string path, ProjectInstance projectInstance, string predictorName)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path, projectInstance.Directory);

            if (!fullPath.StartsWith(_worktree.WorkingDirectory))
            {
                return;
            }

            var relativePath = PathHelpers.Normalize(Path.GetRelativePath(_worktree.WorkingDirectory, fullPath));
            if (!_objectIds.TryGetValue(relativePath, out var sha))
            {
                return;
            }

            var collector = _collectors.GetOrAdd(projectInstance.FullPath, new ProjectCollector());

            collector.AddInputFile(relativePath, sha);
        }

        public void AddInputDirectory(string path, ProjectInstance projectInstance, string predictorName)
        {
        }

        public void AddOutputFile(string path, ProjectInstance projectInstance, string predictorName)
        {
        }

        public void AddOutputDirectory(string path, ProjectInstance projectInstance, string predictorName)
        {
        }


        public IEnumerable<SnapshotProject> GetProjects(ProjectGraph graph)
        {
            var order = new Dictionary<string, int>();
            foreach (var node in graph.ProjectNodesTopologicallySorted)
            {
                var collector = _collectors.GetOrAdd(node.ProjectInstance.FullPath, new ProjectCollector());
                var references = node.ProjectReferences.Select(it =>
                    PathHelpers.Normalize(
                        Path.GetRelativePath(_worktree.WorkingDirectory, it.ProjectInstance.FullPath)
                    )
                );
                collector.AddProjectReferences(references);
                if (order.ContainsKey(node.ProjectInstance.FullPath))
                {
                    continue;
                }

                var depth = node.ProjectReferences
                    .Select(r => order[r.ProjectInstance.FullPath])
                    .DefaultIfEmpty(-1)
                    .Max() + 1;

                order.TryAdd(node.ProjectInstance.FullPath, depth);
            }

            return _collectors
                .Select(it => new SnapshotProject
                {
                    Path = PathHelpers.Normalize(Path.GetRelativePath(_worktree.WorkingDirectory, it.Key)),
                    TopologicalOrder = order.GetValueOrDefault(it.Key, int.MaxValue),
                    InputFiles = it.Value.GetInputFiles()
                            .OrderBy(file => file.Key)
                            .ToDictionary(),
                    ProjectReferences = it.Value.GetProjectReferences()
                            .Order()
                            .ToList()
                }
                )
                .OrderBy(it => it.TopologicalOrder)
                .ThenBy(it => it.Path);
        }

        private sealed class ProjectCollector
        {
            private readonly ConcurrentDictionary<string, string> _inputFiles = [];
            private readonly HashSet<string> _projectReferences = [];

            public void AddInputFile(string path, string sha)
            {
                _inputFiles.TryAdd(path, sha);
            }

            public IReadOnlyDictionary<string, string> GetInputFiles()
            {
                return _inputFiles;
            }

            public IReadOnlyCollection<string> GetProjectReferences() => _projectReferences;

            public void AddProjectReferences(IEnumerable<string> references)
            {
                foreach (var reference in references)
                {
                    _projectReferences.Add(reference);
                }
            }
        }
    }
}
