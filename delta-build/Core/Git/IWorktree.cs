namespace DeltaBuild.Cli.Core.Git;

public interface IWorktree : IAsyncDisposable
{
    string WorkingDirectory { get; }
    string Commit { get; }

    /// <summary>
    /// Collect all blob object-ids for the current tee
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyDictionary<string, string>> GetTrackedFileShasAsync(CancellationToken cancellationToken = default);
}
