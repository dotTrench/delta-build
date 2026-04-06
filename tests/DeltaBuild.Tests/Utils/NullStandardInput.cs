using DeltaBuild.Cli.Core;

namespace DeltaBuild.Tests.Utils;

public sealed class NullStandardInput : IStandardInput
{
    public Stream OpenStream() => Stream.Null;
}