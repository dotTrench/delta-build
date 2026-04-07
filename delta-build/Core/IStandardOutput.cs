namespace DeltaBuild.Cli.Core;

public interface IStandardOutput
{
    Stream OpenStream();
}

public class ConsoleStandardOutput : IStandardOutput
{
    public Stream OpenStream() => Console.OpenStandardOutput();
}
