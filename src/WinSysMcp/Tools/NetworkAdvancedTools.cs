using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Diagnostics;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class NetworkAdvancedTools
{
    private static string RunCommand(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                return $"Error (Exit Code {process.ExitCode}): {error}";
            }

            return output;
        }
        catch (Exception ex)
        {
            return $"Exception running command: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_arp_table"), Description("Runs 'arp -a' and returns the system ARP table as text. Read-only and useful for local network diagnostics — may require command availability depending on OS.")]
    public static string GetArpTable()
    {
        return RunCommand("arp", "-a");
    }

    [McpServerTool(Name = "get_route_table"), Description("Executes 'route print' and returns the system routing table. Read-only — useful for routing and networking diagnostics.")]
    public static string GetRouteTable()
    {
        return RunCommand("route", "print");
    }

    [McpServerTool(Name = "get_dns_cache"), Description("Runs 'ipconfig /displaydns' to show the local DNS resolver cache. Read-only informational command — may be empty depending on system state.")]
    public static string GetDnsCache()
    {
        return RunCommand("ipconfig", "/displaydns");
    }

    [McpServerTool(Name = "flush_dns"), Description("Flushes the DNS resolver cache via 'ipconfig /flushdns'. This modifies system DNS cache state and may require permission; use when troubleshooting name resolution.")]
    public static string FlushDns()
    {
        return RunCommand("ipconfig", "/flushdns");
    }

    [McpServerTool(Name = "get_firewall_rules"), Description("Fetches firewall rules using 'netsh advfirewall firewall show rule name=all'. Parameter: ruleName (optional). Read-only but requires netsh availability and adequate privileges to see full details. Example: ruleName='all' or 'Allow HTTP'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"ruleName\":{\"type\":\"string\"}}}")]
    public static string GetFirewallRules(
        [System.ComponentModel.Description("Filter by rule name (optional). Default is 'all'.")] string ruleName = "all")
    {
        // Sanitize input to prevent injection (basic check)
        if (ruleName.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_'))
        {
            return "Invalid rule name. Only alphanumeric characters, spaces, dashes, and underscores are allowed.";
        }

        return RunCommand("netsh", $"advfirewall firewall show rule name=\"{ruleName}\" verbose");
    }
}
