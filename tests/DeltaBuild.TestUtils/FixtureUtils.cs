namespace DeltaBuild.TestUtils;

public static class FixtureUtils
{
    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(directory.FullName, "DeltaBuild.slnx")))
        {
            directory = directory.Parent;
            if (directory is null)
            {
                throw new InvalidOperationException("Could not find root");
            }
        }

        return directory.FullName;
    }

    private static string GetTestFixtureRoot()
    {
        return Path.Combine(GetRepositoryRoot(), "fixtures");
    }


    public static string GetFixturePath(string name)
    {
        return Path.Combine(GetTestFixtureRoot(), name);
    }
}

public sealed record TestFixture(string Name, string Root, string PrimaryEntrypoint)
{
    public override string ToString()
    {
        return Name;
    }
}

public static class TestFixtures
{
    public static readonly TestFixture SpectreConsole = new("Spectre.Console",
        FixtureUtils.GetFixturePath("spectre.console"),
        Path.Combine("src", "Spectre.Console.slnx")
    );

    public static readonly TestFixture MassTransit = new("MassTransit",
        FixtureUtils.GetFixturePath("MassTransit"),
        "MassTransit.sln"
    );

    public static TestFixture Get(string name) => name switch
    {
        "spectre.console" => SpectreConsole,
        "MassTransit" => MassTransit,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
    };
}
