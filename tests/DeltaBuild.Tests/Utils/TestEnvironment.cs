using DeltaBuild.Cli.Core;

namespace DeltaBuild.Tests.Utils;

public sealed class TestEnvironment : IEnvironment
{
    public TestEnvironment(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }
}
