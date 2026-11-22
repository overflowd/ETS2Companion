using ETS2CompanionLauncher.Services;
using ETS2CompanionLauncher.UI;

namespace ETS2CompanionLauncher;

internal static class Program
{
    private const string MutexName = "ETS2CompanionLauncher_SingleInstance_Mutex";
    private const string ShowWindowEventName = "ETS2CompanionLauncher_ShowWindow_Event";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Signal the running instance to restore its window from the system tray.
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch
            {
                // Ignore if event isn't ready yet.
            }
            return;
        }

        ApplicationConfiguration.Initialize();

        var startMinimized = args.Any(a =>
            a.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
            a.Equals("/minimized", StringComparison.OrdinalIgnoreCase));

        var configService = new ConfigService();
        var config = configService.Load();

        using var showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        using var mainForm = new MainForm(configService, config, startMinimized);

        using var shutdownCts = new CancellationTokenSource();
        var ipcTask = Task.Run(() =>
        {
            var handles = new WaitHandle[] { showWindowEvent, shutdownCts.Token.WaitHandle };
            while (!shutdownCts.IsCancellationRequested)
            {
                var signaled = WaitHandle.WaitAny(handles);
                if (signaled == 0)
                {
                    mainForm.RestoreFromTray();
                }
                else
                {
                    break;
                }
            }
        });

        try
        {
            Application.Run(mainForm);
        }
        finally
        {
            shutdownCts.Cancel();
            try
            {
                ipcTask.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // Ignore shutdown wait issues.
            }
        }
    }
}
