using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ETS2CompanionLauncher.Models;
using Microsoft.Win32;

namespace ETS2CompanionLauncher.Services;

public sealed record DetectionResult(string Path, string Source);

public static class AppAutoDetector
{
    private static readonly string[] ExcludedExeKeywords =
    {
        "unins", "uninstall", "setup", "installer", "update", "updater",
        "crash", "report", "redist", "vcredist", "dxsetup", "helper",
        "elevate", "squirrel", "createdump"
    };

    public static DetectionResult? Detect(CompanionApp app)
    {
        var hints = BuildSearchHints(app);
        if (hints.Count == 0 && string.IsNullOrWhiteSpace(app.SteamAppId))
        {
            return null;
        }

        // 1. Check currently running processes first (exact match if user already has the app open)
        var fromRunning = FindFromRunningProcesses(app, hints);
        if (fromRunning is not null)
        {
            return fromRunning;
        }

        // 2. Check Steam libraries (appmanifest + steamapps\common)
        var fromSteam = FindFromSteam(app, hints);
        if (fromSteam is not null)
        {
            return fromSteam;
        }

        // 3. Check Windows Uninstall registry keys
        var fromRegistry = FindFromUninstallRegistry(hints);
        if (fromRegistry is not null)
        {
            return fromRegistry;
        }

        // 4. Check Start Menu & Desktop shortcuts (.lnk and .url)
        var fromShortcuts = FindFromShortcuts(hints);
        if (fromShortcuts is not null)
        {
            return fromShortcuts;
        }

        // 5. Shallow scan of standard Windows installation folders
        var fromFolders = FindFromStandardFolders(hints);
        if (fromFolders is not null)
        {
            return fromFolders;
        }

        return null;
    }

    private static List<string> BuildSearchHints(CompanionApp app)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddHint(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var trimmed = value.Trim();
            set.Add(trimmed);

            var compact = trimmed.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);
            if (!string.IsNullOrWhiteSpace(compact))
            {
                set.Add(compact);
            }
        }

        AddHint(app.Name);
        AddHint(app.ProcessName);
        foreach (var hint in app.SearchHints)
        {
            AddHint(hint);
        }

        return set.ToList();
    }

    private static DetectionResult? FindFromRunningProcesses(CompanionApp app, List<string> hints)
    {
        var processes = Process.GetProcesses();
        try
        {
            foreach (var proc in processes)
            {
                try
                {
                    if (!MatchesAnyHint(proc.ProcessName, hints))
                    {
                        continue;
                    }

                    var path = proc.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return new DetectionResult(path, "Running process");
                    }
                }
                catch
                {
                    // Access denied or process exited; skip safely.
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

        return null;
    }

    public static List<string> GetSteamLibraryPaths()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steamRoot = GetSteamInstallPath();
        if (string.IsNullOrWhiteSpace(steamRoot) || !Directory.Exists(steamRoot))
        {
            return new List<string>();
        }

        libraries.Add(steamRoot);

        var vdfPath = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdfPath))
        {
            try
            {
                var content = File.ReadAllText(vdfPath);
                foreach (var parsed in ParseLibraryFoldersVdf(content))
                {
                    if (Directory.Exists(parsed))
                    {
                        libraries.Add(parsed);
                    }
                }
            }
            catch
            {
                // Ignore VDF parsing errors.
            }
        }

        return libraries.ToList();
    }

    public static List<string> ParseLibraryFoldersVdf(string content)
    {
        var result = new List<string>();
        var matches = Regex.Matches(content, "\"(?:path|\\d+)\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var raw = match.Groups[1].Value.Replace(@"\\", @"\").Replace('/', '\\').Trim();
            if (raw.Length >= 3 && char.IsLetter(raw[0]) && raw[1] == ':' && raw[2] == '\\')
            {
                result.Add(raw);
            }
        }

        return result;
    }

    private static string? GetSteamInstallPath()
    {
        var registryLocations = new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath")
        };

        foreach (var (root, subKey, valueName) in registryLocations)
        {
            try
            {
                using var key = root.OpenSubKey(subKey);
                var path = key?.GetValue(valueName) as string;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    var normalized = path.Replace('/', '\\').Trim();
                    if (Directory.Exists(normalized))
                    {
                        return normalized;
                    }
                }
            }
            catch
            {
                // Ignore registry read issues.
            }
        }

        var defaultPath = @"C:\Program Files (x86)\Steam";
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }

    private static DetectionResult? FindFromSteam(CompanionApp app, List<string> hints)
    {
        var libraries = GetSteamLibraryPaths();
        foreach (var library in libraries)
        {
            var steamAppsDir = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamAppsDir))
            {
                continue;
            }

            // First check exact AppID manifest if known
            if (!string.IsNullOrWhiteSpace(app.SteamAppId))
            {
                var manifestPath = Path.Combine(steamAppsDir, $"appmanifest_{app.SteamAppId.Trim()}.acf");
                var exe = TryFindExeFromAcfManifest(steamAppsDir, manifestPath, hints);
                if (exe is not null)
                {
                    return new DetectionResult(exe, "Steam library");
                }
            }

            // Scan all appmanifest_*.acf files by app name
            try
            {
                foreach (var acfFile in Directory.EnumerateFiles(steamAppsDir, "appmanifest_*.acf"))
                {
                    var (name, installDir) = ParseAcfManifest(acfFile);
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.IsNullOrWhiteSpace(installDir) &&
                        MatchesAnyHint(name, hints))
                    {
                        var folder = Path.Combine(steamAppsDir, "common", installDir);
                        var exe = FindBestExecutableInDirectory(folder, hints, maxDepth: 2);
                        if (exe is not null)
                        {
                            return new DetectionResult(exe, "Steam library");
                        }
                    }
                }
            }
            catch
            {
                // Ignore enumeration issues.
            }

            // Fallback: check steamapps\common folder names directly
            var commonDir = Path.Combine(steamAppsDir, "common");
            if (Directory.Exists(commonDir))
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(commonDir))
                    {
                        var folderName = Path.GetFileName(dir);
                        if (MatchesAnyHint(folderName, hints))
                        {
                            var exe = FindBestExecutableInDirectory(dir, hints, maxDepth: 2);
                            if (exe is not null)
                            {
                                return new DetectionResult(exe, "Steam library");
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore enumeration issues.
                }
            }
        }

        return null;
    }

    private static string? TryFindExeFromAcfManifest(string steamAppsDir, string manifestPath, List<string> hints)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var (_, installDir) = ParseAcfManifest(manifestPath);
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return null;
        }

        var folder = Path.Combine(steamAppsDir, "common", installDir);
        return FindBestExecutableInDirectory(folder, hints, maxDepth: 2);
    }

    public static (string? Name, string? InstallDir) ParseAcfManifest(string acfFilePath)
    {
        try
        {
            var content = File.ReadAllText(acfFilePath);
            var nameMatch = Regex.Match(content, "\"name\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            var dirMatch = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return (
                nameMatch.Success ? nameMatch.Groups[1].Value : null,
                dirMatch.Success ? dirMatch.Groups[1].Value : null
            );
        }
        catch
        {
            return (null, null);
        }
    }

    private static DetectionResult? FindFromUninstallRegistry(List<string> hints)
    {
        var roots = new[]
        {
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (root, subPath) in roots)
        {
            try
            {
                using var baseKey = root.OpenSubKey(subPath);
                if (baseKey is null)
                {
                    continue;
                }

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    try
                    {
                        using var appKey = baseKey.OpenSubKey(subKeyName);
                        var displayName = appKey?.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(displayName) || !MatchesAnyHint(displayName, hints))
                        {
                            continue;
                        }

                        var displayIcon = CleanIconPath(appKey?.GetValue("DisplayIcon") as string);
                        if (!string.IsNullOrWhiteSpace(displayIcon) &&
                            displayIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(displayIcon) &&
                            !IsExcludedExecutable(displayIcon))
                        {
                            return new DetectionResult(displayIcon, "Windows Registry");
                        }

                        var installLocation = (appKey?.GetValue("InstallLocation") as string)?.Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                        {
                            var exe = FindBestExecutableInDirectory(installLocation, hints, maxDepth: 2);
                            if (exe is not null)
                            {
                                return new DetectionResult(exe, "Windows Registry");
                            }
                        }
                    }
                    catch
                    {
                        // Skip inaccessible subkey.
                    }
                }
            }
            catch
            {
                // Skip inaccessible registry root.
            }
        }

        return null;
    }

    private static DetectionResult? FindFromShortcuts(List<string> hints)
    {
        var dirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }
        .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        string? steamUrlFallback = null;

        foreach (var dir in dirs)
        {
            foreach (var file in EnumerateFilesSafe(dir, maxDepth: 3))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!MatchesAnyHint(fileName, hints))
                {
                    continue;
                }

                var ext = Path.GetExtension(file);
                if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    var target = ShortcutResolver.ResolveTarget(file);
                    if (!string.IsNullOrWhiteSpace(target) &&
                        target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(target) &&
                        !IsExcludedExecutable(target))
                    {
                        return new DetectionResult(target, "Start Menu / Desktop shortcut");
                    }
                }
                else if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase) && steamUrlFallback is null)
                {
                    var url = ParseInternetShortcutUrl(file);
                    if (!string.IsNullOrWhiteSpace(url) && url.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
                    {
                        steamUrlFallback = url;
                    }
                }
            }
        }

        return steamUrlFallback is not null
            ? new DetectionResult(steamUrlFallback, "Steam shortcut")
            : null;
    }

    private static DetectionResult? FindFromStandardFolders(List<string> hints)
    {
        var baseFolders = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }
        .Where(d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d))
        .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var baseFolder in baseFolders)
        {
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(baseFolder))
                {
                    var folderName = Path.GetFileName(subDir);
                    if (!MatchesAnyHint(folderName, hints))
                    {
                        continue;
                    }

                    var exe = FindBestExecutableInDirectory(subDir, hints, maxDepth: 2);
                    if (exe is not null)
                    {
                        return new DetectionResult(exe, "Program folder");
                    }
                }
            }
            catch
            {
                // Ignore access errors.
            }
        }

        return null;
    }

    private static string? FindBestExecutableInDirectory(string directory, List<string> hints, int maxDepth)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var candidates = new List<(string Path, int Score)>();

        foreach (var file in EnumerateFilesSafe(directory, maxDepth))
        {
            if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsExcludedExecutable(file))
            {
                continue;
            }

            var fileName = Path.GetFileNameWithoutExtension(file);
            var score = 0;

            foreach (var hint in hints)
            {
                if (fileName.Equals(hint, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 100);
                }
                else if (NormalizeToken(fileName).Equals(NormalizeToken(hint), StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 90);
                }
                else if (fileName.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                         NormalizeToken(fileName).Contains(NormalizeToken(hint), StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 70);
                }
            }

            // Prefer shallower executables
            var relativeDepth = Path.GetRelativePath(directory, file).Count(c => c == Path.DirectorySeparatorChar);
            score += Math.Max(0, 15 - (relativeDepth * 5));

            candidates.Add((file, score));
        }

        return candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Path.Length)
            .Select(c => c.Path)
            .FirstOrDefault();
    }

    private static IEnumerable<string> EnumerateFilesSafe(string rootDirectory, int maxDepth)
    {
        var stack = new Stack<(string Dir, int Depth)>();
        stack.Push((rootDirectory, 0));

        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(dir))
                {
                    stack.Push((subDir, depth + 1));
                }
            }
            catch
            {
                // Ignore inaccessible subdirectories.
            }
        }
    }

    private static string? CleanIconPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return trimmed[1..endQuote];
            }
        }

        var commaIndex = trimmed.LastIndexOf(',');
        if (commaIndex > 0 && trimmed[..commaIndex].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..commaIndex].Trim();
        }

        return trimmed.Trim('"');
    }

    private static string? ParseInternetShortcutUrl(string urlFilePath)
    {
        try
        {
            foreach (var line in File.ReadLines(urlFilePath))
            {
                if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                {
                    return line[4..].Trim();
                }
            }
        }
        catch
        {
            // Ignore unreadable .url files.
        }

        return null;
    }

    private static bool IsExcludedExecutable(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return ExcludedExeKeywords.Any(k => fileName.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAnyHint(string text, List<string> hints)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalizedText = NormalizeToken(text);
        foreach (var hint in hints)
        {
            if (text.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedHint = NormalizeToken(hint);
            if (normalizedHint.Length >= 4 && normalizedText.Contains(normalizedHint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeToken(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        return sb.ToString();
    }

    private static class ShortcutResolver
    {
        public static string? ResolveTarget(string lnkPath)
        {
            try
            {
                var link = (IShellLinkW)new ShellLink();
                var file = (IPersistFile)link;
                file.Load(lnkPath, 0);

                var buffer = new StringBuilder(260);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                var result = buffer.ToString();
                return string.IsNullOrWhiteSpace(result) ? null : result;
            }
            catch
            {
                return null;
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}
