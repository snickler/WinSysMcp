# WinSysMcp — Tool Catalog

This document is generated from the MCP tool metadata in `src/WinSysMcp/Tools/`. Do not edit it manually; regenerate it with `tools/WinSysMcp.ToolSchemaGen`.

## Files

- `docs/TOOLS.md` — human-readable tool catalog generated from source metadata.
- `docs/TOOLS_SCHEMA.json` — machine-readable JSON catalog generated from the same metadata.
- `src/WinSysMcp/Tools/*` — C# source files implementing each MCP tool.
- `tools/WinSysMcp.ToolSchemaGen` — generator that refreshes both catalog files.

## Refresh generated docs

```powershell
dotnet run --project tools/WinSysMcp.ToolSchemaGen
```

## Safety notes

- Some tools are read-only diagnostics; others modify system state or files. Read each description carefully.
- File and registry tools include destructive operations. Use guarded or exact-match file edit tools when safety matters.
- The machine-readable schema in `docs/TOOLS_SCHEMA.json` is the best source for client-side validation and code generation.

## Tool groups

### `DiskTools` (`src/WinSysMcp/Tools/DiskTools.cs`)

- `get_drives` — Returns information about logical drives (name, type, total size, free space, format and readiness). Useful for capacity checks and diagnostics. Read-only and safe to call. Example: no parameters.
  - Parameters: none
- `get_folder_size` — Calculates total size of a folder and its subfolders. Parameter: path (absolute). May be slow on large hierarchies and requires read permission — returns size in MB/GB or an error message. Example: path='C:\Users\Public'. JSON input schema example: {"type":"object","properties":{"path":{"type":"string"}}}
  - Parameters: `path: string`

### `EventLogTools` (`src/WinSysMcp/Tools/EventLogTools.cs`)

- `get_event_logs` — Fetches recent log entries. On Windows: Event Log (Application/System/etc). On Linux: journalctl (logName maps to systemd unit or 'system'/'user'). Parameters: logName (default 'Application'/'system'), maxEvents, entryType. Read-only; may require privileges.
  - Parameters: `logName?: string = "Application"`, `maxEvents?: integer = 10`, `entryType?: string`

### `FileTools` (`src/WinSysMcp/Tools/FileTools.cs`)

- `apply_diff` — Applies edits from a diff payload to a text file. Parameters: path (absolute), diff, encoding (optional override). Supports unified diff hunks (@@) and JSON operation arrays. Write access to protected system locations is blocked.
  - Parameters: `path: string`, `diff: string`, `encoding?: string`
- `copy_file` — Copies a file from sourcePath to destPath. Parameters: sourcePath, destPath, overwrite (default false). Requires file system permissions; non-idempotent when overwrite=true. JSON input schema example: {"type":"object","properties":{"sourcePath":{"type":"string"},"destPath":{"type":"string"},"overwrite":{"type":"boolean"}}}
  - Parameters: `sourcePath: string`, `destPath: string`, `overwrite?: boolean = false`
- `create_directory` — Creates a directory and any missing parents. Parameter: path. Non-destructive if existing; requires write permission to the parent folder.
  - Parameters: `path: string`
- `delete_directory` — Deletes a directory. Parameters: path, recursive (default false). Destructive when recursive=true; use with caution and appropriate permissions. JSON input schema example: {"type":"object","properties":{"path":{"type":"string"},"recursive":{"type":"boolean"}}}
  - Parameters: `path: string`, `recursive?: boolean = false`
- `delete_exact_chunk` — Deletes an exact text chunk from a file and fails if the number of matches is not exactly what you expect. Parameters: path (absolute), text, expectedOccurrences (default 1), encoding (optional override). Useful when a line-based edit created duplicates and you want a safe cleanup.
  - Parameters: `path: string`, `text: string`, `expectedOccurrences?: integer = 1`, `encoding?: string`
- `delete_file` — Deletes a file at the given path. Parameter: path. Destructive operation — requires privileges. Returns a success or error message.
  - Parameters: `path: string`
- `delete_lines` — Deletes an inclusive line range in a text file. Parameters: path (absolute), startLine, endLine, encoding (optional override). Line numbers are 1-based and inclusive.
  - Parameters: `path: string`, `startLine: integer`, `endLine: integer`, `encoding?: string`
- `delete_lines_guarded` — Deletes an inclusive line range only when the current file content exactly matches the expected content for that range. Parameters: path (absolute), startLine, endLine, expectedContent, encoding (optional override). This is safer for cleanup of stale or duplicated edits because it refuses mismatched ranges.
  - Parameters: `path: string`, `startLine: integer`, `endLine: integer`, `expectedContent: string`, `encoding?: string`
- `get_file_hash` — Computes SHA256 hash of the file at path. Parameter: path. Read-only. Large files processed as streams — may be slow for very large files. JSON input schema example: {"type":"object","properties":{"path":{"type":"string"}}}
  - Parameters: `path: string`
- `get_file_info` — Returns metadata for a file (size, last write time, path). Parameter: path (absolute). Read-only and non-modifying.
  - Parameters: `path: string`
- `get_temp_path` — Returns the current process's temporary directory path. Read-only diagnostic value; may be useful for creating temp files or debugging.
  - Parameters: none
- `insert_at_line` — Inserts content at a line position in a text file. Parameters: path (absolute), lineNumber (1-based insertion point), content, encoding (optional override). lineNumber=1 prepends; values beyond end append.
  - Parameters: `path: string`, `lineNumber: integer`, `content: string`, `encoding?: string`
- `insert_at_line_guarded` — Inserts content at a line position only when the surrounding line anchors match the current file. Parameters: path (absolute), lineNumber (1-based insertion point), content, expectedBeforeLine (optional), expectedAfterLine (optional), encoding (optional override). At least one expected anchor must be provided. This is safer for LLM-driven edits because it refuses insertions when nearby lines drift.
  - Parameters: `path: string`, `lineNumber: integer`, `content: string`, `expectedBeforeLine?: string`, `expectedAfterLine?: string`, `encoding?: string`
- `list_directory` — Lists files and subdirectories inside a directory. Parameters: path (absolute). Returns entries (name, type, size and timestamps). Read-only; access may be restricted by file permissions.
  - Parameters: `path: string`
- `move_file` — Moves a file from sourcePath to destPath. Parameters: sourcePath, destPath. Requires permissions; operation is destructive to original path and may fail across volumes.
  - Parameters: `sourcePath: string`, `destPath: string`
- `read_file_head` — Reads the first N lines of a text file. Parameters: path (absolute), lineCount (default 20), encoding (optional override). Read-only; returns early with a friendly error message if file missing.
  - Parameters: `path: string`, `lineCount?: integer = 20`, `encoding?: string`
- `read_file_lines` — Reads a range of lines from a text file. Parameters: path (absolute), startLine (1-based, default 1), endLine (inclusive, default -1 for end of file), encoding (optional override). Returns up to 10,000 lines. Read-only.
  - Parameters: `path: string`, `startLine?: integer = 1`, `endLine?: integer = -1`, `encoding?: string`
- `read_file_tail` — Reads the last N lines of a text file. Parameters: path (absolute), lineCount (default 20), encoding (optional override). Returns newest lines. Large files are handled but may be slow; this is read-only. JSON input schema example: {"type":"object","properties":{"path":{"type":"string"},"lineCount":{"type":"integer"},"encoding":{"type":"string"}}}
  - Parameters: `path: string`, `lineCount?: integer = 20`, `encoding?: string`
- `replace_exact_chunk` — Replaces an exact text chunk in a file and fails if the number of matches is not exactly what you expect. Parameters: path (absolute), oldText, newText, expectedOccurrences (default 1), encoding (optional override). Safer for LLM-driven edits than line-number edits because it avoids editing the wrong location when line numbers drift.
  - Parameters: `path: string`, `oldText: string`, `newText: string`, `expectedOccurrences?: integer = 1`, `encoding?: string`
- `replace_lines` — Replaces an inclusive line range in a text file. Parameters: path (absolute), startLine, endLine, newContent, encoding (optional override). Line numbers are 1-based and inclusive.
  - Parameters: `path: string`, `startLine: integer`, `endLine: integer`, `newContent: string`, `encoding?: string`
- `replace_lines_guarded` — Replaces an inclusive line range only when the current file content exactly matches the expected content for that range. Parameters: path (absolute), startLine, endLine, expectedContent, newContent, encoding (optional override). This is safer for LLM-driven edits because it refuses stale or drifted line-number edits.
  - Parameters: `path: string`, `startLine: integer`, `endLine: integer`, `expectedContent: string`, `newContent: string`, `encoding?: string`
- `replace_text` — Replaces text in a file. Parameters: path (absolute), oldText, newText, replaceAll (default true), encoding (optional override). Returns replacement count. Write access to protected system locations is blocked.
  - Parameters: `path: string`, `oldText: string`, `newText: string`, `replaceAll?: boolean = true`, `encoding?: string`
- `search_files` — Searches for files matching searchPattern (e.g. '*.log') inside path. Parameters: path, searchPattern, recursive=false. Returns up to first 100 matches. Read-only.
  - Parameters: `path: string`, `searchPattern: string`, `recursive?: boolean = false`
- `write_file` — Writes text content to a file. Parameters: path (absolute), content, overwrite (default true), encoding name or code page (for example utf-8, utf-8-bom, utf-16, utf-16be, windows-1252, shift_jis). Creates or overwrites the target file. Write access to protected system locations is blocked.
  - Parameters: `path: string`, `content: string`, `overwrite?: boolean = true`, `encoding?: string = "utf-8"`

### `NetworkAdvancedTools` (`src/WinSysMcp/Tools/NetworkAdvancedTools.cs`)

- `flush_dns` — Flushes the DNS resolver cache. Windows: ipconfig /flushdns. Linux: resolvectl flush-caches.
  - Parameters: none
- `get_arp_table` — Returns the system ARP/neighbor table. Windows: arp -a. Linux: ip neigh. Read-only.
  - Parameters: none
- `get_dns_cache` — Shows local DNS resolver cache/status. Windows: ipconfig /displaydns. Linux: resolvectl statistics (cache dump is often unavailable).
  - Parameters: none
- `get_firewall_rules` — Fetches firewall rules. Windows: netsh advfirewall. Linux: nft list ruleset or iptables -L. Parameter: ruleName (Windows filter; optional).
  - Parameters: `ruleName?: string = "all"`
- `get_route_table` — Returns the system routing table. Windows: route print. Linux: ip route. Read-only.
  - Parameters: none

### `NetworkTools` (`src/WinSysMcp/Tools/NetworkTools.cs`)

- `check_port_open` — Attempts a TCP connection to the given host and port to determine if the port is open. Parameters: host, port. Uses a short timeout (2s). Non-destructive and safe to use for quick checks. Example: host='example.com', port=80. JSON input schema example: {"type":"object","properties":{"host":{"type":"string"},"port":{"type":"integer"}}}
  - Parameters: `host: string`, `port: integer`
- `dns_lookup` — Resolves a hostname to one or more IP addresses. Parameter: host. Uses system DNS resolver and may return IPv4/IPv6 addresses.
  - Parameters: `host: string`
- `download_file` — Downloads a file from a public URL and writes it to destPath. Parameters: url, destPath. Network I/O operation — ensure URL is trusted and dest path writable. Returns bytes downloaded or error message. Example: url='https://example.com/file.txt', destPath='C:\temp\file.txt'. JSON input schema example: {"type":"object","properties":{"url":{"type":"string"},"destPath":{"type":"string"}}}
  - Parameters: `url: string`, `destPath: string`
- `get_active_tcp_connections` — Lists active TCP connections (local/remote endpoints and state). Parameter: count (default 20) to limit output. Read-only; may require elevated privileges to see all connections.
  - Parameters: `count?: integer = 20`
- `get_network_interfaces` — Returns a list of network adapters with basic configuration. Includes IP addresses (IPv4), operational status, speed, type and DNS suffix. Read-only diagnostic information.
  - Parameters: none
- `get_public_ip` — Queries a public IP service to return the server's public-facing IPv4 address. No parameters. Network call may fail if outbound HTTP blocked.
  - Parameters: none
- `ping_host` — Sends an ICMP ping to a hostname or IP to check reachability and round-trip time. Parameter: host string. May be blocked by firewall or require permission. Example: host='github.com'. JSON input schema example: {"type":"object","properties":{"host":{"type":"string"}}}
  - Parameters: `host: string`

### `PerformanceTools` (`src/WinSysMcp/Tools/PerformanceTools.cs`)

- `get_system_metrics` — Returns real-time system metrics (CPU percentage, available memory MB, system up time seconds). Uses Performance Counters on Windows and /proc on Linux.
  - Parameters: none

### `PowerAndSecurityTools` (`src/WinSysMcp/Tools/PowerAndSecurityTools.cs`)

- `check_user_in_group` — Checks whether a user account is a member of a specified local group. Parameters: username, groupName. Read-only; helpful for access troubleshooting. JSON input schema example: {"type":"object","properties":{"username":{"type":"string"},"groupName":{"type":"string"}}}
  - Parameters: `username: string`, `groupName: string`
- `get_battery_status` — Returns battery health and estimated charge remaining when present. No parameters. Read-only; may return 'No battery detected' on desktops.
  - Parameters: none
- `get_current_user` — Returns the identity (DOMAIN\username) of the account running the MCP server process. Use for debugging and auditing; read-only.
  - Parameters: none
- `list_local_groups` — Enumerates local groups on this machine and returns group names. Read-only; useful for permission audits.
  - Parameters: none
- `list_local_users` — Enumerates local user accounts on this machine. Returns an array of usernames. May require privileges and can return limited data under restricted contexts.
  - Parameters: none

### `ProcessTools` (`src/WinSysMcp/Tools/ProcessTools.cs`)

- `get_process_details` — Returns detailed metadata for a process by PID: name, memory details, start time, module path when accessible. Parameter: processId. Read-only; may fail on protected processes. Example: processId=1234. JSON input schema example: {"type":"object","properties":{"processId":{"type":"integer"}}}
  - Parameters: `processId: integer`
- `get_top_processes` — Returns the top N running processes sorted by memory (RSS). Parameter: count (default 10). Read-only; may skip system processes due to access restrictions. Example: count=5. JSON input schema example: {"type":"object","properties":{"count":{"type":"integer"}}}
  - Parameters: `count?: integer = 10`
- `kill_process` — Terminates a process by PID. Parameter: processId. Destructive and requires permissions; can fail for protected or system processes. Use cautiously. Example: processId=1234. JSON input schema example: {"type":"object","properties":{"processId":{"type":"integer"}}}
  - Parameters: `processId: integer`
- `start_process` — Starts a new process with the given executable path and optional arguments. Parameters: fileName, arguments (optional). Use caution launching untrusted executables; process runs under server user account. Example: fileName='C:\Program Files\MyApp\app.exe', arguments='--verbose'. JSON input schema example: {"type":"object","properties":{"fileName":{"type":"string"},"arguments":{"type":"string"}}}
  - Parameters: `fileName: string`, `arguments?: string = ""`

### `RegistryTools` (`src/WinSysMcp/Tools/RegistryTools.cs`)

- `delete_registry_value` — Deletes a registry value. Parameters: root, keyPath, valueName. Destructive — use with care and expect permission errors without elevation. Example: root='HKCU', keyPath='SOFTWARE\MyApp', valueName='Setting'. JSON input schema example: {"type":"object","properties":{"root":{"type":"string"},"keyPath":{"type":"string"},"valueName":{"type":"string"}}}
  - Parameters: `root: string`, `keyPath: string`, `valueName: string`
- `list_registry_keys` — Lists subkeys under a registry key. Parameters: root, keyPath. Read-only listing; will return helpful messages when keys are missing or inaccessible. Example: root='HKLM', keyPath='SOFTWARE'. JSON input schema example: {"type":"object","properties":{"root":{"type":"string"},"keyPath":{"type":"string"}}}
  - Parameters: `root: string`, `keyPath: string`
- `read_registry_value` — Reads a value from the Windows Registry. Parameters: root (HKLM/HKCU/etc), keyPath, valueName. Read-only — returns the value or an explanatory error message. Example: root='HKLM', keyPath='SOFTWARE\MyApp', valueName='InstallPath'. JSON input schema example: {"type":"object","properties":{"root":{"type":"string"},"keyPath":{"type":"string"},"valueName":{"type":"string"}}}
  - Parameters: `root: string`, `keyPath: string`, `valueName: string`
- `write_registry_value` — Writes a value to the Windows Registry. Parameters: root, keyPath, valueName, valueData. Destructive operation — modifies system configuration and requires appropriate privileges. Use carefully. Example: root='HKLM', keyPath='SOFTWARE\MyApp', valueName='Setting', valueData='1'. JSON input schema example: {"type":"object","properties":{"root":{"type":"string"},"keyPath":{"type":"string"},"valueName":{"type":"string"},"valueData":{"type":"string"}}}
  - Parameters: `root: string`, `keyPath: string`, `valueName: string`, `valueData: string`

### `ReliabilityTools` (`src/WinSysMcp/Tools/ReliabilityTools.cs`)

- `get_reliability_records` — Retrieves Windows reliability records (Win32_ReliabilityRecords) such as crashes/failures. Parameter: maxEvents (default 10) to limit results. Requires WMI and often admin rights. Example: maxEvents=50. JSON input schema example: {"type":"object","properties":{"maxEvents":{"type":"integer"}}}
  - Parameters: `maxEvents?: integer = 10`

### `ServiceTools` (`src/WinSysMcp/Tools/ServiceTools.cs`)

- `get_service_details` — Returns detailed data for a service (Windows or systemd unit). Parameter: serviceName. Read-only; may require elevation. Example: serviceName='wuauserv' or 'ssh.service'.
  - Parameters: `serviceName: string`
- `list_services` — Lists system services (Windows Service Controller or systemd units on Linux) with name, display name, and status. Optional filters: status and nameFilter. Read-only overview; may require elevated privileges. JSON input schema example: {"type":"object","properties":{"status":{"type":"string"},"nameFilter":{"type":"string"}}}
  - Parameters: `status?: string`, `nameFilter?: string`
- `start_service` — Attempts to start a service (Windows SCM or systemctl start). Requires privilege. Example: serviceName='Spooler' or 'nginx.service'.
  - Parameters: `serviceName: string`
- `stop_service` — Attempts to stop a service (Windows SCM or systemctl stop). Requires privilege. Example: serviceName='Spooler' or 'nginx.service'.
  - Parameters: `serviceName: string`

### `SoftwareTools` (`src/WinSysMcp/Tools/SoftwareTools.cs`)

- `get_installed_programs` — Lists installed programs. On Windows: Add/Remove Programs registry. On Linux: dpkg/rpm package database. Parameter: nameFilter (optional). Read-only.
  - Parameters: `nameFilter?: string`

### `SystemTools` (`src/WinSysMcp/Tools/SystemTools.cs`)

- `abort_shutdown` — Attempts to cancel a pending shutdown or restart. Windows: shutdown /a. Linux: shutdown -c.
  - Parameters: none
- `echo_message` — Echoes back the provided text. Use for connection and health checks. Parameter: message — string returned verbatim. Example: message='ping'. JSON input schema example: {"type":"object","properties":{"message":{"type":"string"}}}
  - Parameters: `message: string`
- `get_environment_variables` — Returns all environment variables visible to the MCP process as a dictionary (key => value). WARNING: may contain sensitive values (API keys, secrets) — treat output carefully.
  - Parameters: none
- `get_os_version` — Reports detailed OS version information and whether the OS is 32-bit or 64-bit. Useful for troubleshooting and compatibility checks.
  - Parameters: none
- `get_startup_apps` — Lists applications configured to start automatically. Windows: Registry Run keys. Linux: ~/.config/autostart and enabled systemd user units. Read-only and safe.
  - Parameters: none
- `get_system_info` — Returns key read-only system diagnostics: OS description, machine name, .NET runtime version and architecture. Safe to call — useful for diagnostics. Example: no parameters.
  - Parameters: none
- `get_uptime` — Returns system uptime (time since last boot) as a human-readable string. Read-only diagnostic information.
  - Parameters: none
- `lock_workstation` — Locks the currently logged-in user session immediately. Windows: LockWorkStation. Linux: loginctl lock-session. May not work from non-interactive services.
  - Parameters: none
- `restart_computer` — Schedules a system restart. Parameters: delay (seconds, default 30) and comment. Requires privileges; this will reboot the machine.
  - Parameters: `delay?: integer = 30`, `comment?: string = "Restart initiated by MCP."`
- `shutdown_computer` — Schedules a system shutdown. Parameters: delay (seconds, default 30) and comment. Requires privileges; operation is destructive.
  - Parameters: `delay?: integer = 30`, `comment?: string = "Shutdown initiated by MCP."`

### `TaskSchedulerTools` (`src/WinSysMcp/Tools/TaskSchedulerTools.cs`)

- `get_scheduled_tasks` — Retrieves scheduled tasks. Windows: schtasks /query. Linux: systemctl list-timers plus user crontab. Parameter: format (TABLE|LIST|CSV on Windows; ignored on Linux).
  - Parameters: `format?: string = "CSV"`

### `WmiTools` (`src/WinSysMcp/Tools/WmiTools.cs`)

- `get_bios_info` — Retrieves BIOS/firmware information via WMI (Win32_BIOS). Returns manufacturer, name, serial and version where available. Read-only and may require WMI availability.
  - Parameters: none
- `get_printer_info` — Lists installed printers using WMI (Win32_Printer). Returns driver, port and status where available. Read-only; information depends on WMI access.
  - Parameters: none
- `get_processor_info` — Retrieves CPU/processor information via WMI (Win32_Processor) — e.g. name, manufacturer, cores and logical processors. Read-only diagnostic info.
  - Parameters: none
- `get_sound_devices` — Enumerates sound devices via WMI (Win32_SoundDevice). Helpful for diagnosing audio hardware; read-only.
  - Parameters: none
- `get_startup_commands` — Retrieves startup commands using WMI (Win32_StartupCommand). Returns name, command, location and user context. Read-only and may include duplicates from other startup sources.
  - Parameters: none
- `get_video_controllers` — Returns video controller (GPU) info via WMI (Win32_VideoController) including name, memory and driver version when available. Read-only diagnostics.
  - Parameters: none

---
Generated from source metadata by `tools/WinSysMcp.ToolSchemaGen`.
