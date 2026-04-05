using System.Diagnostics;
using DeltaBuild.Cli.Core;
using Spectre.Console;

namespace DeltaBuild.Cli;

public static class DiffRenderer
{
    public static void Render(IAnsiConsole console, IEnumerable<ProjectDiffResult> projects, bool detailed)
    {
        var tree = new Tree(new Text("delta-build", new Style(decoration: Decoration.Bold)));

        foreach (var project in projects)
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


    private static Style ProjectStyle(ProjectState state) => state switch
    {
        ProjectState.Added => new Style(Color.Green),
        ProjectState.Removed => new Style(Color.Red),
        ProjectState.Modified => new Style(Color.Yellow),
        ProjectState.Affected => new Style(Color.Orange1),
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