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
    c.ConfigureConsole(AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Error),
        Interactive = InteractionSupport.Detect
    }));


    c.AddCommand<SnapshotCommand>("snapshot");
    c.AddCommand<DiffCommand>("diff");
});


await app.RunAsync(args);