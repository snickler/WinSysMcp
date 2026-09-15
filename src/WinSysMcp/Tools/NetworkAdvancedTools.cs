using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class NetworkAdvancedTools
{
    [McpServerTool(Name = "get_arp_table"), Description("Returns the system ARP/neighbor table. Windows: arp -a. Linux: ip neigh. Read-only.")]
    public static string GetArpTable()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return OsProcess.Run("arp", "-a");
#endif
        var ip = OsProcess.Run("ip", "neigh");
        if (!ip.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            && !ip.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return ip;
        }

        return OsProcess.Run("arp", "-an");
    }

    [McpServerTool(Name = "get_route_table"), Description("Returns the system routing table. Windows: route print. Linux: ip route. Read-only.")]
    public static string GetRouteTable()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return OsProcess.Run("route", "print");
#endif
        var ip = OsProcess.Run("ip", "route");
        if (!ip.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            && !ip.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return ip;
        }

        return OsProcess.Run("route", "-n");
    }

    [McpServerTool(Name = "get_dns_cache"), Description("Shows local DNS resolver cache/status. Windows: ipconfig /displaydns. Linux: resolvectl statistics (cache dump is often unavailable).")]
    public static string GetDnsCache()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return OsProcess.Run("ipconfig", "/displaydns");
#endif
        var resolvectl = OsProcess.Run("resolvectl", "statistics");
        if (!resolvectl.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            && !resolvectl.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return resolvectl;
        }

        return "DNS cache dump is not available on this Linux host (install systemd-resolved / resolvectl for statistics).";
    }

    [McpServerTool(Name = "flush_dns"), Description("Flushes the DNS resolver cache. Windows: ipconfig /flushdns. Linux: resolvectl flush-caches.")]
    public static string FlushDns()
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
            return OsProcess.Run("ipconfig", "/flushdns");
#endif
        var result = OsProcess.Run("resolvectl", "flush-caches");
        if (!result.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            && !result.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(result) ? "DNS caches flushed." : result;
        }

        return result;
    }

    [McpServerTool(Name = "get_firewall_rules"), Description("Fetches firewall rules. Windows: netsh advfirewall. Linux: nft list ruleset or iptables -L. Parameter: ruleName (Windows filter; optional).")]
    public static string GetFirewallRules(
        [Description("Filter by rule name on Windows (optional). Default is 'all'. Ignored on Linux.")] string ruleName = "all")
    {
#if WINDOWS_APIS
        if (OperatingSystem.IsWindows())
        {
            if (ruleName.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '-' && c != '_'))
                return "Invalid rule name. Only alphanumeric characters, spaces, dashes, and underscores are allowed.";
            return OsProcess.Run("netsh", $"advfirewall firewall show rule name=\"{ruleName}\" verbose");
        }
#endif
        var nft = OsProcess.Run("nft", "list ruleset");
        if (!nft.StartsWith("Error", StringComparison.OrdinalIgnoreCase)
            && !nft.StartsWith("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return nft;
        }

        return OsProcess.Run("iptables", "-L -n -v");
    }
}
