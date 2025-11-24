using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Management;
using System.Security.Principal;
using System.DirectoryServices.AccountManagement;

namespace WinSysMcp.Tools;

[McpServerToolType]
public static class PowerTools
{
    [McpServerTool(Name = "get_battery_status")]
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
public static class SecurityTools
{
    [McpServerTool(Name = "get_current_user")]
    public static string GetCurrentUser()
    {
        return WindowsIdentity.GetCurrent().Name;
    }

    [McpServerTool(Name = "list_local_users")]
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

    [McpServerTool(Name = "list_local_groups")]
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

    [McpServerTool(Name = "check_user_in_group")]
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
