using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class NetworkTools
{
    private static readonly string[] SystemProtectedDirectories =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
    ];

    private static bool IsSystemProtectedPath(string fullPath)
    {
        foreach (var dir in SystemProtectedDirectories)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var normalized = dir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), dir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [McpServerTool(Name = "get_network_interfaces"), Description("Returns a list of network adapters with basic configuration. Includes IP addresses (IPv4), operational status, speed, type and DNS suffix. Read-only diagnostic information.")]
    public static List<NetworkInterfaceModel> GetNetworkInterfaces()
    {
        var results = new List<NetworkInterfaceModel>();
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();

        foreach (var ni in interfaces)
        {
            var ipProps = ni.GetIPProperties();
            var ipv4 = ipProps.UnicastAddresses
                .Where(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ua => ua.Address.ToString())
                .ToList();

            results.Add(new NetworkInterfaceModel
            {
                Id = ni.Id,
                Name = ni.Name,
                Description = ni.Description,
                Status = ni.OperationalStatus.ToString(),
                SpeedMbps = ni.Speed / 1000000,
                Type = ni.NetworkInterfaceType.ToString(),
                IPAddresses = ipv4,
                DnsSuffix = ipProps.DnsSuffix
            });
        }

        return results;
    }

    [McpServerTool(Name = "get_active_tcp_connections"), Description("Lists active TCP connections (local/remote endpoints and state). Parameter: count (default 20) to limit output. Read-only; may require elevated privileges to see all connections.")]
    public static List<TcpConnectionModel> GetActiveTcpConnections(
        [System.ComponentModel.DescriptionAttribute("Max number of connections to return. Default 20.")] int count = 20)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var connections = properties.GetActiveTcpConnections();

        return connections
            .Take(count)
            .Select(c => new TcpConnectionModel
            {
                LocalAddress = c.LocalEndPoint.Address.ToString(),
                LocalPort = c.LocalEndPoint.Port,
                RemoteAddress = c.RemoteEndPoint.Address.ToString(),
                RemotePort = c.RemoteEndPoint.Port,
                State = c.State.ToString()
            })
            .ToList();
    }

    [McpServerTool(Name = "ping_host"), Description("Sends an ICMP ping to a hostname or IP to check reachability and round-trip time. Parameter: host string. May be blocked by firewall or require permission. Example: host='github.com'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"host\":{\"type\":\"string\"}}}")]
    public static string PingHost(
        [System.ComponentModel.DescriptionAttribute("The hostname or IP address to ping.")] string host)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) return "Error: host parameter is required.";
            using var ping = new Ping();
            var reply = ping.Send(host);
            if (reply.Status == IPStatus.Success)
            {
                return $"Success: {reply.Address} time={reply.RoundtripTime}ms ttl={reply.Options?.Ttl}";
            }
            return $"Failed: {reply.Status}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "dns_lookup"), Description("Resolves a hostname to one or more IP addresses. Parameter: host. Uses system DNS resolver and may return IPv4/IPv6 addresses.")]
    public static string DnsLookup(
        [System.ComponentModel.DescriptionAttribute("The hostname to resolve.")] string host)
    {
        try
        {
            var entry = Dns.GetHostEntry(host);
            var ips = string.Join(", ", entry.AddressList.Select(a => a.ToString()));
            return $"Resolved '{host}' to: {ips}";
        }
        catch (Exception ex)
        {
            return $"Error resolving host: {ex.Message}";
        }
    }

    [McpServerTool(Name = "check_port_open"), Description("Attempts a TCP connection to the given host and port to determine if the port is open. Parameters: host, port. Uses a short timeout (2s). Non-destructive and safe to use for quick checks. Example: host='example.com', port=80. JSON input schema example: {\"type\":\"object\",\"properties\":{\"host\":{\"type\":\"string\"},\"port\":{\"type\":\"integer\"}}}")]
    public static async Task<string> CheckPortOpen(
        [System.ComponentModel.DescriptionAttribute("The hostname or IP.")] string host,
        [System.ComponentModel.DescriptionAttribute("The port number.")] int port)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) return "Error: host parameter is required.";
            if (port < 1 || port > 65535) return "Error: port must be between 1 and 65535.";
            using var client = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return $"Port {port} on {host} is OPEN.";
        }
        catch (OperationCanceledException)
        {
            return $"Port {port} on {host} is closed or unreachable (timeout).";
        }
        catch (Exception ex)
        {
            return $"Port {port} on {host} is CLOSED. Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "download_file"), Description("Downloads a file from a public URL and writes it to destPath. Parameters: url, destPath. Network I/O operation — ensure URL is trusted and dest path writable. Returns bytes downloaded or error message. Example: url='https://example.com/file.txt', destPath='C:\\temp\\file.txt'. JSON input schema example: {\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"destPath\":{\"type\":\"string\"}}}")]
    public static async Task<string> DownloadFile(
        [System.ComponentModel.DescriptionAttribute("The URL to download.")] string url,
        [System.ComponentModel.DescriptionAttribute("The local destination path.")] string destPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(destPath)) return "Error: url and destPath are required.";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return "Error: url must be a valid absolute HTTP/HTTPS URL.";
            if (!Path.IsPathRooted(destPath)) return "Error: destPath must be an absolute path.";
            if (IsSystemProtectedPath(Path.GetFullPath(destPath))) return $"Error: Writing to protected system path is blocked: '{destPath}'.";
            using var client = new HttpClient();
            var bytes = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            File.WriteAllBytes(destPath, bytes);
            return $"Successfully downloaded {bytes.Length} bytes to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error downloading file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_public_ip"), Description("Queries a public IP service to return the server's public-facing IPv4 address. No parameters. Network call may fail if outbound HTTP blocked.")]
    public static async Task<string> GetPublicIp()
    {
        try
        {
            using var client = new HttpClient();
            return await client.GetStringAsync("https://api.ipify.org").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return $"Error getting public IP: {ex.Message}";
        }
    }

    public class NetworkInterfaceModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long SpeedMbps { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<string> IPAddresses { get; set; } = new();
        public string DnsSuffix { get; set; } = string.Empty;
    }

    public class TcpConnectionModel
    {
        public string LocalAddress { get; set; } = string.Empty;
        public int LocalPort { get; set; }
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string State { get; set; } = string.Empty;
    }
}
