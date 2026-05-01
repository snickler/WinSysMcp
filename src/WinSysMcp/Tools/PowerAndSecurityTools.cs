using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Management;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;

namespace WinSysMcp.Tools;

[McpServerToolType]
public class PowerTools
{
    [McpServerTool(Name = "get_battery_status"), Description("Returns battery health and estimated charge remaining when present. No parameters. Read-only; may return 'No battery detected' on desktops.")]
    public static string GetBatteryStatus()
    {
        try
        {
            var scope = new ManagementScope("\\\\.\\root\\cimv2");
            scope.Connect();
            var query = new ObjectQuery("SELECT * FROM Win32_Battery");
            using var searcher = new ManagementObjectSearcher(scope, query);
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var status = obj["BatteryStatus"]?.ToString();
                var estimatedChargeRemaining = obj["EstimatedChargeRemaining"]?.ToString();
                return $"Battery Status: {status}, Charge: {estimatedChargeRemaining}%";
            }
            return "No battery detected.";
        }
        catch (Exception ex)
        {
            return $"Error getting battery status: {ex.Message}";
        }
    }
}

[McpServerToolType]
public class SecurityTools
{
    [McpServerTool(Name = "get_current_user"), Description("Returns the identity (DOMAIN\\username) of the account running the MCP server process. Use for debugging and auditing; read-only.")]
    public static string GetCurrentUser()
    {
        return WindowsIdentity.GetCurrent().Name;
    }

    [McpServerTool(Name = "list_local_users"), Description("Enumerates local user accounts on this machine. Returns an array of usernames. May require privileges and can return limited data under restricted contexts.")]
    public static List<string> ListLocalUsers()
    {
        var users = new List<string>();
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var searcher = new PrincipalSearcher(new UserPrincipal(context));
            foreach (var result in searcher.FindAll())
            {
                users.Add(result.Name);
            }
        }
        catch (Exception ex)
        {
            users.Add($"Error listing users: {ex.Message}");
        }
        return users;
    }

    [McpServerTool(Name = "list_local_groups"), Description("Enumerates local groups on this machine and returns group names. Read-only; useful for permission audits.")]
    public static List<string> ListLocalGroups()
    {
        var groups = new List<string>();
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var searcher = new PrincipalSearcher(new GroupPrincipal(context));
            foreach (var result in searcher.FindAll())
            {
                groups.Add(result.Name);
            }
        }
        catch (Exception ex)
        {
            groups.Add($"Error listing groups: {ex.Message}");
        }
        return groups;
    }

    [McpServerTool(Name = "check_user_in_group"), Description("Checks whether a user account is a member of a specified local group. Parameters: username, groupName. Read-only; helpful for access troubleshooting. JSON input schema example: {\"type\":\"object\",\"properties\":{\"username\":{\"type\":\"string\"},\"groupName\":{\"type\":\"string\"}}}")]
    public static string CheckUserInGroup(
        [System.ComponentModel.DescriptionAttribute("The username.")] string username,
        [System.ComponentModel.DescriptionAttribute("The group name.")] string groupName)
    {
        try
        {
            using var context = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(context, username);
            using var group = GroupPrincipal.FindByIdentity(context, groupName);

            if (user == null) return $"User '{username}' not found.";
            if (group == null) return $"Group '{groupName}' not found.";

            if (user.IsMemberOf(group))
            {
                return $"User '{username}' is a member of '{groupName}'.";
            }
            return $"User '{username}' is NOT a member of '{groupName}'.";
        }
        catch (Exception ex)
        {
            return $"Error checking membership: {ex.Message}";
        }
    }
}
