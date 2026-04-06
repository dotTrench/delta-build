using System.Collections.Concurrent;

using DeltaBuild.Cli.Core.Git;

using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Prediction;
using Microsoft.Build.Prediction.Predictors;

namespace DeltaBuild.Cli.Core.Snapshots;

public static class SnapshotGenerator
{
    private static readonly IProjectGraphPredictor[] GraphPredictors = ProjectPredictors.AllProjectGraphPredictors
        .Where(it => it.GetType() != typeof(ProjectFileAndImportsGraphPredictor))
        .ToArray();

    public static Snapshot GenerateSnapshot(ProjectGraph projectGraph, IWorktree workTree)
    {
        var executor = new ProjectGraphPredictionExecutor(
            GraphPredictors,
            ProjectPredictors.AllProjectPredictors
        );


        var collector = new Collector(workTree);
        executor.PredictInputsAndOutputs(projectGraph, collector);

        var projects = collector.GetProjects(projectGraph).ToList();

        var hashes = new Dictionary<string, string>();
        foreach (var file in projects.SelectMany(p => p.InputFiles))
        {
            if (hashes.ContainsKey(file))
            {
                continue;
            }

            var sha = workTree.GetFileSha(file);
            if (sha is null)
            {
                // Untracked file, this file is probably generated on build and probably belongs in .gitignore.
                // We'll just skip the hash for this for now
                continue;
            }

            hashes.Add(file, sha);
        }

        return new Snapshot
        {
            Commit = workTree.Commit,
            Projects = projects,
            FileHashes = hashes.OrderBy(it => it.Key).ToDictionary()
        };
    }


    private sealed class Collector : IProjectPredictionCollector
    {
        private readonly IWorktree _worktree;
        private readonly ConcurrentDictionary<string, ProjectCollector> _collectors = new();

        public Collector(IWorktree worktree)
        {
            _worktree = worktree;
        }

        public void AddInputFile(string path, ProjectInstance projectInstance, string predictorName)
        {
            var fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path, projectInstance.Directory);

            if (!fullPath.StartsWith(_worktree.WorkingDirectory))
            {
                return;
            }

            var relativePath = PathHelpers.Normalize(Path.GetRelativePath(_worktree.WorkingDirectory, fullPath));

            if (_worktree.IsFileIgnored(relativePath))
            {
                return;
            }

            var collector = _collectors.GetOrAdd(projectInstance.FullPath, new ProjectCollector());

            collector.AddInputFile(relativePath);
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
            foreach (var (node, i) in graph.ProjectNodesTopologicallySorted.Select((node, i) => (node, i)))
            {
                var collector = _collectors.GetOrAdd(node.ProjectInstance.FullPath, new ProjectCollector());
                var references = node.ProjectReferences.Select(it =>
                    PathHelpers.Normalize(
                        Path.GetRelativePath(_worktree.WorkingDirectory, it.ProjectInstance.FullPath)
                    )
                );
                collector.AddProjectReferences(references);

                order.TryAdd(node.ProjectInstance.FullPath, i);
            }

            return _collectors
                .Select(it => new SnapshotProject
                    {
                        Path = PathHelpers.Normalize(Path.GetRelativePath(_worktree.WorkingDirectory, it.Key)),
                        TopologicalOrder = order.GetValueOrDefault(it.Key, int.MaxValue),
                        InputFiles = it.Value.GetInputFiles()
                            .Order()
                            .Select(PathHelpers.Normalize)
                            .ToList(),
                        ProjectReferences = it.Value.GetProjectReferences()
                            .Order()
                            .Select(PathHelpers.Normalize)
                            .ToList()
                    }
                )
                .OrderBy(it => it.TopologicalOrder)
                .ThenBy(it => it.Path);
        }

        private sealed class ProjectCollector
        {
            private readonly Lock _inputFilesLock = new();
            private readonly HashSet<string> _inputFiles = [];
            private readonly HashSet<string> _projectReferences = [];

            public void AddInputFile(string path)
            {
                lock (_inputFilesLock)
                {
                    _inputFiles.Add(path);
                }
            }

            public IReadOnlyCollection<string> GetInputFiles()
            {
                lock (_inputFilesLock)
                {
                    return _inputFiles;
                }
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
