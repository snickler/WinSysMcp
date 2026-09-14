using System.Diagnostics;
using System.Text;

namespace WinSysMcp;

internal static class OsProcess
{
    internal static string Run(string fileName, string arguments, int timeoutMs = 30_000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return $"Error: timed out after {timeoutMs}ms running '{fileName} {arguments}'.";
            }

            // Ensure async readers finish.
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var err = stderr.ToString().Trim();
                if (string.IsNullOrEmpty(err))
                    err = stdout.ToString().Trim();
                return $"Error (Exit Code {process.ExitCode}): {err}";
            }

            return stdout.ToString();
        }
        catch (Exception ex)
        {
            return $"Exception running command: {ex.Message}";
        }
    }

    internal static (int ExitCode, string StdOut, string StdErr) RunRaw(string fileName, string arguments, int timeoutMs = 30_000)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return (-1, "", $"timed out after {timeoutMs}ms");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            return (process.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (-1, "", ex.Message);
        }
    }
}
