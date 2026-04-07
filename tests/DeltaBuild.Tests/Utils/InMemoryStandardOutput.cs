using System.Text;

using DeltaBuild.Cli.Core;

namespace DeltaBuild.Tests.Utils;

public sealed class InMemoryStandardOutput : IStandardOutput
{
    private readonly MemoryStream _stream = new();

    public byte[] GetBytes() => _stream.ToArray();
    public string GetString() => Encoding.UTF8.GetString(GetBytes());

    public IEnumerable<string> GetLines()
    {
        using var reader = new StringReader(GetString());
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    public Stream OpenStream()
    {
        return _stream;
    }
}
