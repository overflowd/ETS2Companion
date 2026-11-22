using System.Diagnostics;
using ETS2CompanionLauncher.Models;

namespace ETS2CompanionLauncher.Services;

public sealed record WatcherSnapshot(
    bool IsPaused,
    bool IsGameRunning,
    string? DetectedGameProcess,
    IReadOnlyDictionary<string, bool> AppRunningStates);

public sealed class GameWatcherService : IDisposable
{
    private readonly object _sync = new();
    private AppConfig _config;
    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _launchSessionCts;
    private Task? _loopTask;
    private bool _isPaused;
    private bool _wasGameRunning;
    private string? _lastDetectedGameProcess;
    private bool _disposed;

    public event Action<WatcherSnapshot>? StatusChanged;
    public event Action<string>? LogMessage;
    public event Action<string, string>? NotificationRequested;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _isPaused;
            }
        }
    }

    public GameWatcherService(AppConfig initialConfig)
    {
        _config = initialConfig;
    }

    public void UpdateConfig(AppConfig config)
    {
        lock (_sync)
        {
            _config = config;
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_loopTask is not null)
            {
                return;
            }

            // Capture initial game state so we know if ETS 2 was already open before launcher started.
            var initialGame = FindRunningGameProcess(_config);
            _wasGameRunning = initialGame is not null;
            _lastDetectedGameProcess = initialGame;

            if (_wasGameRunning)
            {
                Log($"ETS 2 ({initialGame}) zaten çalışıyor olarak algılandı.");
            }
            else
            {
                Log($"İzleme başlatıldı. Beklenen işlem: {_config.WatchedProcesses}");
            }

            _loopCts = new CancellationTokenSource();
            _loopTask = Task.Run(() => WatchLoopAsync(_loopCts.Token));
        }

        PublishSnapshot();
    }

    public void SetPaused(bool paused)
    {
        bool changed;
        lock (_sync)
        {
            changed = _isPaused != paused;
            _isPaused = paused;
            if (paused)
            {
                CancelActiveLaunchSession_NoLock();
            }
        }

        if (changed)
        {
            Log(paused ? "Otomatik izleme duraklatıldı." : "Otomatik izleme devam ediyor.");
            PublishSnapshot();
        }
    }

    public async Task LaunchEnabledAppsAsync(string reason, CancellationToken cancellationToken = default)
    {
        AppConfig config;
        lock (_sync)
        {
            config = _config;
        }

        var enabledApps = config.Apps.Where(a => a.Enabled).Select(a => a.Clone()).ToList();
        if (enabledApps.Count == 0)
        {
            Log($"{reason}: Etkinleştirilmiş uygulama yok.");
            return;
        }

        var validApps = enabledApps.Where(a => a.HasValidTarget()).ToList();
        var missingApps = enabledApps.Where(a => !a.HasValidTarget()).ToList();

        foreach (var missing in missingApps)
        {
            Log($"UYARI: '{missing.Name}' için geçerli uygulama yolu ayarlanmamış — atlandı.");
        }

        if (validApps.Count == 0)
        {
            if (config.ShowNotifications)
            {
                NotificationRequested?.Invoke(
                    "ETS 2 Algılandı",
                    "Başlatılacak uygulamaların dosya yolları ayarlanmamış. Lütfen pencereden 'Otomatik Bul' veya 'Düzenle' kullanın.");
            }
            return;
        }

        Log($"{reason}: {validApps.Count} uygulama başlatılıyor...");
        if (config.ShowNotifications)
        {
            var names = string.Join(", ", validApps.Select(a => a.Name));
            NotificationRequested?.Invoke("ETS 2 Başlatıcı", $"{reason}: {names}");
        }

        var tasks = validApps.Select(app => LaunchSingleAppWithDelayAsync(app, cancellationToken)).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        PublishSnapshot();
    }

    public static bool IsAppRunning(CompanionApp app)
    {
        var namesToCheck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var primary = app.GetEffectiveProcessName();
        if (!string.IsNullOrWhiteSpace(primary))
        {
            namesToCheck.Add(primary);
        }

        foreach (var hint in app.SearchHints)
        {
            var compact = hint.Replace(" ", string.Empty);
            if (!string.IsNullOrWhiteSpace(compact))
            {
                namesToCheck.Add(compact);
            }
        }

        foreach (var name in namesToCheck)
        {
            if (IsProcessNameRunning(name))
            {
                return true;
            }
        }

        return false;
    }

    private async Task WatchLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            int intervalMs;
            AppConfig config;
            bool paused;

            lock (_sync)
            {
                config = _config;
                paused = _isPaused;
                intervalMs = Math.Clamp(config.PollIntervalMs, 500, 30000);
            }

            try
            {
                var runningGame = FindRunningGameProcess(config);
                var isGameRunningNow = runningGame is not null;
                bool becameRunning = false;
                bool becameStopped = false;
                CancellationToken launchToken = CancellationToken.None;

                lock (_sync)
                {
                    if (!paused)
                    {
                        if (isGameRunningNow && !_wasGameRunning)
                        {
                            becameRunning = true;
                            CancelActiveLaunchSession_NoLock();
                            _launchSessionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                            launchToken = _launchSessionCts.Token;
                        }
                        else if (!isGameRunningNow && _wasGameRunning)
                        {
                            becameStopped = true;
                            CancelActiveLaunchSession_NoLock();
                        }
                    }

                    _wasGameRunning = isGameRunningNow;
                    _lastDetectedGameProcess = runningGame;
                }

                if (becameRunning)
                {
                    _ = Task.Run(() => LaunchEnabledAppsAsync($"ETS 2 ({runningGame}) açıldı", launchToken), ct);
                }
                else if (becameStopped)
                {
                    Log("ETS 2 kapandı.");
                    _ = Task.Run(() => CloseConfiguredAppsOnExitAsync(config), ct);
                }

                PublishSnapshot();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"İzleme hatası: {ex.Message}");
            }

            try
            {
                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task LaunchSingleAppWithDelayAsync(CompanionApp app, CancellationToken ct)
    {
        try
        {
            if (app.LaunchDelaySeconds > 0)
            {
                Log($"'{app.Name}' {app.LaunchDelaySeconds} saniye sonra başlatılacak...");
                await Task.Delay(TimeSpan.FromSeconds(app.LaunchDelaySeconds), ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested)
            {
                return;
            }

            if (IsAppRunning(app))
            {
                Log($"'{app.Name}' zaten çalışıyor — yeniden açılmadı.");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = app.ExecutablePath,
                Arguments = app.Arguments ?? string.Empty,
                UseShellExecute = true
            };

            if (!app.IsUriTarget)
            {
                var workingDir = Path.GetDirectoryName(app.ExecutablePath);
                if (!string.IsNullOrWhiteSpace(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }
            }

            if (app.RunAsAdmin && !app.IsUriTarget)
            {
                startInfo.Verb = "runas";
            }

            Process.Start(startInfo);
            Log($"'{app.Name}' başlatıldı.");
        }
        catch (OperationCanceledException)
        {
            Log($"'{app.Name}' başlatılması iptal edildi (ETS 2 kapandı veya izleme duraklatıldı).");
        }
        catch (Exception ex)
        {
            Log($"HATA: '{app.Name}' başlatılamadı — {ex.Message}");
        }
    }

    private async Task CloseConfiguredAppsOnExitAsync(AppConfig config)
    {
        var appsToClose = config.Apps
            .Where(a => a.Enabled && a.CloseOnGameExit)
            .Select(a => a.Clone())
            .ToList();

        if (appsToClose.Count == 0)
        {
            return;
        }

        foreach (var app in appsToClose)
        {
            var procName = app.GetEffectiveProcessName();
            if (string.IsNullOrWhiteSpace(procName))
            {
                continue;
            }

            var processes = Process.GetProcessesByName(procName);
            if (processes.Length == 0)
            {
                continue;
            }

            try
            {
                Log($"ETS 2 kapandığı için '{app.Name}' kapatılıyor...");
                foreach (var proc in processes)
                {
                    try
                    {
                        proc.CloseMainWindow();
                    }
                    catch
                    {
                        // Ignore close errors.
                    }
                }

                await Task.Delay(2500).ConfigureAwait(false);

                foreach (var proc in processes)
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Process may have exited or require elevation.
                    }
                }
            }
            finally
            {
                foreach (var proc in processes)
                {
                    proc.Dispose();
                }
            }
        }

        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        AppConfig config;
        bool paused;
        bool isGameRunning;
        string? detectedGame;

        lock (_sync)
        {
            config = _config;
            paused = _isPaused;
            isGameRunning = _wasGameRunning;
            detectedGame = _lastDetectedGameProcess;
        }

        var states = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in config.Apps)
        {
            states[app.Id] = IsAppRunning(app);
        }

        StatusChanged?.Invoke(new WatcherSnapshot(paused, isGameRunning, detectedGame, states));
    }

    private static string? FindRunningGameProcess(AppConfig config)
    {
        foreach (var procName in config.GetWatchedProcessList())
        {
            if (IsProcessNameRunning(procName))
            {
                return procName;
            }
        }

        return null;
    }

    private static bool IsProcessNameRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var proc in processes)
            {
                proc.Dispose();
            }
        }
    }

    private void CancelActiveLaunchSession_NoLock()
    {
        if (_launchSessionCts is not null)
        {
            try
            {
                _launchSessionCts.Cancel();
            }
            catch
            {
                // Ignore cancellation exceptions.
            }

            _launchSessionCts.Dispose();
            _launchSessionCts = null;
        }
    }

    private void Log(string message)
    {
        LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            CancelActiveLaunchSession_NoLock();
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;
        }
    }
}
