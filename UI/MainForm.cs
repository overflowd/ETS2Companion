using System.Drawing;
using ETS2CompanionLauncher.Models;
using ETS2CompanionLauncher.Services;

namespace ETS2CompanionLauncher.UI;

public sealed class MainForm : Form
{
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
    private readonly Label _emptyStateLabel;
    private readonly TextBox _logBox;
    private readonly CheckBox _startupCheck;
    private readonly CheckBox _notifyCheck;
    private readonly CheckBox _pauseCheck;
    private readonly CheckBox _ets2Check;
    private readonly CheckBox _atsCheck;
    private readonly TextBox _customProcessBox;
    private readonly Button _addAppBtn;
    private readonly Button _presetsBtn;
    private readonly Button _autoDetectBtn;
    private readonly Button _launchNowBtn;
    private readonly ContextMenuStrip _addMenu;
    private readonly ContextMenuStrip _presetsMenu;

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
        MinimumSize = new Size(900, 620);
        Size = new Size(950, 660);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        DarkTheme.ApplyDarkTitleBar(this);

        // --- System Tray ---
        var trayMenu = new ContextMenuStrip();
        DarkTheme.StyleMenu(trayMenu);
        var openItem = new ToolStripMenuItem("Open Window", null, (_, _) => RestoreFromTray());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        var launchNowTrayItem = new ToolStripMenuItem("Launch Apps Now", null, async (_, _) => await RunManualLaunchAsync());
        _trayPauseItem = new ToolStripMenuItem("Pause Watching", null, (_, _) => TogglePause());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication());

        trayMenu.Items.AddRange(new ToolStripItem[]
        {
            openItem,
            launchNowTrayItem,
            new ToolStripSeparator(),
            _trayPauseItem,
            new ToolStripSeparator(),
            exitItem
        });

        _trayIcon = new NotifyIcon
        {
            Icon = _watchingIcon,
            Text = "ETS 2 Companion Launcher — Waiting for game",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        // --- Add App Dropdown Menu ---
        _addMenu = new ContextMenuStrip();
        DarkTheme.StyleMenu(_addMenu);
        _addMenu.Items.Add("Pick from Running Apps...", null, (_, _) => AddFromPicker(AppPickerMode.Running));
        _addMenu.Items.Add("Pick from Installed / Start Menu...", null, (_, _) => AddFromPicker(AppPickerMode.Installed));
        _addMenu.Items.Add(new ToolStripSeparator());
        _addMenu.Items.Add("Browse .exe / Custom Entry...", null, (_, _) => AddNewApp());

        // --- Popular Presets Dropdown Menu ---
        _presetsMenu = new ContextMenuStrip();
        DarkTheme.StyleMenu(_presetsMenu);
        foreach (var preset in AppPresets.GetPopularPresets())
        {
            var captured = preset;
            _presetsMenu.Items.Add(captured.Name, null, async (_, _) => await AddFromPresetAsync(captured));
        }

        // --- Layout ---
        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
            BackColor = DarkTheme.Background
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Header
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Toolbar
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));   // Grid
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Settings
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));   // Log
        Controls.Add(rootLayout);

        // 1. Header (DPI-safe TableLayoutPanel)
        var headerPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DarkTheme.Surface,
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 10)
        };
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var titleLabel = new Label
        {
            Text = "ETS 2 Companion Launcher",
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            ForeColor = DarkTheme.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        var subtitleLabel = new Label
        {
            Text = "Automatically launch your chosen apps whenever Euro Truck Simulator 2 or American Truck Simulator starts.",
            Font = new Font("Segoe UI", 8.8f),
            ForeColor = DarkTheme.TextSecondary,
            AutoSize = true,
            Margin = new Padding(0)
        };

        titleStack.Controls.Add(titleLabel, 0, 0);
        titleStack.Controls.Add(subtitleLabel, 0, 1);

        _statusBadge = new Label
        {
            Text = "●  Waiting: ETS 2",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
            ForeColor = DarkTheme.TextPrimary,
            BackColor = DarkTheme.Elevated,
            AutoSize = true,
            MinimumSize = new Size(180, 32),
            Padding = new Padding(14, 6, 14, 6),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(12, 0, 0, 0)
        };

        headerPanel.Controls.Add(titleStack, 0, 0);
        headerPanel.Controls.Add(_statusBadge, 1, 0);
        rootLayout.Controls.Add(headerPanel, 0, 0);

        // 2. Action Toolbar (DPI-safe TableLayoutPanel + FlowLayoutPanel)
        var toolbarPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DarkTheme.Background,
            Margin = new Padding(0, 0, 0, 8)
        };
        toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        _addAppBtn = CreateAutoButton("+ Add App ▾", primary: true);
        _addAppBtn.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        _addAppBtn.Click += (_, _) => _addMenu.Show(_addAppBtn, new Point(0, _addAppBtn.Height));

        _presetsBtn = CreateAutoButton("Presets ▾");
        _presetsBtn.Click += (_, _) => _presetsMenu.Show(_presetsBtn, new Point(0, _presetsBtn.Height));

        var editBtn = CreateAutoButton("Edit");
        editBtn.Click += (_, _) => EditSelectedApp();

        var removeBtn = CreateAutoButton("Remove", foreColorOverride: DarkTheme.Danger);
        removeBtn.Click += (_, _) => DeleteSelectedApp();

        _autoDetectBtn = CreateAutoButton("Detect Missing Paths", foreColorOverride: DarkTheme.TextSecondary);
        _autoDetectBtn.Click += async (_, _) => await RunAutoDetectAsync(forceRescan: false);

        leftButtons.Controls.AddRange(new Control[] { _addAppBtn, _presetsBtn, editBtn, removeBtn, _autoDetectBtn });

        _launchNowBtn = CreateAutoButton("Launch Now (Test)");
        _launchNowBtn.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
        _launchNowBtn.Margin = new Padding(8, 0, 0, 0);
        _launchNowBtn.Click += async (_, _) => await RunManualLaunchAsync();

        toolbarPanel.Controls.Add(leftButtons, 0, 0);
        toolbarPanel.Controls.Add(_launchNowBtn, 1, 0);
        rootLayout.Controls.Add(toolbarPanel, 0, 1);

        // 3. Apps Grid + Empty State Overlay
        var gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Surface
        };

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

        _emptyStateLabel = new Label
        {
            Text = "No companion apps added yet.\r\nClick '+ Add App' to choose any program on your PC, or open 'Presets' to quick-add popular ETS 2 / ATS tools.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = DarkTheme.TextSecondary,
            BackColor = DarkTheme.Surface,
            Font = new Font("Segoe UI", 10f),
            Visible = false
        };

        gridContainer.Controls.Add(_appsGrid);
        gridContainer.Controls.Add(_emptyStateLabel);
        _emptyStateLabel.BringToFront();
        rootLayout.Controls.Add(gridContainer, 0, 2);

        // 4. Settings Panel (DPI-safe two-row FlowLayoutPanel inside TableLayoutPanel)
        var settingsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DarkTheme.Surface,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 10, 0, 10)
        };
        settingsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        settingsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var settingsRow1 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 8)
        };

        _startupCheck = new CheckBox
        {
            Text = "Start minimized with Windows",
            AutoSize = true,
            ForeColor = DarkTheme.TextPrimary,
            Checked = StartupService.IsEnabled(),
            Margin = new Padding(0, 2, 22, 2)
        };
        _startupCheck.CheckedChanged += StartupCheck_CheckedChanged;

        _notifyCheck = new CheckBox
        {
            Text = "Show tray notifications",
            AutoSize = true,
            ForeColor = DarkTheme.TextPrimary,
            Checked = _config.ShowNotifications,
            Margin = new Padding(0, 2, 22, 2)
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
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Margin = new Padding(0, 2, 0, 2)
        };
        _pauseCheck.CheckedChanged += (_, _) =>
        {
            if (_suppressUiEvents) return;
            _watcher?.SetPaused(_pauseCheck.Checked);
        };

        settingsRow1.Controls.AddRange(new Control[] { _startupCheck, _notifyCheck, _pauseCheck });

        var settingsRow2 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };

        var watchLabel = new Label
        {
            Text = "Watch games:",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Margin = new Padding(0, 6, 12, 4)
        };

        _ets2Check = new CheckBox
        {
            Text = "ETS 2 (eurotrucks2)",
            AutoSize = true,
            ForeColor = DarkTheme.TextPrimary,
            Checked = _config.WatchEts2,
            Margin = new Padding(0, 5, 18, 4)
        };
        _ets2Check.CheckedChanged += GameToggle_CheckedChanged;

        _atsCheck = new CheckBox
        {
            Text = "ATS (amtrucks)",
            AutoSize = true,
            ForeColor = DarkTheme.TextPrimary,
            Checked = _config.WatchAts,
            Margin = new Padding(0, 5, 22, 4)
        };
        _atsCheck.CheckedChanged += GameToggle_CheckedChanged;

        var extraLabel = new Label
        {
            Text = "Extra process(es):",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Margin = new Padding(0, 6, 8, 4)
        };

        _customProcessBox = new TextBox
        {
            Text = _config.CustomWatchedProcesses,
            Width = 220,
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "optional, comma-separated",
            Margin = new Padding(0, 2, 8, 2)
        };

        var applyProcessBtn = CreateAutoButton("Apply");
        applyProcessBtn.Padding = new Padding(12, 2, 12, 2);
        applyProcessBtn.Margin = new Padding(0, 1, 0, 1);
        applyProcessBtn.Click += SaveCustomProcesses_Click;

        settingsRow2.Controls.AddRange(new Control[]
        {
            watchLabel, _ets2Check, _atsCheck,
            extraLabel, _customProcessBox, applyProcessBtn
        });

        settingsPanel.Controls.Add(settingsRow1, 0, 0);
        settingsPanel.Controls.Add(settingsRow2, 0, 1);
        rootLayout.Controls.Add(settingsPanel, 0, 3);

        // 5. Log Panel (DPI-safe header row + log box)
        var logContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DarkTheme.Surface,
            Padding = new Padding(12, 8, 12, 10)
        };
        logContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        logContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logHeaderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };
        logHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        logHeaderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var logHeader = new Label
        {
            Text = "Activity Log",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            Margin = new Padding(0)
        };

        _logBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = DarkTheme.LogBackground,
            ForeColor = Color.FromArgb(208, 208, 208),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f),
            Margin = new Padding(0)
        };

        var clearLogBtn = new LinkLabel
        {
            Text = "Clear",
            AutoSize = true,
            LinkColor = DarkTheme.TextSecondary,
            ActiveLinkColor = DarkTheme.TextPrimary,
            VisitedLinkColor = DarkTheme.TextSecondary,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };
        clearLogBtn.Click += (_, _) => _logBox.Clear();

        logHeaderRow.Controls.Add(logHeader, 0, 0);
        logHeaderRow.Controls.Add(clearLogBtn, 1, 0);
        logContainer.Controls.Add(logHeaderRow, 0, 0);
        logContainer.Controls.Add(_logBox, 0, 1);
        rootLayout.Controls.Add(logContainer, 0, 4);

        // Initialize Watcher
        _watcher = new GameWatcherService(_config);
        _watcher.StatusChanged += OnWatcherStatusChanged;
        _watcher.LogMessage += AppendLog;
        _watcher.NotificationRequested += ShowBalloon;

        RefreshGrid();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_startMinimized)
        {
            HideToTray(showTip: false);
        }

        _watcher.Start();
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

    private void AddFromPicker(AppPickerMode mode)
    {
        using var picker = new AppPickerDialog(mode);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedApp is null)
        {
            return;
        }

        var picked = picker.SelectedApp;
        var existing = _config.Apps.FirstOrDefault(a =>
            a.ExecutablePath.Equals(picked.ExecutablePath, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            MessageBox.Show(this, $"'{existing.Name}' is already in your list.", "Already Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var newApp = new CompanionApp
        {
            Name = picked.Name,
            ExecutablePath = picked.ExecutablePath,
            ProcessName = picked.ProcessName,
            Enabled = true,
            LaunchDelaySeconds = 1
        };

        _config.Apps.Add(newApp);
        SaveConfig();
        RefreshGrid();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Added '{newApp.Name}' ({picked.Source}): {newApp.ExecutablePath}");
    }

    private async Task AddFromPresetAsync(CompanionApp presetTemplate)
    {
        var existing = _config.Apps.FirstOrDefault(a =>
            a.Name.Equals(presetTemplate.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            MessageBox.Show(this, $"'{existing.Name}' is already in your list.", "Already Added", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var candidate = presetTemplate.Clone();
        candidate.Id = Guid.NewGuid().ToString("N");

        _presetsBtn.Enabled = false;
        _presetsBtn.Text = "Finding...";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Looking for preset '{candidate.Name}'...");

        DetectionResult? detected = null;
        try
        {
            detected = await Task.Run(() => AppAutoDetector.Detect(candidate));
        }
        finally
        {
            _presetsBtn.Enabled = true;
            _presetsBtn.Text = "Presets ▾";
        }

        if (detected is not null)
        {
            candidate.ExecutablePath = detected.Path;
            if (detected.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                candidate.ProcessName = Path.GetFileNameWithoutExtension(detected.Path);
            }

            _config.Apps.Add(candidate);
            SaveConfig();
            RefreshGrid();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Added preset '{candidate.Name}' ({detected.Source}): {detected.Path}");
            return;
        }

        AppendLog($"[{DateTime.Now:HH:mm:ss}] '{candidate.Name}' was not auto-detected. Opening editor so you can pick its executable.");
        using var dialog = new AppEditDialog(candidate, isNew: true);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _config.Apps.Add(dialog.ResultApp);
            SaveConfig();
            RefreshGrid();
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Added '{dialog.ResultApp.Name}'.");
        }
    }

    private async Task RunAutoDetectAsync(bool forceRescan)
    {
        if (_config.Apps.Count == 0)
        {
            MessageBox.Show(this, "Your app list is empty. Click '+ Add App' or 'Presets' first.", "No Apps", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var targets = _config.Apps
            .Where(a => forceRescan || !a.HasValidTarget())
            .ToList();

        if (targets.Count == 0)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] All configured apps already have valid paths.");
            return;
        }

        _autoDetectBtn.Enabled = false;
        _autoDetectBtn.Text = "Scanning...";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Scanning for missing app paths...");

        try
        {
            var foundCount = 0;
            var missingNames = new List<string>();

            await Task.Run(() =>
            {
                foreach (var app in targets)
                {
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
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Detection completed: {foundCount} path(s) found.");
            }

            if (missingNames.Count > 0)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] Still not found: {string.Join(", ", missingNames)}. Double-click a row to browse manually.");
            }
        }
        finally
        {
            _autoDetectBtn.Enabled = true;
            _autoDetectBtn.Text = "Detect Missing Paths";
        }
    }

    private async Task RunManualLaunchAsync()
    {
        if (_config.Apps.Count == 0)
        {
            MessageBox.Show(this, "Add at least one companion app before testing launch.", "No Apps Configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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

    private void GameToggle_CheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents) return;

        _config.WatchEts2 = _ets2Check.Checked;
        _config.WatchAts = _atsCheck.Checked;
        SaveConfig();

        var watched = _config.GetWatchedProcessList();
        AppendLog(watched.Length > 0
            ? $"[{DateTime.Now:HH:mm:ss}] Monitoring updated: {string.Join(", ", watched)}"
            : $"[{DateTime.Now:HH:mm:ss}] No games selected to monitor.");

        if (_latestSnapshot is not null)
        {
            UpdateStatusDisplay(_latestSnapshot);
        }
    }

    private void SaveCustomProcesses_Click(object? sender, EventArgs e)
    {
        _config.CustomWatchedProcesses = _customProcessBox.Text.Trim();
        SaveConfig();

        var watched = _config.GetWatchedProcessList();
        AppendLog(watched.Length > 0
            ? $"[{DateTime.Now:HH:mm:ss}] Monitoring updated: {string.Join(", ", watched)}"
            : $"[{DateTime.Now:HH:mm:ss}] No games selected to monitor.");

        if (_latestSnapshot is not null)
        {
            UpdateStatusDisplay(_latestSnapshot);
        }
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

        UpdateStatusDisplay(snapshot);
        UpdateGridRunningStates(snapshot);
    }

    private void UpdateStatusDisplay(WatcherSnapshot snapshot)
    {
        if (snapshot.IsPaused)
        {
            _statusBadge.Text = "●  Watcher Paused";
            _statusBadge.ForeColor = DarkTheme.TextMuted;
            _trayIcon.Icon = _pausedIcon;
            Icon = _pausedIcon;
            _trayIcon.Text = "ETS 2 Companion Launcher — Paused";
        }
        else if (snapshot.IsGameRunning)
        {
            var label = snapshot.DetectedGameProcess?.Equals("amtrucks", StringComparison.OrdinalIgnoreCase) == true
                ? "ATS Running"
                : snapshot.DetectedGameProcess?.Equals("eurotrucks2", StringComparison.OrdinalIgnoreCase) == true
                    ? "ETS 2 Running"
                    : "Game Running";
            _statusBadge.Text = $"●  {label}";
            _statusBadge.ForeColor = DarkTheme.Success;
            _trayIcon.Icon = _runningIcon;
            Icon = _runningIcon;
            _trayIcon.Text = TruncateTrayText($"ETS 2 Companion — {snapshot.DetectedGameProcess} running");
        }
        else
        {
            var summary = _config.GetWatchedSummaryLabel();
            var waitingText = summary == "No Game Selected" ? "No Game Selected" : $"Waiting: {summary}";
            _statusBadge.Text = $"●  {waitingText}";
            _statusBadge.ForeColor = summary == "No Game Selected" ? DarkTheme.Warning : DarkTheme.TextPrimary;
            _trayIcon.Icon = _watchingIcon;
            Icon = _watchingIcon;
            _trayIcon.Text = TruncateTrayText($"ETS 2 Companion — {waitingText}");
        }
    }

    private static string TruncateTrayText(string text) =>
        text.Length <= 63 ? text : text[..60] + "...";

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
                : "Not configured — double-click to select path";

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

            row.Cells[2].Style.ForeColor = hasValidPath ? DarkTheme.TextSecondary : DarkTheme.Warning;
            row.Cells[5].Style.ForeColor = isRunning ? DarkTheme.Success : DarkTheme.TextMuted;

            if (selectedId is not null && app.Id == selectedId)
            {
                row.Selected = true;
            }
        }

        _emptyStateLabel.Visible = _config.Apps.Count == 0;
        _autoDetectBtn.Visible = _config.Apps.Any(a => !a.HasValidTarget());

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
            row.Cells[5].Style.ForeColor = isRunning ? DarkTheme.Success : DarkTheme.TextMuted;
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
            BackgroundColor = DarkTheme.Surface,
            BorderStyle = BorderStyle.None,
            GridColor = DarkTheme.Border,
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
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(6, 0, 6, 0)
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextPrimary,
            SelectionBackColor = DarkTheme.Selection,
            SelectionForeColor = DarkTheme.TextPrimary,
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

    private static Button CreateAutoButton(string text, bool primary = false, Color? foreColorOverride = null) =>
        DarkTheme.CreateAutoButton(text, primary, foreColorOverride);
}
