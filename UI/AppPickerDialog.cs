using System.Drawing;
using ETS2CompanionLauncher.Services;

namespace ETS2CompanionLauncher.UI;

public enum AppPickerMode
{
    Running,
    Installed
}

public sealed class AppPickerDialog : Form
{
    private readonly Button _runningTabBtn;
    private readonly Button _installedTabBtn;
    private readonly TextBox _searchBox;
    private readonly Button _refreshBtn;
    private readonly DataGridView _grid;
    private readonly Label _statusLabel;
    private readonly Button _selectBtn;

    private AppPickerMode _mode;
    private List<DiscoveredApp> _currentItems = new();
    private List<DiscoveredApp>? _cachedInstalled;

    public DiscoveredApp? SelectedApp { get; private set; }

    public AppPickerDialog(AppPickerMode initialMode = AppPickerMode.Running)
    {
        _mode = initialMode;

        Text = "Pick an Application";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(700, 480);
        Size = new Size(780, 540);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DarkTheme.Background;
        ForeColor = DarkTheme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        DarkTheme.ApplyDarkTitleBar(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(14),
            BackColor = DarkTheme.Background
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // 1. Mode tabs
        var tabRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DarkTheme.Background,
            Margin = new Padding(0, 0, 0, 8)
        };
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tabRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var tabsFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };

        _runningTabBtn = DarkTheme.CreateAutoButton("Running Apps");
        _runningTabBtn.Click += async (_, _) => await SwitchModeAsync(AppPickerMode.Running);

        _installedTabBtn = DarkTheme.CreateAutoButton("Installed Apps (Start Menu & Steam)");
        _installedTabBtn.Click += async (_, _) => await SwitchModeAsync(AppPickerMode.Installed);

        tabsFlow.Controls.AddRange(new Control[] { _runningTabBtn, _installedTabBtn });

        _refreshBtn = DarkTheme.CreateAutoButton("Refresh");
        _refreshBtn.Margin = new Padding(8, 0, 0, 0);
        _refreshBtn.Click += async (_, _) =>
        {
            if (_mode == AppPickerMode.Installed) _cachedInstalled = null;
            await LoadItemsAsync();
        };

        tabRow.Controls.Add(tabsFlow, 0, 0);
        tabRow.Controls.Add(_refreshBtn, 1, 0);
        root.Controls.Add(tabRow, 0, 0);

        // 2. Search bar
        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DarkTheme.Background,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var searchLabel = new Label
        {
            Text = "Search:",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 10, 4)
        };

        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Type an app name or path to filter...",
            Margin = new Padding(0)
        };
        _searchBox.TextChanged += (_, _) => ApplyFilter();

        searchRow.Controls.Add(searchLabel, 0, 0);
        searchRow.Controls.Add(_searchBox, 1, 0);
        root.Controls.Add(searchRow, 0, 1);

        // 3. Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = DarkTheme.Surface,
            BorderStyle = BorderStyle.None,
            GridColor = DarkTheme.Border,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            EnableHeadersVisualStyles = false,
            RowHeadersVisible = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 32 },
            ColumnHeadersHeight = 32,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };

        _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DarkTheme.Elevated,
            ForeColor = DarkTheme.TextSecondary,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Padding = new Padding(6, 0, 6, 0)
        };

        _grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextPrimary,
            SelectionBackColor = DarkTheme.Selection,
            SelectionForeColor = DarkTheme.TextPrimary,
            Padding = new Padding(6, 0, 6, 0)
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Application", FillWeight = 32, MinimumWidth = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", FillWeight = 16, MinimumWidth = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Path", FillWeight = 52, MinimumWidth = 220 });

        _grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0)
            {
                ConfirmSelection();
            }
        };

        root.Controls.Add(_grid, 0, 2);

        // 4. Footer
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DarkTheme.Background,
            Margin = new Padding(0, 10, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _statusLabel = new Label
        {
            Text = "Loading...",
            AutoSize = true,
            ForeColor = DarkTheme.TextSecondary,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 0, 6)
        };

        var footerBtns = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };

        var cancelBtn = DarkTheme.CreateAutoButton("Cancel");
        cancelBtn.DialogResult = DialogResult.Cancel;

        _selectBtn = DarkTheme.CreateAutoButton("Select", primary: true);
        _selectBtn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _selectBtn.Margin = new Padding(0);
        _selectBtn.Click += (_, _) => ConfirmSelection();

        footerBtns.Controls.AddRange(new Control[] { cancelBtn, _selectBtn });
        footer.Controls.Add(_statusLabel, 0, 0);
        footer.Controls.Add(footerBtns, 1, 0);
        root.Controls.Add(footer, 0, 3);

        AcceptButton = _selectBtn;
        CancelButton = cancelBtn;
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        UpdateTabStyling();
        await LoadItemsAsync();
        _searchBox.Focus();
    }

    private async Task SwitchModeAsync(AppPickerMode newMode)
    {
        if (_mode == newMode) return;
        _mode = newMode;
        UpdateTabStyling();
        await LoadItemsAsync();
    }

    private void UpdateTabStyling()
    {
        var isRunning = _mode == AppPickerMode.Running;
        _runningTabBtn.BackColor = isRunning ? DarkTheme.AccentPrimary : DarkTheme.Surface;
        _runningTabBtn.ForeColor = isRunning ? DarkTheme.AccentOnPrimary : DarkTheme.TextSecondary;
        _runningTabBtn.Font = new Font("Segoe UI", 9.5f, isRunning ? FontStyle.Bold : FontStyle.Regular);

        _installedTabBtn.BackColor = !isRunning ? DarkTheme.AccentPrimary : DarkTheme.Surface;
        _installedTabBtn.ForeColor = !isRunning ? DarkTheme.AccentOnPrimary : DarkTheme.TextSecondary;
        _installedTabBtn.Font = new Font("Segoe UI", 9.5f, !isRunning ? FontStyle.Bold : FontStyle.Regular);
    }

    private async Task LoadItemsAsync()
    {
        _refreshBtn.Enabled = false;
        _statusLabel.Text = _mode == AppPickerMode.Running
            ? "Scanning running applications..."
            : "Scanning Start Menu, Steam libraries, and installed apps...";
        _grid.Rows.Clear();

        try
        {
            var mode = _mode;
            List<DiscoveredApp> loaded;
            if (mode == AppPickerMode.Running)
            {
                loaded = await Task.Run(AppAutoDetector.GetRunningUserApps);
            }
            else
            {
                loaded = _cachedInstalled ?? await Task.Run(AppAutoDetector.GetInstalledApps);
                _cachedInstalled = loaded;
            }

            if (_mode != mode || IsDisposed)
            {
                return;
            }

            _currentItems = loaded;
            ApplyFilter();
        }
        finally
        {
            if (!IsDisposed)
            {
                _refreshBtn.Enabled = true;
            }
        }
    }

    private void ApplyFilter()
    {
        var query = _searchBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _currentItems
            : _currentItems.Where(a =>
                a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.ExecutablePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _grid.Rows.Clear();
        foreach (var item in filtered)
        {
            var idx = _grid.Rows.Add(item.Name, item.Source, item.ExecutablePath);
            var row = _grid.Rows[idx];
            row.Tag = item;
            row.Cells[1].Style.ForeColor = DarkTheme.TextSecondary;
            row.Cells[2].Style.ForeColor = DarkTheme.TextMuted;
        }

        _statusLabel.Text = $"{filtered.Count} app(s) shown";
    }

    private void ConfirmSelection()
    {
        if (_grid.CurrentRow?.Tag is not DiscoveredApp app)
        {
            MessageBox.Show(this, "Please select an application from the list.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SelectedApp = app;
        DialogResult = DialogResult.OK;
        Close();
    }
}
