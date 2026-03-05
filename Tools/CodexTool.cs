using System.ComponentModel;
using System.Diagnostics;
using ModelContextProtocol.Server;

namespace ClaudeMcpBridge.Tools;

[McpServerToolType]
public static class CodexTool
{
    [McpServerTool, Description("Send a task to OpenAI Codex CLI for non-interactive execution. Returns the agent's final response.")]
    public static async Task<string> RunCodexTask(
        [Description("The task or instruction to give to Codex")]
        string task,
        [Description("Absolute path to the working directory where Codex should operate. Defaults to home directory.")]
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var workDir = workingDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!Directory.Exists(workDir))
            return $"[Error] Working directory does not exist: {workDir}";

        var outputFile = Path.GetTempFileName();

        try
        {
            var psi = new ProcessStartInfo("codex")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workDir
            };

            // codex exec --full-auto --skip-git-repo-check --output-last-message <file> <task>
            psi.ArgumentList.Add("exec");
            psi.ArgumentList.Add("--full-auto");
            psi.ArgumentList.Add("--skip-git-repo-check");
            psi.ArgumentList.Add("--output-last-message");
            psi.ArgumentList.Add(outputFile);
            psi.ArgumentList.Add(task);

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start codex process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // Primary: last message written by --output-last-message
            if (File.Exists(outputFile))
            {
                var lastMessage = await File.ReadAllTextAsync(outputFile, cancellationToken);
                if (!string.IsNullOrWhiteSpace(lastMessage))
                    return lastMessage.Trim();
            }

            // Fallback: raw stdout
            if (!string.IsNullOrWhiteSpace(stdout))
                return stdout.Trim();

            // Error output
            if (!string.IsNullOrWhiteSpace(stderr))
                return $"[Codex Error]\n{stderr.Trim()}";

            return process.ExitCode == 0
                ? "Task completed with no output."
                : $"Codex exited with code {process.ExitCode}.";
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }
}
