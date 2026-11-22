namespace ETS2CompanionLauncher.Models;

public sealed class AppConfig
{
    /// <summary>
    /// Comma-separated process names (without .exe) to watch. Default: eurotrucks2
    /// </summary>
    public string WatchedProcesses { get; set; } = "eurotrucks2";

    /// <summary>
    /// Polling interval in milliseconds.
    /// </summary>
    public int PollIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Show system tray notifications when ETS 2 starts and apps launch.
    /// </summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>
    /// Minimize to tray instead of exiting when clicking the window close button.
    /// </summary>
    public bool MinimizeToTrayOnClose { get; set; } = true;

    /// <summary>
    /// Tracks whether initial auto-detection has already run at least once.
    /// </summary>
    public bool InitialAutoDetectCompleted { get; set; } = false;

    public List<CompanionApp> Apps { get; set; } = new();

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            WatchedProcesses = "eurotrucks2",
            PollIntervalMs = 2000,
            ShowNotifications = true,
            MinimizeToTrayOnClose = true,
            InitialAutoDetectCompleted = false,
            Apps = new List<CompanionApp>
            {
                new CompanionApp
                {
                    Id = "lossless-scaling",
                    Name = "Lossless Scaling",
                    Enabled = true,
                    ExecutablePath = string.Empty,
                    Arguments = string.Empty,
                    ProcessName = "LosslessScaling",
                    LaunchDelaySeconds = 3,
                    CloseOnGameExit = false,
                    RunAsAdmin = false,
                    SearchHints = new List<string> { "LosslessScaling", "Lossless Scaling" },
                    SteamAppId = "993090"
                },
                new CompanionApp
                {
                    Id = "lookpilot",
                    Name = "LookPilot",
                    Enabled = true,
                    ExecutablePath = string.Empty,
                    Arguments = string.Empty,
                    ProcessName = "LookPilot",
                    LaunchDelaySeconds = 1,
                    CloseOnGameExit = false,
                    RunAsAdmin = false,
                    SearchHints = new List<string> { "LookPilot", "lookpilot" },
                    SteamAppId = string.Empty
                },
                new CompanionApp
                {
                    Id = "westplay",
                    Name = "WestPlay",
                    Enabled = true,
                    ExecutablePath = string.Empty,
                    Arguments = string.Empty,
                    ProcessName = "WestPlay",
                    LaunchDelaySeconds = 1,
                    CloseOnGameExit = false,
                    RunAsAdmin = false,
                    SearchHints = new List<string> { "WestPlay", "Westplay", "westplay" },
                    SteamAppId = string.Empty
                }
            }
        };
    }

    public string[] GetWatchedProcessList()
    {
        return WatchedProcesses
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
