using System.Text.Json.Serialization;

namespace ETS2CompanionLauncher.Models;

public sealed class AppConfig
{
    /// <summary>
    /// Watch Euro Truck Simulator 2 (eurotrucks2.exe).
    /// </summary>
    public bool WatchEts2 { get; set; } = true;

    /// <summary>
    /// Watch American Truck Simulator (amtrucks.exe).
    /// </summary>
    public bool WatchAts { get; set; } = false;

    /// <summary>
    /// Optional comma-separated additional process names (without .exe) to watch.
    /// </summary>
    public string CustomWatchedProcesses { get; set; } = string.Empty;

    /// <summary>
    /// Legacy property kept for backward compatibility with older config files.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WatchedProcesses { get; set; }

    /// <summary>
    /// Polling interval in milliseconds.
    /// </summary>
    public int PollIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Show system tray notifications when the game starts and companion apps launch.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Minimize to tray instead of exiting when clicking the window close button.
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    public List<CompanionApp> Apps { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            WatchEts2 = true,
            WatchAts = false,
            CustomWatchedProcesses = string.Empty,
            PollIntervalMs = 2000,
            ShowNotifications = true,
            MinimizeToTrayOnClose = true,
            Apps = new List<CompanionApp>()
        };
    }

    public void MigrateLegacyFieldsIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(WatchedProcesses))
        {
            return;
        }

        var tokens = SplitProcessNames(WatchedProcesses);
        WatchEts2 = tokens.Any(t => t.Equals("eurotrucks2", StringComparison.OrdinalIgnoreCase));
        WatchAts = tokens.Any(t => t.Equals("amtrucks", StringComparison.OrdinalIgnoreCase));

        var extras = tokens
            .Where(t => !t.Equals("eurotrucks2", StringComparison.OrdinalIgnoreCase) &&
                        !t.Equals("amtrucks", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (extras.Length > 0 && string.IsNullOrWhiteSpace(CustomWatchedProcesses))
        {
            CustomWatchedProcesses = string.Join(", ", extras);
        }

        WatchedProcesses = null;
    }

    public string[] GetWatchedProcessList()
    {
        var list = new List<string>();
        if (WatchEts2)
        {
            list.Add("eurotrucks2");
        }

        if (WatchAts)
        {
            list.Add("amtrucks");
        }

        list.AddRange(SplitProcessNames(CustomWatchedProcesses));

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string GetWatchedSummaryLabel()
    {
        var parts = new List<string>();
        if (WatchEts2) parts.Add("ETS 2");
        if (WatchAts) parts.Add("ATS");

        var extras = SplitProcessNames(CustomWatchedProcesses);
        if (extras.Length > 0)
        {
            parts.AddRange(extras);
        }

        return parts.Count > 0 ? string.Join(" / ", parts) : "No Game Selected";
    }

    private static string[] SplitProcessNames(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Array.Empty<string>();
        }

        return input
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? p[..^4] : p)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

public sealed class CompanionApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public int LaunchDelaySeconds { get; set; } = 0;
    public bool CloseOnGameExit { get; set; } = false;
    public bool RunAsAdmin { get; set; } = false;
    public List<string> SearchHints { get; set; } = new();
    public string SteamAppId { get; set; } = string.Empty;

    public bool IsUriTarget =>
        ExecutablePath.StartsWith("steam://", StringComparison.OrdinalIgnoreCase) ||
        ExecutablePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        ExecutablePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public bool HasValidTarget()
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            return false;
        }

        if (IsUriTarget)
        {
            return true;
        }

        return File.Exists(ExecutablePath);
    }

    public string GetEffectiveProcessName()
    {
        if (!string.IsNullOrWhiteSpace(ProcessName))
        {
            return ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? ProcessName[..^4]
                : ProcessName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ExecutablePath) && !IsUriTarget)
        {
            return Path.GetFileNameWithoutExtension(ExecutablePath);
        }

        return string.Empty;
    }

    public CompanionApp Clone()
    {
        return new CompanionApp
        {
            Id = Id,
            Name = Name,
            Enabled = Enabled,
            ExecutablePath = ExecutablePath,
            Arguments = Arguments,
            ProcessName = ProcessName,
            LaunchDelaySeconds = LaunchDelaySeconds,
            CloseOnGameExit = CloseOnGameExit,
            RunAsAdmin = RunAsAdmin,
            SearchHints = new List<string>(SearchHints),
            SteamAppId = SteamAppId
        };
    }
}
