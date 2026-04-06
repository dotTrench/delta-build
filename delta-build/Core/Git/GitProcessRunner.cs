using System.Diagnostics;

namespace DeltaBuild.Cli.Core.Git;

public static class GitProcessRunner
{
    public static async Task<GitRunResult> RunCmd(
        string workingDirectory,
        IEnumerable<string> args,
        CancellationToken cancellationToken = default
    )
    {
        var startInfo = new ProcessStartInfo("git", args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            WorkingDirectory = workingDirectory,
        };

        using var p = Process.Start(startInfo);
        if (p is null)
        {
            return new GitRunResult(-1, "", "Failed to start git");
        }

        var stdoutTask = p.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = p.StandardError.ReadToEndAsync(cancellationToken);

        await p.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        
        return new GitRunResult(p.ExitCode, stdout, stderr);
    }
}

public sealed record GitRunResult(int ExitCode, string Stdout, string Stderr);
