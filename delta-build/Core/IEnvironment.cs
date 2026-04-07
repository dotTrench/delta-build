namespace DeltaBuild.Cli.Core;

public interface IEnvironment
{
    string WorkingDirectory { get; }
}

public sealed class SystemEnvironment : IEnvironment
{
    public string WorkingDirectory => Directory.GetCurrentDirectory();
}
