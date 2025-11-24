using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Net;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class NetworkTools
{
    [McpServerTool(Name = "get_network_interfaces")]
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

    [McpServerTool(Name = "get_active_tcp_connections")]
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

    [McpServerTool(Name = "ping_host")]
    public static string PingHost(
        [System.ComponentModel.DescriptionAttribute("The hostname or IP address to ping.")] string host)
    {
        try
        {
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

    [McpServerTool(Name = "dns_lookup")]
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

    [McpServerTool(Name = "check_port_open")]
    public static string CheckPortOpen(
        [System.ComponentModel.DescriptionAttribute("The hostname or IP.")] string host,
        [System.ComponentModel.DescriptionAttribute("The port number.")] int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var result = client.BeginConnect(host, port, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            if (!success) return $"Port {port} on {host} is closed or unreachable (timeout).";
            client.EndConnect(result);
            return $"Port {port} on {host} is OPEN.";
        }
        catch (Exception ex)
        {
            return $"Port {port} on {host} is CLOSED. Error: {ex.Message}";
        }
    }

    [McpServerTool(Name = "download_file")]
    public static string DownloadFile(
        [System.ComponentModel.DescriptionAttribute("The URL to download.")] string url,
        [System.ComponentModel.DescriptionAttribute("The local destination path.")] string destPath)
    {
        try
        {
            using var client = new HttpClient();
            var bytes = client.GetByteArrayAsync(url).Result;
            File.WriteAllBytes(destPath, bytes);
            return $"Successfully downloaded {bytes.Length} bytes to '{destPath}'.";
        }
        catch (Exception ex)
        {
            return $"Error downloading file: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_public_ip")]
    public static string GetPublicIp()
    {
        try
        {
            using var client = new HttpClient();
            return client.GetStringAsync("https://api.ipify.org").Result;
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
