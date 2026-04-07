namespace DeltaBuild.Cli.Core.Git;

public sealed class GitProcessException : Exception
{
    public GitProcessException(string message) : base(message)
    {

    }
}
