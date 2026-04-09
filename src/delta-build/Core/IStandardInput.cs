namespace DeltaBuild.Cli.Core;

public interface IStandardInput
{
    Stream OpenStream();
}

public sealed class ConsoleStandardInput : IStandardInput
{
    public Stream OpenStream() => Console.OpenStandardInput();
}
