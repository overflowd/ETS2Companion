using System.Drawing;
using ETS2CompanionLauncher.Models;
using ETS2CompanionLauncher.Services;

namespace ETS2CompanionLauncher.UI;

public sealed class MainForm : Form
{
    private static readonly Color BgDark = Color.FromArgb(18, 21, 28);
    private static readonly Color SurfaceDark = Color.FromArgb(27, 32, 43);
    private static readonly Color ElevatedDark = Color.FromArgb(36, 43, 56);
    private static readonly Color BorderDark = Color.FromArgb(46, 55, 72);
    private static readonly Color TextPrimary = Color.FromArgb(241, 245, 249);
    private static readonly Color TextMuted = Color.FromArgb(148, 163, 184);
    private static readonly Color AccentBlue = Color.FromArgb(56, 189, 248);
    private static readonly Color SuccessGreen = Color.FromArgb(34, 197, 94);
    private static readonly Color WarningAmber = Color.FromArgb(245, 158, 11);
    private static readonly Color DangerRed = Color.FromArgb(239, 68, 68);

    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly GameWatcherService _watcher;
    private readonly bool _startMinimized;

    private readonly Icon _watchingIcon;
    private readonly Icon _runningIcon;
    private readonly Icon _pausedIcon;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _trayPauseItem;
    private readonly Label _statusBadge;
    private readonly DataGridView _appsGrid;
    private readonly TextBox _logBox;
    private readonly CheckBox _startupCheck;
    private readonly CheckBox _notifyCheck;
    private readonly CheckBox _pauseCheck;
    private readonly TextBox _watchedProcessBox;
    private readonly Button _autoDetectBtn;
    private readonly Button _launchNowBtn;

    private WatcherSnapshot? _latestSnapshot;
    private bool _allowExit;
    private bool _hasShownMinimizeTip;
    private bool _suppressUiEvents;

    public MainForm(ConfigService configService, AppConfig config, bool startMinimized)
    {
        _configService = configService;
        _config = config;
        _startMinimized = startMinimized;

        _watchingIcon = IconFactory.CreateAppIcon(TrayIconState.Watching);
        _runningIcon = IconFactory.CreateAppIcon(TrayIconState.GameRunning);
        _pausedIcon = IconFactory.CreateAppIcon(TrayIconState.Paused);

        Text = "ETS 2 Companion Launcher";
        Icon = _watchingIcon;
        MinimumSize = new Size(860, 610);
        Size = new Size(920, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgDark;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f);

        // --- System Tray ---
        var trayMenu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open Window", null, (_, _) => RestoreFromTray());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        var launchNowTrayItem = new ToolStripMenuItem("Launch Apps Now", null, async (_, _) => await RunManualLaunchAsync());
        var detectTrayItem = new ToolStripMenuItem("Auto-Detect Apps", null, async (_, _) => await RunAutoDetectAsync(forceRescan: false));
        _trayPauseItem = new ToolStripMenuItem("Pause Watching", null, (_, _) => TogglePause());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        trayMenu.Items.AddRange(new ToolStripItem[]
        {
            openItem,
            launchNowTrayItem,
            detectTrayItem,
            new ToolStripSeparator(),
            _trayPauseItem,
            new ToolStripSeparator(),
            exitItem
        });

        _trayIcon = new NotifyIcon
        {
            Icon = _watchingIcon,
            Text = "ETS 2 Companion Launcher — Waiting for ETS 2",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        // --- Layout ---
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
            BackColor = BgDark
        };
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));   // Header
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));   // Toolbar
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));    // Grid
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));   // Settings
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));    // Log
        Controls.Add(rootLayout);

        // 1. Header
        var headerPanel = new Panel
        {
            Size = new Size(880, 68),
            Dock = DockStyle.Fill,
            BackColor = SurfaceDark,
            Padding = new Padding(16, 10, 16, 10),
            Margin = new Padding(0, 0, 0, 10)
        };

        var titleLabel = new Label
        {
            Text = "ETS 2 Companion Launcher",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(16, 8)
        };

        var subtitleLabel = new Label
        {
            Text = "Automatically launches your companion apps when Euro Truck Simulator 2 starts.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = TextMuted,
            AutoSize = true,
            Location = new Point(17, 34)
        };

        _statusBadge = new Label
        {
            Text = "●  Waiting for ETS 2",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = AccentBlue,
            BackColor = ElevatedDark,
            AutoSize = false,
            Size = new Size(195, 32),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(headerPanel.Width - 211, 14)
        };

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(subtitleLabel);
        headerPanel.Controls.Add(_statusBadge);
        rootLayout.Controls.Add(headerPanel, 0, 0);

        // 2. Action Toolbar
        var toolbarPanel = new Panel
        {
            Size = new Size(880, 46),
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            Margin = new Padding(0, 0, 0, 6)
        };

        _autoDetectBtn = CreateActionButton("Auto-Detect", 0, 4, 120, ElevatedDark, AccentBlue);
        _autoDetectBtn.Click += async (_, _) => await RunAutoDetectAsync(forceRescan: true);

        var editBtn = CreateActionButton("Edit", 128, 4, 86, SurfaceDark, TextPrimary);
        editBtn.Click += (_, _) => EditSelectedApp();

        var addBtn = CreateActionButton("Add", 222, 4, 80, SurfaceDark, TextPrimary);
        addBtn.Click += (_, _) => AddNewApp();

        var removeBtn = CreateActionButton("Remove", 310, 4, 84, SurfaceDark, DangerRed);
        removeBtn.Click += (_, _) => DeleteSelectedApp();

        _launchNowBtn = CreateActionButton("Launch Now (Test)", toolbarPanel.Width - 165, 4, 165, AccentBlue, Color.FromArgb(15, 23, 42));
        _launchNowBtn.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        _launchNowBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _launchNowBtn.Click += async (_, _) => await RunManualLaunchAsync();

        toolbarPanel.Controls.AddRange(new Control[] { _autoDetectBtn, editBtn, addBtn, removeBtn, _launchNowBtn });
        rootLayout.Controls.Add(toolbarPanel, 0, 1);

        // 3. Apps Grid
        _appsGrid = CreateAppsGrid();
        _appsGrid.CellContentClick += AppsGrid_CellContentClick;
        _appsGrid.CellValueChanged += AppsGrid_CellValueChanged;
        _appsGrid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                EditSelectedApp();
            }
        };
        rootLayout.Controls.Add(_appsGrid, 0, 2);

        // 4. Settings Panel
        var settingsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceDark,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 10, 0, 10)
        };

        _startupCheck = new CheckBox
        {
            Text = "Start minimized with Windows",
            Location = new Point(14, 12),
            AutoSize = true,
            ForeColor = TextPrimary,
            Checked = StartupService.IsEnabled()
        };
        _startupCheck.CheckedChanged += StartupCheck_CheckedChanged;

        _notifyCheck = new CheckBox
        {
            Text = "Show tray notifications",
            Location = new Point(260, 12),
            AutoSize = true,
            ForeColor = TextPrimary,
            Checked = _config.ShowNotifications
        };
        _notifyCheck.CheckedChanged += (_, _) =>
        {
            if (_suppressUiEvents) return;
            _config.ShowNotifications = _notifyCheck.Checked;
            SaveConfig();
        };

        _pauseCheck = new CheckBox
        {
            Text = "Pause watching",
            Location = new Point(460, 12),
            AutoSize = true,
            ForeColor = WarningAmber
        };
        _pauseCheck.CheckedChanged += (_, _) =>
        {
            if (_suppressUiEvents) return;
            _watcher?.SetPaused(_pauseCheck.Checked);
        };

        var watchLabel = new Label
        {
            Text = "Watched game process(es):",
            Location = new Point(14, 44),
            AutoSize = true,
            ForeColor = TextMuted
        };

        _watchedProcessBox = new TextBox
        {
            Text = _config.WatchedProcesses,
            Location = new Point(195, 41),
            Size = new Size(260, 24),
            BackColor = ElevatedDark,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var saveProcessBtn = CreateActionButton("Apply", 463, 39, 80, ElevatedDark, AccentBlue);
        saveProcessBtn.Height = 26;
        saveProcessBtn.Click += SaveWatchedProcess_Click;

        var configHint = new Label
        {
            Text = "Comma-separated, e.g. eurotrucks2, amtrucks",
            Location = new Point(553, 44),
            AutoSize = true,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.5f)
        };

        settingsPanel.Controls.AddRange(new Control[]
        {
            _startupCheck, _notifyCheck, _pauseCheck,
            watchLabel, _watchedProcessBox, saveProcessBtn, configHint
        });
        rootLayout.Controls.Add(settingsPanel, 0, 3);

        // 5. Log Panel
        var logContainer = new Panel
        {
            Size = new Size(880, 160),
            Dock = DockStyle.Fill,
            BackColor = SurfaceDark,
            Padding = new Padding(12, 8, 12, 10)
        };

        var logHeader = new Label
        {
            Text = "Activity Log",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold)
        };

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(14, 17, 23),
            ForeColor = Color.FromArgb(203, 213, 225),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f)
        };

        var clearLogBtn = new LinkLabel
        {
            Text = "Clear",
            AutoSize = true,
            LinkColor = AccentBlue,
            ActiveLinkColor = TextPrimary,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(logContainer.Width - 54, 8)
        };
        clearLogBtn.Click += (_, _) => _logBox.Clear();

        logContainer.Controls.Add(_logBox);
        logContainer.Controls.Add(clearLogBtn);
        logContainer.Controls.Add(logHeader);
        clearLogBtn.BringToFront();
        rootLayout.Controls.Add(logContainer, 0, 4);

        // Initialize Watcher
        _watcher = new GameWatcherService(_config);
        _watcher.StatusChanged += OnWatcherStatusChanged;
        _watcher.LogMessage += AppendLog;
        _watcher.NotificationRequested += ShowBalloon;

        RefreshGrid();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_startMinimized)
        {
            HideToTray(showTip: false);
        }

        _watcher.Start();

        // Automatically search for missing paths on first launch
        if (!_config.InitialAutoDetectCompleted || _config.Apps.Any(a => string.IsNullOrWhiteSpace(a.ExecutablePath)))
        {
            await RunAutoDetectAsync(forceRescan: false);
            _config.InitialAutoDetectCompleted = true;
            SaveConfig();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_allowExit && _config.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray(showTip: !_hasShownMinimizeTip);
            _hasShownMinimizeTip = true;
            return;
        }

        _watcher.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _watchingIcon.Dispose();
        _runningIcon.Dispose();
        _pausedIcon.Dispose();
        base.OnFormClosing(e);
    }

    public void RestoreFromTray()
    {
        if (InvokeRequired)
        {
            BeginInvoke(RestoreFromTray);
            return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void HideToTray(bool showTip)
    {
        ShowInTaskbar = false;
        Hide();

        if (showTip && _config.ShowNotifications)
        {
            _trayIcon.ShowBalloonTip(
                3000,
                "ETS 2 Companion Launcher",
                "Still running in the system tray. Right-click the tray icon and select Exit to close completely.",
                ToolTipIcon.Info);
        }
    }

    private void ExitApplication()
    {
        _allowExit = true;
        Close();
    }

    private void TogglePause()
    {
        _watcher.SetPaused(!_watcher.IsPaused);
    }

    private async Task RunAutoDetectAsync(bool forceRescan)
    {
        _autoDetectBtn.Enabled = false;
        _autoDetectBtn.Text = "Scanning...";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Scanning for installed companion apps...");

        try
        {
            var targets = _config.Apps
                .Where(a => forceRescan || !a.HasValidTarget())
                .ToList();

            var foundCount = 0;
            var missingNames = new List<string>();

            await Task.Run(() =>
            {
                foreach (var app in targets)
                {
                    if (!forceRescan && app.HasValidTarget())
                    {
                        continue;
                    }

                    var result = AppAutoDetector.Detect(app);
                    if (result is not null)
                    {
                        app.ExecutablePath = result.Path;
                        if (result.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            app.ProcessName = Path.GetFileNameWithoutExtension(result.Path);
                        }

                        foundCount++;
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] Found '{app.Name}' ({result.Source}): {result.Path}");
                    }
                    else
                    {
                        missingNames.Add(app.Name);
                    }
                }
            });

            SaveConfig();
            RefreshGrid();

            if (foundCount > 0)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Auto-detection completed: {foundCount} app(s) found.");
            }

            if (missingNames.Count > 0)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Not found: {string.Join(", ", missingNames)}. Select a row and click 'Edit' to browse for its .exe manually.");
            }
        }
        finally
        {
            _autoDetectBtn.Enabled = true;
            _autoDetectBtn.Text = "Auto-Detect";
        }
    }

    private async Task RunManualLaunchAsync()
    {
        _launchNowBtn.Enabled = false;
        try
        {
            await _watcher.LaunchEnabledAppsAsync("Manual launch");
        }
        finally
        {
            if (!IsDisposed)
            {
                _launchNowBtn.Enabled = true;
            }
        }
    }

    private void AddNewApp()
    {
        var newApp = new CompanionApp
        {
            Name = string.Empty,
            Enabled = true,
            LaunchDelaySeconds = 1
        };

        using var dialog = new AppEditDialog(newApp, isNew: true);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _config.Apps.Add(dialog.ResultApp);
            SaveConfig();
            RefreshGrid();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Added '{dialog.ResultApp.Name}'.");
        }
    }

    private void EditSelectedApp()
    {
        var selected = GetSelectedApp();
        if (selected is null)
        {
            MessageBox.Show(this, "Please select an application from the list to edit.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new AppEditDialog(selected, isNew: false);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var index = _config.Apps.FindIndex(a => a.Id == selected.Id);
            if (index >= 0)
            {
                _config.Apps[index] = dialog.ResultApp;
                SaveConfig();
                RefreshGrid();
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Updated settings for '{dialog.ResultApp.Name}'.");
            }
        }
    }

    private void DeleteSelectedApp()
    {
        var selected = GetSelectedApp();
        if (selected is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            this,
            $"Are you sure you want to remove '{selected.Name}' from the list?",
            "Confirm Removal",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            _config.Apps.RemoveAll(a => a.Id == selected.Id);
            SaveConfig();
            RefreshGrid();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Removed '{selected.Name}'.");
        }
    }

    private CompanionApp? GetSelectedApp()
    {
        if (_appsGrid.CurrentRow?.Tag is CompanionApp app)
        {
            return app;
        }

        return null;
    }

    private void StartupCheck_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents) return;

        try
        {
            StartupService.SetEnabled(_startupCheck.Checked);
            AppendLog(_startupCheck.Checked
                ? $"[{DateTime.Now:HH:mm:ss}] Enabled Windows startup (minimized to tray)."
                : $"[{DateTime.Now:HH:mm:ss}] Disabled Windows startup.");
        }
        catch (Exception ex)
        {
            _suppressUiEvents = true;
            _startupCheck.Checked = StartupService.IsEnabled();
            _suppressUiEvents = false;
            MessageBox.Show(this, $"Failed to update Windows startup setting: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SaveWatchedProcess_Click(object? sender, EventArgs e)
    {
        var value = _watchedProcessBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "eurotrucks2";
            _watchedProcessBox.Text = value;
        }

        _config.WatchedProcesses = value;
        SaveConfig();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Updated watched process(es): {value}");
    }

    private void SaveConfig()
    {
        try
        {
            _configService.Save(_config);
            _watcher.UpdateConfig(_config);
        }
        catch (Exception ex)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] ERROR: Failed to save configuration — {ex.Message}");
        }
    }

    private void OnWatcherStatusChanged(WatcherSnapshot snapshot)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => OnWatcherStatusChanged(snapshot));
            }
            catch
            {
                // Form closing.
            }
            return;
        }

        _latestSnapshot = snapshot;

        _suppressUiEvents = true;
        _pauseCheck.Checked = snapshot.IsPaused;
        _trayPauseItem.Checked = snapshot.IsPaused;
        _suppressUiEvents = false;

        if (snapshot.IsPaused)
        {
            _statusBadge.Text = "●  Watcher Paused";
            _statusBadge.ForeColor = TextMuted;
            _trayIcon.Icon = _pausedIcon;
            Icon = _pausedIcon;
            _trayIcon.Text = "ETS 2 Companion Launcher — Paused";
        }
        else if (snapshot.IsGameRunning)
        {
            _statusBadge.Text = "●  Game Running";
            _statusBadge.ForeColor = SuccessGreen;
            _trayIcon.Icon = _runningIcon;
            Icon = _runningIcon;
            _trayIcon.Text = $"ETS 2 Companion Launcher — {snapshot.DetectedGameProcess} running";
        }
        else
        {
            _statusBadge.Text = "●  Waiting for ETS 2";
            _statusBadge.ForeColor = AccentBlue;
            _trayIcon.Icon = _watchingIcon;
            Icon = _watchingIcon;
            _trayIcon.Text = "ETS 2 Companion Launcher — Waiting for ETS 2";
        }

        UpdateGridRunningStates(snapshot);
    }

    private void RefreshGrid()
    {
        var selectedId = GetSelectedApp()?.Id;

        _suppressUiEvents = true;
        _appsGrid.Rows.Clear();

        foreach (var app in _config.Apps)
        {
            var hasValidPath = app.HasValidTarget();
            var pathDisplay = hasValidPath
                ? app.ExecutablePath
                : "Not configured — click 'Auto-Detect' or 'Edit'";

            var isRunning = _latestSnapshot?.AppRunningStates.TryGetValue(app.Id, out var running) == true && running;
            var statusText = isRunning ? "Running" : "Stopped";

            var rowIndex = _appsGrid.Rows.Add(
                app.Enabled,
                app.Name,
                pathDisplay,
                $"{app.LaunchDelaySeconds}s",
                app.CloseOnGameExit,
                statusText);

            var row = _appsGrid.Rows[rowIndex];
            row.Tag = app;

            row.Cells[2].Style.ForeColor = hasValidPath ? TextMuted : WarningAmber;
            row.Cells[5].Style.ForeColor = isRunning ? SuccessGreen : TextMuted;

            if (selectedId is not null && app.Id == selectedId)
            {
                row.Selected = true;
            }
        }

        _suppressUiEvents = false;
    }

    private void UpdateGridRunningStates(WatcherSnapshot snapshot)
    {
        _suppressUiEvents = true;
        foreach (DataGridViewRow row in _appsGrid.Rows)
        {
            if (row.Tag is not CompanionApp app)
            {
                continue;
            }

            var isRunning = snapshot.AppRunningStates.TryGetValue(app.Id, out var running) && running;
            row.Cells[5].Value = isRunning ? "Running" : "Stopped";
            row.Cells[5].Style.ForeColor = isRunning ? SuccessGreen : TextMuted;
        }
        _suppressUiEvents = false;
    }

    private void AppsGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (e.ColumnIndex == 0 || e.ColumnIndex == 4)
        {
            _appsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void AppsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressUiEvents || e.RowIndex < 0) return;
        if (_appsGrid.Rows[e.RowIndex].Tag is not CompanionApp app) return;

        if (e.ColumnIndex == 0)
        {
            app.Enabled = Convert.ToBoolean(_appsGrid.Rows[e.RowIndex].Cells[0].Value);
            SaveConfig();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] '{app.Name}' {(app.Enabled ? "enabled" : "disabled")}.");
        }
        else if (e.ColumnIndex == 4)
        {
            app.CloseOnGameExit = Convert.ToBoolean(_appsGrid.Rows[e.RowIndex].Cells[4].Value);
            SaveConfig();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Close on game exit for '{app.Name}': {(app.CloseOnGameExit ? "On" : "Off")}.");
        }
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => AppendLog(line));
            }
            catch
            {
                // Form closing.
            }
            return;
        }

        if (_logBox.TextLength > 24000)
        {
            _logBox.Text = _logBox.Text[^12000..];
        }

        _logBox.AppendText(line + Environment.NewLine);
    }

    private void ShowBalloon(string title, string message)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(() => ShowBalloon(title, message));
            }
            catch
            {
                // Form closing.
            }
            return;
        }

        _trayIcon.ShowBalloonTip(3500, title, message, ToolTipIcon.Info);
    }

    private static DataGridView CreateAppsGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = SurfaceDark,
            BorderStyle = BorderStyle.None,
            GridColor = BorderDark,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 36 },
            ColumnHeadersHeight = 34,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ElevatedDark,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(6, 0, 6, 0)
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = SurfaceDark,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(40, 56, 82),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 0, 6, 0)
        };

        var colEnabled = new DataGridViewCheckBoxColumn
        {
            HeaderText = "Enabled",
            FillWeight = 9,
            MinimumWidth = 60
        };

        var colName = new DataGridViewTextBoxColumn
        {
            HeaderText = "Application",
            ReadOnly = true,
            FillWeight = 20,
            MinimumWidth = 120
        };

        var colPath = new DataGridViewTextBoxColumn
        {
            HeaderText = "Executable Path / Target",
            ReadOnly = true,
            FillWeight = 41,
            MinimumWidth = 220
        };

        var colDelay = new DataGridViewTextBoxColumn
        {
            HeaderText = "Delay",
            ReadOnly = true,
            FillWeight = 9,
            MinimumWidth = 60
        };

        var colClose = new DataGridViewCheckBoxColumn
        {
            HeaderText = "Close on Exit",
            FillWeight = 12,
            MinimumWidth = 92
        };

        var colStatus = new DataGridViewTextBoxColumn
        {
            HeaderText = "Status",
            ReadOnly = true,
            FillWeight = 11,
            MinimumWidth = 74
        };

        grid.Columns.AddRange(colEnabled, colName, colPath, colDelay, colClose, colStatus);
        return grid;
    }

    private static Button CreateActionButton(string text, int x, int y, int width, Color backColor, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = BorderDark;
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }
}
