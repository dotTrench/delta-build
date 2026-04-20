using System.Diagnostics;
using System.IO.Enumeration;

using Spectre.Console;

namespace DeltaBuild.Cli.Core.Diff;

public static class DiffRenderer
{
    public static void Render(IAnsiConsole console, IEnumerable<ProjectDiffResult> projects, bool detailed)
    {
        var tree = new Tree(new Text("delta-build", new Style(decoration: Decoration.Bold)));

        // Sort by status, then by original topological order
        var ordered = projects.Select((project, i) => (project, i))
            .OrderBy(it => GetOrder(it.project.State))
            .ThenBy(it => it.i)
            .Select(it => it.project);

        foreach (var project in ordered)
        {
            var label = new Text(
                $"{project.Path} ({project.State})",
                ProjectStyle(project.State)
            );
            var projectNode = tree.AddNode(label);

            foreach (var affectedBy in project.AffectedBy)
            {
                projectNode.AddNode(new Text($"via {affectedBy}", new Style(decoration: Decoration.Italic)));
            }

            foreach (var file in project.InputFiles)
            {
                if (!detailed && file.State == FileState.Unchanged)
                    continue;

                projectNode.AddNode(new Text(file.Path, FileStyle(file.State)));
            }
        }

        console.Write(tree);
    }

    private static int GetOrder(ProjectState state)
    {
        return state switch
        {
            ProjectState.Removed or ProjectState.Added => 0,
            ProjectState.Modified => 1,
            ProjectState.Affected => 2,
            ProjectState.Dependency => 3,
            ProjectState.Unchanged => 4,
            _ => 5
        };
    }


    private static Style ProjectStyle(ProjectState state) => state switch
    {
        ProjectState.Added => new Style(Color.Green),
        ProjectState.Removed => new Style(Color.Red),
        ProjectState.Modified => new Style(Color.Yellow),
        ProjectState.Affected => new Style(Color.Orange1),
        ProjectState.Dependency => new Style(Color.Blue),
        ProjectState.Unchanged => new Style(Color.Grey, decoration: Decoration.Dim),
        _ => throw new UnreachableException()
    };

    private static Style FileStyle(FileState state) => state switch
    {
        FileState.Added => new Style(Color.Green),
        FileState.Deleted => new Style(Color.Red),
        FileState.Modified => new Style(Color.Yellow),
        FileState.Unchanged => new Style(Color.Grey, decoration: Decoration.Dim),
        _ => throw new UnreachableException()
    };
}
