using System.Diagnostics;

namespace FModel.ModTools;

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken);
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Could not start process: {executable}");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited while cancellation was being handled.
            }
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return new ProcessExecutionResult(process.ExitCode, stdout, stderr);
    }
}
