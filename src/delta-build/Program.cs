using DeltaBuild.Cli.Commands;
using DeltaBuild.Cli.Core;

using Microsoft.Build.Locator;

using Spectre.Console;
using Spectre.Console.Cli;

MSBuildLocator.RegisterDefaults();

var app = new CommandApp();
app.Configure(c =>
{
    c.Settings.Registrar.RegisterInstance<IEnvironment>(new SystemEnvironment());
    c.Settings.Registrar.RegisterInstance<IStandardOutput>(new ConsoleStandardOutput());
    c.Settings.Registrar.RegisterInstance<IStandardInput>(new ConsoleStandardInput());
    c.ConfigureConsole(AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error),
        Interactive = InteractionSupport.Detect
    }));

    c.SetApplicationName("delta-build");
    c.UseAssemblyInformationalVersion();

#if DEBUG
    c.ValidateExamples();
#endif

    c.AddCommand<SnapshotCommand>("snapshot")
        .WithDescription("Pre-compute a snapshot")
        .WithExample("snapshot", "--commit", "HEAD")
        .WithExample("snapshot", "--commit", "HEAD~1")
        .WithExample("snapshot", "--commit", "develop");

    c.AddCommand<DiffCommand>("diff")
        .WithDescription(
            "Compare two snapshots and output any projects that might have changed or is affected by changes"
        )
        .WithExample("diff", "--base", "develop")
        .WithExample("diff", "--base", "main", "--output", "/tmp/affected.slnx")
        .WithExample("diff", "--base", "main-snapshot.json");
});


return await app.RunAsync(args);
