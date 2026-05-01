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
            var process = new Process
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

    [McpServerTool(Name = "get_arp_table")]
    [System.ComponentModel.Description("Retrieves the ARP table using 'arp -a'.")]
    public static string GetArpTable()
    {
        return RunCommand("arp", "-a");
    }

    [McpServerTool(Name = "get_route_table")]
    [System.ComponentModel.Description("Retrieves the IP routing table using 'route print'.")]
    public static string GetRouteTable()
    {
        return RunCommand("route", "print");
    }

    [McpServerTool(Name = "get_dns_cache")]
    [System.ComponentModel.Description("Retrieves the DNS resolver cache using 'ipconfig /displaydns'.")]
    public static string GetDnsCache()
    {
        return RunCommand("ipconfig", "/displaydns");
    }

    [McpServerTool(Name = "flush_dns")]
    [System.ComponentModel.Description("Flushes the DNS resolver cache using 'ipconfig /flushdns'.")]
    public static string FlushDns()
    {
        return RunCommand("ipconfig", "/flushdns");
    }

    [McpServerTool(Name = "get_firewall_rules")]
    [System.ComponentModel.Description("Retrieves Windows Firewall rules using 'netsh advfirewall firewall show rule name=all'.")]
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
