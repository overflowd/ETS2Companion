using Microsoft.Win32;

namespace ETS2CompanionLauncher.Services;

public static class StartupService
{
    private const string RunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "ETS2CompanionLauncher";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: false);
            var value = key?.GetValue(AppRegistryName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath);

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException("Uygulama yürütülebilir dosya yolu alınamadı.");
            }

            key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppRegistryName, throwOnMissingValue: false);
        }
    }
}
